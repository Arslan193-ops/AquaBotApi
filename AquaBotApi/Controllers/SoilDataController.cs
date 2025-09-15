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
        private readonly WaterCalculationService _waterCalculationService;

        public SoilDataController(AppDbContext context, WeatherService weatherService, WaterCalculationService waterCalculationService)
        {
            _context = context;
            _weatherService = weatherService;
            _waterCalculationService = waterCalculationService;
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

    }
}
