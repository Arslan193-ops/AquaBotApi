using AquaBotApi.Data;
using AquaBotApi.Models;
using AquaBotApi.Models.DTOs;
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
        private readonly ImageAnalysisService _imageAnalysisService;
        private readonly EnhancedWaterCalculationService _enhancedWaterCalculationService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageAnalysisController> _logger;

        public ImageAnalysisController(
            AppDbContext context,
            ImageAnalysisService imageAnalysisService,
            EnhancedWaterCalculationService enhancedWaterCalculationService,
            IWebHostEnvironment environment,
            ILogger<ImageAnalysisController> logger)
        {
            _context = context;
            _imageAnalysisService = imageAnalysisService;
            _enhancedWaterCalculationService = enhancedWaterCalculationService;
            _environment = environment;
            _logger = logger;
        }

        /// <summary>
        /// Upload and analyze soil/crop image with enhanced weather-based calculations
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
                var analysisResult = await _imageAnalysisService.AnalyzeImageAsync(dto.Image, dto.CropType);
                var enhancedRecommendation = await _enhancedWaterCalculationService
                    .CalculateFromImageAndWeatherAsync(analysisResult, "Lahore", dto.CropType ?? string.Empty, fieldArea);

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
                    Message = "Image analyzed with weather data successfully",
                    ImageId = dbRecord.Id,
                    ImageUrl = imageUrl,
                    ImageAnalysis = analysisResult,
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
        /// Quick analyze with enhanced weather calculations (not saved in DB)
        /// POST: api/imageanalysis/quick-analyze
        /// </summary>
        [HttpPost("quick-analyze")]
        public async Task<ActionResult<ImageAnalysisResponseDto>> QuickAnalyze([FromForm] IFormFile image, [FromForm] string? cropType = null, [FromForm] double? fieldArea = null)
        {
            try
            {
                if (image == null || image.Length == 0)
                    return BadRequest(new ImageAnalysisResponseDto { Success = false, Message = "No image file provided" });

                var analysisResult = await _imageAnalysisService.AnalyzeImageAsync(image, cropType);
                var enhancedRecommendation = await _enhancedWaterCalculationService
                    .CalculateFromImageAndWeatherAsync(analysisResult, "Lahore", cropType ?? string.Empty, fieldArea);

                return Ok(new ImageAnalysisResponseDto
                {
                    Success = true,
                    Message = "Quick analysis with weather data completed",
                    ImageAnalysis = analysisResult,
                    WaterRecommendation = enhancedRecommendation
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in quick analysis");
                return StatusCode(500, new ImageAnalysisResponseDto
                {
                    Success = false,
                    Message = "Internal server error. Please try again later."
                });
            }
        }

        #region Private Helpers

        /// <summary>
        /// Save uploaded image using ImageSharp (cross-platform) with optional resize/compression
        /// </summary>
        private async Task<string> SaveUploadedImageAsync(IFormFile image)
        {
            var uploadsPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "images");
            Directory.CreateDirectory(uploadsPath);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(image.FileName)}";
            var filePath = Path.Combine(uploadsPath, fileName);

            // ✅ Load with ImageSharp
            using var imageSharp = await SixLabors.ImageSharp.Image.LoadAsync(image.OpenReadStream());

            // ⚖️ Resize if too large (e.g., > 1920px width)
            const int maxWidth = 1920;
            if (imageSharp.Width > maxWidth)
            {
                imageSharp.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxWidth, 0),
                    Mode = ResizeMode.Max
                }));
            }

            // ✅ Save as JPEG with compression (quality 85%)
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
