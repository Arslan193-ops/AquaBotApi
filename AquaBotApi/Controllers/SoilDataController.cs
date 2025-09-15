using AquaBotApi.Data;
using AquaBotApi.Models;
using AquaBotApi.Models.DTOs;
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

        public SoilDataController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/soildata
        [HttpPost]
        public async Task<IActionResult> AddSoilData(SoilDataDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // from JWT
            if (userId == null)
            {
                return Unauthorized("User ID not found in token.");
            }

            var soil = new SoilData
            {
                Condition = dto.Condition,
                MoisturePercentage = dto.MoisturePercentage,
                UserId = userId
            };

            _context.SoilDatas.Add(soil);
            await _context.SaveChangesAsync();

            var response = new SoilDataResponseDto
            {
                Id = soil.Id,
                Condition = soil.Condition,
                MoisturePercentage = soil.MoisturePercentage,
                CreatedAt = soil.CreatedAt
            };

            return CreatedAtAction(nameof(GetUserSoilData), new { id = soil.Id }, response);
        }

        // GET: api/soildata
        [HttpGet]
        public IActionResult GetUserSoilData()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized("User ID not found in token.");
            }

            var records = _context.SoilDatas
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();

            return Ok(records);
        }
    }
}
