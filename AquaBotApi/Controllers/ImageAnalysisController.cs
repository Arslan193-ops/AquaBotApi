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
        private readonly EnhancedWaterCalculationService _enhancedWaterCalculationService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageAnalysisController> _logger;

        public ImageAnalysisController(
            AppDbContext context,
            OnnxImageAnalysisService onnxImageAnalysisService,   // ✅ switched to ONNX
            EnhancedWaterCalculationService enhancedWaterCalculationService,
            IWebHostEnvironment environment,
            ILogger<ImageAnalysisController> logger)
        {
            _context = context;
            _onnxImageAnalysisService = onnxImageAnalysisService;
            _enhancedWaterCalculationService = enhancedWaterCalculationService;
            _environment = environment;
            _logger = logger;
        }

        /// <summary>
        /// Upload and analyze soil/crop image with ML + weather-based calculations
        /// POST: api/imageanalysis/analyze
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

                // ✅ Use ONNX model for image analysis
                var analysisResult = _onnxImageAnalysisService.AnalyzeImage(dto.Image, dto.CropType);

                // ✅ Add weather-based recommendation
                var enhancedRecommendation = await _enhancedWaterCalculationService
                    .CalculateFromImageAndWeatherAsync(analysisResult, "Lahore", dto.CropType ?? string.Empty, fieldArea);

                // ✅ Save record in DB
                var dbRecord = new ImageAnalysisResult
                {
                    UserId = userId!,
                    FileName = fileName,
                    SoilCondition = analysisResult.SoilCondition,
                    EstimatedMoisture = analysisResult.EstimatedMoisture,
                    CropHealth = analysisResult.CropHealth,
                    CropType = analysisResult.CropType
                };

                _context.ImageAnalysisResults.Add(dbRecord);
                await _context.SaveChangesAsync();

                var imageUrl = $"{Request.Scheme}://{Request.Host}/uploads/images/{fileName}";

                return Ok(new ImageAnalysisResponseDto
                {
                    Success = true,
                    Message = "Image analyzed with weather data successfully (ML model)",
                    ImageId = dbRecord.Id,
                    ImageUrl = imageUrl,
                    ImageAnalysis = analysisResult,
                    WaterRecommendation = enhancedRecommendation
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing image with ONNX model");
                return StatusCode(500, new ImageAnalysisResponseDto
                {
                    Success = false,
                    Message = "Internal server error. Please try again later."
                });
            }
        }

        [HttpPost("analyze-simple")]
        public async Task<ActionResult<FarmerRecommendationDto>> AnalyzeSimple([FromForm] ImageUploadDto dto)
        {
            var analysisResult = _onnxImageAnalysisService.AnalyzeImage(dto.Image, dto.CropType);
            var enhancedRecommendation = await _enhancedWaterCalculationService
                .CalculateFromImageAndWeatherAsync(analysisResult, "Lahore", dto.CropType ?? string.Empty, dto.FieldArea);

            var farmerDto = new FarmerRecommendationDto
            {
                SoilCondition = analysisResult.SoilCondition,
                CropType = dto.CropType ?? "Unknown",
                Recommendation = enhancedRecommendation.Recommendation,
                Urgency = enhancedRecommendation.IrrigationUrgency switch
                {
                    IrrigationUrgency.Low => "Low",
                    IrrigationUrgency.Medium => "Medium",
                    IrrigationUrgency.High => "High",
                    IrrigationUrgency.Critical => "Critical",
                    _ => "Unknown"
                },
                WaterPerSquareMeter = enhancedRecommendation.WaterPerSquareMeter,
                TotalWaterNeeded = enhancedRecommendation.TotalWaterNeeded
            };

            return Ok(farmerDto);
        }


        /// <summary>
        /// Get user's image analysis history
        /// GET: api/imageanalysis/history
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
                    r.CreatedAt,
                    r.FileName
                })
                .Take(20)
                .ToList();

            return Ok(history);
        }

        /// <summary>
        /// Get specific analysis details
        /// GET: api/imageanalysis/{id}
        /// </summary>
        [HttpGet("{id}")]
        public IActionResult GetAnalysisById(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var analysis = _context.ImageAnalysisResults
                .Where(r => r.Id == id && r.UserId == userId)
                .Select(r => new
                {
                    r.Id,
                    r.SoilCondition,
                    r.EstimatedMoisture,
                    r.CropHealth,
                    r.CropType,
                    r.CreatedAt,
                    r.FileName
                })
                .FirstOrDefault();

            if (analysis == null)
                return NotFound("Analysis not found");

            return Ok(analysis);
        }

        /// <summary>
        /// Quick analyze with ML + weather calculations (not saved in DB)
        /// POST: api/imageanalysis/quick-analyze
        /// </summary>
        [HttpPost("quick-analyze")]
        public async Task<ActionResult<ImageAnalysisResponseDto>> QuickAnalyze([FromForm] IFormFile image, [FromForm] string? cropType = null, [FromForm] double? fieldArea = null)
        {
            try
            {
                if (image == null || image.Length == 0)
                    return BadRequest(new ImageAnalysisResponseDto { Success = false, Message = "No image file provided" });

                // ✅ Use ONNX model
                var analysisResult = _onnxImageAnalysisService.AnalyzeImage(image, cropType);

                var enhancedRecommendation = await _enhancedWaterCalculationService
                    .CalculateFromImageAndWeatherAsync(analysisResult, "Lahore", cropType ?? string.Empty, fieldArea);

                return Ok(new ImageAnalysisResponseDto
                {
                    Success = true,
                    Message = "Quick analysis with weather data completed (ML model)",
                    ImageAnalysis = analysisResult,
                    WaterRecommendation = enhancedRecommendation
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in quick analysis with ONNX model");
                return StatusCode(500, new ImageAnalysisResponseDto
                {
                    Success = false,
                    Message = "Internal server error. Please try again later."
                });
            }
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
