using AquaBotApi.Data;
using AquaBotApi.Models;
using AquaBotApi.Models.DTOs;
using AquaBotApi.Service;
using AquaBotApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Security.Claims;

namespace AquaBotApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ImageAnalysisController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly OnnxImageAnalysisService _onnxImageAnalysisService;
        private readonly ImageAnalysisService _heuristicService; // 🔄 fallback
        private readonly EnhancedWaterCalculationService _enhancedWaterCalculationService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageAnalysisController> _logger;

        public ImageAnalysisController(
            AppDbContext context,
            OnnxImageAnalysisService onnxImageAnalysisService,
            ImageAnalysisService heuristicService,
            EnhancedWaterCalculationService enhancedWaterCalculationService,
            IWebHostEnvironment environment,
            ILogger<ImageAnalysisController> logger)
        {
            _context = context;
            _onnxImageAnalysisService = onnxImageAnalysisService;
            _heuristicService = heuristicService;
            _enhancedWaterCalculationService = enhancedWaterCalculationService;
            _environment = environment;
            _logger = logger;
        }

        /// <summary>
        /// Upload and analyze soil/crop image with ML + weather-based calculations
        /// Saves result in DB
        /// </summary>
        [HttpPost("analyze")]
        public async Task<ActionResult<ImageAnalysisResponseDto>> AnalyzeImage([FromForm] ImageUploadDto dto)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (dto.Image == null || dto.Image.Length == 0)
                    return BadRequest(new ImageAnalysisResponseDto { Success = false, Message = "No image file provided" });

                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
                if (!allowedTypes.Contains(dto.Image.ContentType.ToLower()))
                    return BadRequest(new ImageAnalysisResponseDto { Success = false, Message = "Only JPEG and PNG images are supported" });

                if (dto.Image.Length > 10 * 1024 * 1024)
                    return BadRequest(new ImageAnalysisResponseDto { Success = false, Message = "Image size must be less than 10MB" });

                double? fieldArea = (dto.FieldArea.HasValue && dto.FieldArea.Value > 0) ? dto.FieldArea : null;

                var fileName = await SaveUploadedImageAsync(dto.Image);

                // ✅ Use ONNX model
                var analysisResult = _onnxImageAnalysisService.AnalyzeImage(dto.Image, dto.CropType);

                // ✅ Fallback if low confidence
                if (analysisResult.Confidence < 60)
                {
                    _logger.LogWarning("Low confidence ({Confidence}%) from ONNX model. Falling back to heuristic service.", analysisResult.Confidence);
                    analysisResult = await _heuristicService.AnalyzeImageAsync(dto.Image, dto.CropType);
                }

                // ✅ Use provided location (fallback Lahore)
                var location = dto.FieldLocation ?? "Lahore";
                var enhancedRecommendation = await _enhancedWaterCalculationService
                    .CalculateFromImageAndWeatherAsync(analysisResult, location, dto.CropType ?? string.Empty, fieldArea);

                // ✅ Save record in DB
                var dbRecord = new ImageAnalysisResult
                {
                    UserId = userId!,
                    FileName = fileName,
                    SoilCondition = analysisResult.SoilCondition,
                    EstimatedMoisture = analysisResult.EstimatedMoisture,
                    CropHealth = analysisResult.CropHealth,
                    CropType = analysisResult.CropType,
                    FieldLocation = location
                };

                _context.ImageAnalysisResults.Add(dbRecord);
                await _context.SaveChangesAsync();

                var imageUrl = $"{Request.Scheme}://{Request.Host}/uploads/images/{fileName}";

                return Ok(new ImageAnalysisResponseDto
                {
                    Success = true,
                    Message = "Image analyzed with weather data successfully",
                    ImageId = dbRecord.Id,
                    ImageUrl = imageUrl,
                    ImageAnalysis = analysisResult,
                    FieldLocation = dbRecord.FieldLocation,
                    WaterRecommendation = enhancedRecommendation
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing image");
                return StatusCode(500, new ImageAnalysisResponseDto
                {
                    Success = false,
                    Message = "Internal server error. Please try again later."
                });
            }
        }

        /// <summary>
        /// Quick farmer-friendly analysis (not saved in DB)
        /// </summary>
        [HttpPost("analyze-simple")]
        public async Task<ActionResult<FarmerRecommendationDto>> AnalyzeSimple([FromForm] ImageUploadDto dto)
        {
            var analysisResult = _onnxImageAnalysisService.AnalyzeImage(dto.Image, dto.CropType);

            if (analysisResult.Confidence < 60)
            {
                _logger.LogWarning("Low confidence from ONNX model. Falling back to heuristic analysis (simple).");
                analysisResult = await _heuristicService.AnalyzeImageAsync(dto.Image, dto.CropType);
            }

            var location = dto.FieldLocation ?? "Lahore";
            var enhancedRecommendation = await _enhancedWaterCalculationService
                .CalculateFromImageAndWeatherAsync(analysisResult, location, dto.CropType ?? string.Empty, dto.FieldArea);

            return Ok(new FarmerRecommendationDto
            {
                SoilCondition = analysisResult.SoilCondition,
                CropType = dto.CropType ?? "Unknown",
                FieldLocation = dto.FieldLocation ?? "Lahore",
                Recommendation = enhancedRecommendation.Recommendation,
                Urgency = enhancedRecommendation.IrrigationUrgency.ToString(),
                WaterPerSquareMeter = enhancedRecommendation.WaterPerSquareMeter,
                TotalWaterNeeded = enhancedRecommendation.TotalWaterNeeded
            });
        }

        /// <summary>
        /// Get user’s past analyses
        /// </summary>
        [HttpGet("history")]
        public IActionResult GetAnalysisHistory()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var history = _context.ImageAnalysisResults
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.SoilCondition,
                    r.EstimatedMoisture,
                    r.CropHealth,
                    r.CropType,
                    r.FieldLocation,   // ✅ include location
                    r.CreatedAt,
                    r.FileName
                })
                .Take(20)
                .ToList();

            return Ok(history);
        }

        #region Private Helpers
        private async Task<string> SaveUploadedImageAsync(IFormFile image)
        {
            var uploadsPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "images");
            Directory.CreateDirectory(uploadsPath);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(image.FileName)}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using var imageSharp = await SixLabors.ImageSharp.Image.LoadAsync(image.OpenReadStream());

            const int maxWidth = 1920;
            if (imageSharp.Width > maxWidth)
            {
                imageSharp.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxWidth, 0),
                    Mode = ResizeMode.Max
                }));
            }

            var encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
            {
                Quality = 85
            };

            await imageSharp.SaveAsync(filePath, encoder);

            return fileName;
        }
        #endregion
    }
}
