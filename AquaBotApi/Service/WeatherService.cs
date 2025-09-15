using AquaBotApi.Models.DTOs;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AquaBotApi.Services
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public WeatherService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["Weather:ApiKey"] ?? throw new InvalidOperationException("Weather:ApiKey configuration value is missing.");
        }

        public async Task<WeatherDto?> GetWeatherAsync(string city)
        {
            var url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={_apiKey}&units=metric";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            using var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = jsonDoc.RootElement;

            return new WeatherDto
            {
                City = root.GetProperty("name").GetString() ?? string.Empty,
                Condition = root.GetProperty("weather")[0].GetProperty("main").GetString() ?? string.Empty,
                Description = root.GetProperty("weather")[0].GetProperty("description").GetString() ?? string.Empty,
                Temperature = root.GetProperty("main").GetProperty("temp").GetDouble(),
                Humidity = root.GetProperty("main").GetProperty("humidity").GetInt32()
            };
        }

    }
}
