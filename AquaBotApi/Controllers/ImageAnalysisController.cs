// Controllers/ImageAnalysisController.cs
using AquaBotApi.Data;
using AquaBotApi.Models;
using AquaBotApi.Models.DTOs;
using AquaBotApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        private readonly WaterCalculationService _waterCalculationService;

        public ImageAnalysisController(
            AppDbContext context,
            ImageAnalysisService imageAnalysisService,
            EnhancedWaterCalculationService enhancedWaterCalculationService,
            IWebHostEnvironment environment,
            WaterCalculationService waterCalculationService)
        {
            _context = context;
            _imageAnalysisService = imageAnalysisService;
            _enhancedWaterCalculationService = enhancedWaterCalculationService;
            _environment = environment;
            _waterCalculationService = waterCalculationService;
        }

        /// <summary>
        /// Upload and analyze soil/crop image with enhanced weather-based calculations
        /// POST: api/imageanalysis/analyze
        /// </summary>
        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeImage([FromForm] ImageUploadDto dto)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Validate file
                if (dto.Image == null || dto.Image.Length == 0)
                    return BadRequest("No image file provided");

                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
                if (!allowedTypes.Contains(dto.Image.ContentType.ToLower()))
                    return BadRequest("Only JPEG and PNG images are supported");

                if (dto.Image.Length > 10 * 1024 * 1024) // 10MB limit
                    return BadRequest("Image size must be less than 10MB");

                // Parse field area from form data
                double? fieldArea = null;
                if (dto.FieldArea.HasValue && dto.FieldArea > 0)
                {
                    fieldArea = dto.FieldArea.Value;
                }

                // Save uploaded image
                var fileName = await SaveUploadedImageAsync(dto.Image);

                // Analyze the image
                var analysisResult = await _imageAnalysisService.AnalyzeImageAsync(dto.Image, dto.CropType);

                // 🔥 Enhanced calculation with weather + image data
                var enhancedRecommendation = await _enhancedWaterCalculationService
                    .CalculateFromImageAndWeatherAsync(
                        analysisResult,
                        "Lahore", // Could be from GPS or user input
                        dto.CropType ?? string.Empty, // Ensure non-null value
                        fieldArea
                    );

                // Save analysis to database
                var dbRecord = new ImageAnalysisResult
                {
                    UserId = userId!,
                    FileName = fileName,
                    SoilCondition = analysisResult.SoilCondition,
                    EstimatedMoisture = analysisResult.EstimatedMoisture,
                    CropHealth = analysisResult.CropHealth,
                    CropType = analysisResult.CropType,
                    AvgBrightness = 0, // You can get this from analysis if needed
                    GreenPercentage = 0, // You can get this from analysis if needed
                    BrownPercentage = 0, // You can get this from analysis if needed
                };

                _context.ImageAnalysisResults.Add(dbRecord);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Success = true,
                    ImageAnalysis = analysisResult,
                    WaterRecommendation = enhancedRecommendation,
                    ImageId = dbRecord.Id,
                    Message = "Image analyzed with weather data successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error analyzing image",
                    Error = ex.Message
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
                .Take(20) // Last 20 analyses
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
                    r.AvgBrightness,
                    r.GreenPercentage,
                    r.BrownPercentage,
                    r.CreatedAt,
                    r.FileName
                })
                .FirstOrDefault();

            if (analysis == null)
                return NotFound("Analysis not found");

            return Ok(analysis);
        }

        /// <summary>
        /// Quick analyze with enhanced weather calculations
        /// POST: api/imageanalysis/quick-analyze
        /// </summary>
        [HttpPost("quick-analyze")]
        public async Task<IActionResult> QuickAnalyze([FromForm] IFormFile image, [FromForm] string? cropType = null, [FromForm] double? fieldArea = null)
        {
            try
            {
                if (image == null || image.Length == 0)
                    return BadRequest("No image file provided");

                var analysisResult = await _imageAnalysisService.AnalyzeImageAsync(image, cropType);

                // 🔥 Enhanced calculation with weather data
                var enhancedRecommendation = await _enhancedWaterCalculationService
                    .CalculateFromImageAndWeatherAsync(
                        analysisResult,
                        "Lahore", // Default location
                        cropType ?? string.Empty, // Ensure non-null value
                        fieldArea
                    );

                return Ok(new
                {
                    Success = true,
                    ImageAnalysis = analysisResult,
                    WaterRecommendation = enhancedRecommendation,
                    Message = "Quick analysis with weather data completed"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error in quick analysis",
                    Error = ex.Message
                });
            }
        }

        #region Private Methods

        /// <summary>
        /// Save uploaded image to server storage
        /// </summary>
        private async Task<string> SaveUploadedImageAsync(IFormFile image)
        {
            var uploadsPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "images");
            Directory.CreateDirectory(uploadsPath);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(image.FileName)}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await image.CopyToAsync(stream);

            return fileName;
        }

        /// <summary>
        /// Calculate water recommendation based on image analysis
        /// </summary>
        private object CalculateWaterFromImage(ImageAnalysisDto analysis)
        {
            // Use existing water calculation service with image-derived moisture
            var waterPerSquareMeter = _waterCalculationService.Calculate(
                moisture: analysis.EstimatedMoisture,
                temperature: 25.0, // Default temp - could be enhanced with weather data
                humidity: 60 // Default humidity - could be enhanced with weather data
            );

            return new
            {
                EstimatedMoisture = analysis.EstimatedMoisture,
                SoilCondition = analysis.SoilCondition,
                WaterPerSquareMeter = $"{waterPerSquareMeter} L/m²",
                Recommendation = GenerateWateringRecommendation(analysis.SoilCondition, waterPerSquareMeter),
                Confidence = analysis.Confidence,
                NextCheckIn = GetNextCheckRecommendation(analysis.SoilCondition)
            };
        }

        /// <summary>
        /// Generate human-readable watering recommendation
        /// </summary>
        private string GenerateWateringRecommendation(string soilCondition, double waterNeeded)
        {
            return soilCondition.ToLower() switch
            {
                "dry" => $"🚨 Urgent: Apply {waterNeeded} L/m² immediately. Check soil daily.",
                "moist" => $"✅ Good condition. Light watering: {waterNeeded} L/m² if needed.",
                "wet" => "⏸️ Stop watering. Allow soil to dry before next irrigation.",
                _ => $"💧 Apply {waterNeeded} L/m² as needed based on crop requirements."
            };
        }

        /// <summary>
        /// Recommend when to check again
        /// </summary>
        private string GetNextCheckRecommendation(string soilCondition)
        {
            return soilCondition.ToLower() switch
            {
                "dry" => "Check again in 1 day",
                "moist" => "Check again in 2-3 days",
                "wet" => "Check again in 4-5 days",
                _ => "Check again in 2-3 days"
            };
        }

        #endregion
    }
}