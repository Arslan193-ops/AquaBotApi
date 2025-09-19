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
    [Authorize]   // ✅ Require login
    public class SoilDataController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly WeatherService _weatherService;
        private readonly EnhancedWaterCalculationService _waterService;

        public SoilDataController(
            AppDbContext context,
            WeatherService weatherService,
            EnhancedWaterCalculationService waterService)
        {
            _context = context;
            _weatherService = weatherService;
            _waterService = waterService;
        }

        // GET: api/soildata
        [HttpGet]
        public IActionResult GetUserSoilData()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var records = _context.SoilDatas
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new
                {
                    s.Id,
                    s.Condition,
                    s.MoisturePercentage,
                    s.Temperature,
                    s.Humidity,
                    s.CreatedAt
                })
                .ToList();

            return Ok(records);
        }

        // POST: api/soildata/add
        [HttpPost("add")]
        public async Task<IActionResult> AddSoilData(SoilDataDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // ✅ Validation
            if (dto.FieldAreaM2.HasValue && dto.FieldAreaM2 <= 0)
            {
                return BadRequest("Field area must be greater than 0.");
            }

            // 🌦 Fetch weather
            var weather = await _weatherService.GetWeatherAsync("Lahore");

            // 📝 Save soil data in DB
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
