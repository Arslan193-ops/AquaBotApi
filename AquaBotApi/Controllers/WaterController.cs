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
        private readonly WaterCalculationService _waterService;

        public WaterController(AppDbContext context, WeatherService weatherService, WaterCalculationService waterService)
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

            // ✅ Manual validation for field area
            if (dto.FieldAreaM2.HasValue && dto.FieldAreaM2 <= 0)
            {
                return BadRequest("Field area must be greater than 0.");
            }

            // Fetch weather
            var weather = await _weatherService.GetWeatherAsync("Lahore");

            // Save soil + weather snapshot
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

            // Calculate recommendation
            var perSquare = _waterService.Calculate(
                soil.MoisturePercentage,
                soil.Temperature,
                soil.Humidity
            );

            double? total = null;
            if (dto.FieldAreaM2.HasValue)
            {
                total = Math.Round(perSquare * dto.FieldAreaM2.Value, 1);
            }

            return Ok(new
            {
                soil.Id,
                soil.Condition,
                soil.MoisturePercentage,
                soil.Temperature,
                soil.Humidity,
                soil.CreatedAt,
                WaterNeededPerSquareMeter = $"{perSquare} L/m²",
                FieldAreaM2 = dto.FieldAreaM2,
                TotalWaterNeeded = total.HasValue ? $"{total} L" : null
            });
        }

    }
}
