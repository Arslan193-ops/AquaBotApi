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
    public class WaterController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly WeatherService _weatherService;
        private readonly EnhancedWaterCalculationService _waterService;

        public WaterController(
            AppDbContext context,
            WeatherService weatherService,
            EnhancedWaterCalculationService waterService)
        {
            _context = context;
            _weatherService = weatherService;
            _waterService = waterService;
        }

        // POST: api/water/recommend
        [HttpPost("recommend")]
        public async Task<IActionResult> RecommendWater(SoilDataDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // ✅ Validation
            if (dto.FieldAreaM2.HasValue && dto.FieldAreaM2 <= 0)
            {
                return BadRequest("Field area must be greater than 0.");
            }

            // 🌦 Fetch weather
            var weather = await _weatherService.GetWeatherAsync("Lahore");

            // 📝 Save soil + weather snapshot in DB
            var soil = new SoilData
            {
                Condition = dto.Condition,
                MoisturePercentage = dto.MoisturePercentage,
                UserId = userId!,
                Temperature = weather?.Temperature,
                Humidity = weather?.Humidity
            };

            _context.SoilDatas.Add(soil);
            await _context.SaveChangesAsync();

            // 🔥 Use EnhancedWaterCalculationService
            var imageLikeData = new ImageAnalysisDto
            {
                SoilCondition = dto.Condition,
                EstimatedMoisture = dto.MoisturePercentage,
                CropHealth = "Unknown", // no image input
                Confidence = 50
            };

            var recommendation = await _waterService.CalculateFromImageAndWeatherAsync(
                imageLikeData,
                "Lahore",
                dto.CropType,
                dto.FieldAreaM2
            );

            // 📤 Return enriched response
            return Ok(new
            {
                soil.Id,
                soil.Condition,
                soil.MoisturePercentage,
                soil.Temperature,
                soil.Humidity,
                soil.CreatedAt,
                WaterRecommendation = recommendation
            });
        }
    }
}
