using AquaBotApi.Models.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AquaBotApi.Services
{
    public class EnhancedWaterCalculationService
    {
        private readonly WeatherService _weatherService;
        private readonly ILogger<EnhancedWaterCalculationService> _logger;
        private readonly IConfiguration _config;

        // Configurable parameters (read from IConfiguration with defaults)
        private readonly double MinLPerM2;
        private readonly double MaxLPerM2;
        private readonly double RainReductionThresholdMm;
        private readonly double RecentRainStrongReductionMm;
        private readonly double RainProbabilityReductionFactor; // fraction to reduce based on forecast prob
        private readonly Dictionary<string, double> DefaultCropKc;
        private readonly Dictionary<string, double> DefaultRootDepthM;

        public EnhancedWaterCalculationService(
            WeatherService weatherService,
            ILogger<EnhancedWaterCalculationService> logger,
            IConfiguration config)
        {
            _weatherService = weatherService;
            _logger = logger;
            _config = config;

            // Load configurable constants (fallback to sensible defaults)
            MinLPerM2 = _config.GetValue<double?>("Irrigation:MinLPerM2") ?? 2.0;
            MaxLPerM2 = _config.GetValue<double?>("Irrigation:MaxLPerM2") ?? 20.0;
            RainReductionThresholdMm = _config.GetValue<double?>("Irrigation:RainReductionThresholdMm") ?? 2.0;
            RecentRainStrongReductionMm = _config.GetValue<double?>("Irrigation:RecentRainStrongReductionMm") ?? 8.0;
            RainProbabilityReductionFactor = _config.GetValue<double?>("Irrigation:RainProbabilityReductionFactor") ?? 0.8;

            // Default crop Kc values (can be overridden in configuration)
            DefaultCropKc = _config.GetSection("Irrigation:DefaultCropKc").Get<Dictionary<string, double>>()
                            ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["default"] = 1.0,
                                ["wheat"] = 0.9,
                                ["rice"] = 1.2,
                                ["maize"] = 1.05,
                                ["cotton"] = 1.0,
                                ["vegetable"] = 1.1
                            };

            // Default root depths (m)
            DefaultRootDepthM = _config.GetSection("Irrigation:DefaultRootDepthM").Get<Dictionary<string, double>>()
                            ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["default"] = 0.3,
                                ["wheat"] = 0.6,
                                ["rice"] = 0.2,
                                ["maize"] = 0.6,
                                ["cotton"] = 0.8,
                                ["vegetable"] = 0.3
                            };
        }

        public async Task<WaterRecommendationResult> CalculateFromImageAndWeatherAsync(
            ImageAnalysisDto imageData,
            string location = "Lahore",
            string? cropType = null,
            double? fieldAreaM2 = null)
        {
            // 1) Get weather (use provided weather service, else fallback defaults)
            var weather = await _weatherService.GetWeatherAsync(location) ?? GetDefaultWeather();

            // 2) Validate and sanitize inputs
            cropType = string.IsNullOrWhiteSpace(cropType) ? imageData?.CropType ?? "default" : cropType;
            var soilType = imageData?.SoilCondition ?? "Unknown";
            var moisture = Math.Clamp(imageData?.EstimatedMoisture ?? 50, 0, 100);

            // 3) Compute components
            var baseWater = ComputeBaseByMoisture(moisture); // L/m² base from moisture
            var soilRetention = GetSoilRetentionFactor(soilType); // soil type retention multiplier
            var cropKc = GetCropCoefficient(cropType); // crop coefficient
            var evapFactor = ComputeEvapotranspirationFactor(weather); // atm demand multiplier
            var weatherMultiplier = ComputeWeatherMultipliers(weather); // temperature/humidity/rain factors
            var rainAdjustmentFactor = AdjustForRecentAndForecastRain(weather);

            // Compose final water need per m²
            var raw = baseWater * soilRetention * cropKc * evapFactor * weatherMultiplier;
            var adjusted = raw * (1.0 - rainAdjustmentFactor); // reduce if rain expected / occurred

            // clamp to configured bounds
            var waterNeed = Math.Round(Math.Min(MaxLPerM2, Math.Max(MinLPerM2, adjusted)), 1);

            // 4) total water if area provided
            double? totalWater = null;
            if (fieldAreaM2.HasValue && fieldAreaM2 > 0)
                totalWater = Math.Round(waterNeed * fieldAreaM2.Value, 1);

            // 5) urgency and recommendation text (with breakdown)
            var urgency = DetermineUrgency(imageData, weather, waterNeed);
            var (farmerText, technicalText) = BuildRecommendationText(
    waterNeed,
    fieldAreaM2,
    cropType,
    soilType,
    moisture,
    baseWater,
    soilRetention,
    cropKc,
    evapFactor,
    weatherMultiplier,
    rainAdjustmentFactor,
    urgency);



            // Next check scheduling: urgency-based (Critical -> few hours, Low -> day or more)
            var nextCheck = CalculateNextCheckDate(urgency, weather);

            return new WaterRecommendationResult
            {
                SoilCondition = soilType,
                EstimatedMoisture = (int)Math.Round((double)moisture),
                CropHealth = imageData?.CropHealth ?? "Unknown",
                ImageConfidence = imageData?.Confidence ?? 0,

                Temperature = weather.Temperature,
                Humidity = weather.Humidity,
                WeatherCondition = weather.Condition,
                Location = weather.City,

                WaterPerSquareMeter = waterNeed,
                FieldAreaM2 = fieldAreaM2,
                TotalWaterNeeded = totalWater,

                IrrigationUrgency = urgency,
                Recommendation = farmerText,
                DebugDetails = technicalText,   // ✅ NEW
                NextCheckDate = nextCheck,
                CalculatedAt = DateTime.UtcNow
            };

        }

        // ----------------- Calculation helpers -----------------

        private double ComputeBaseByMoisture(double moisturePercent)
        {
            // Base mapping: 0% -> high water (15 L/m²), 100% -> low water (2 L/m²)
            // linear interpolation
            var baseFromMoisture = MinLPerM2 + (MaxLPerM2 - MinLPerM2) * (1.0 - (moisturePercent / 100.0));
            return Math.Round(Math.Max(MinLPerM2, Math.Min(MaxLPerM2, baseFromMoisture)), 2);
        }

        private double GetSoilRetentionFactor(string soilType)
        {
            var s = soilType?.ToLowerInvariant() ?? string.Empty;

            // Typical multipliers: peat -> retains water (reduce need), laterite -> fast draining (increase)
            if (s.Contains("peat")) return 0.6;
            if (s.Contains("black")) return 0.9;
            if (s.Contains("cinder")) return 1.05;
            if (s.Contains("laterite")) return 1.2;
            if (s.Contains("yellow")) return 1.0;
            // Unknown soil -> neutral
            return 1.0;
        }

        private double GetCropCoefficient(string cropType)
        {
            if (string.IsNullOrWhiteSpace(cropType)) cropType = "default";
            if (DefaultCropKc.TryGetValue(cropType, out var kc)) return kc;
            // try contains matching
            foreach (var kv in DefaultCropKc)
            {
                if (cropType.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) return kv.Value;
            }
            return DefaultCropKc["default"];
        }

        private double ComputeEvapotranspirationFactor(WeatherDto weather)
        {
            // Simple empirical evapotranspiration factor:
            // Start at 1.0 for moderate conditions (T ~ 20°C, Humidity ~50%)
            // Increase with higher temperature, decrease with higher humidity.
            // Factor = 1 + 0.05*(T - 20) + 0.01*(30 - Humidity)
            // Clamp to [0.5, 2.0]
            var t = weather?.Temperature ?? 20.0;
            var h = weather?.Humidity ?? 50;
            var factor = 1.0 + 0.05 * (t - 20.0) + 0.01 * (30.0 - h);
            factor = Math.Round(Math.Max(0.5, Math.Min(2.0, factor)), 3);
            return factor;
        }

        private double ComputeWeatherMultipliers(WeatherDto weather)
        {
            // Additional multipliers from extreme heat / low humidity / rain mention
            double mult = 1.0;
            if (weather == null) return mult;

            if (weather.Temperature > 35) mult *= 1.15; // hot -> increase
            if (weather.Humidity < 30) mult *= 1.1;     // dry air -> increase slightly

            // If weather description clearly contains rain now, reduce (most rain happened already)
            var cond = weather.Condition?.ToLowerInvariant() ?? string.Empty;
            if (cond.Contains("heavy rain") || cond.Contains("downpour")) mult *= 0.25;
            else if (cond.Contains("rain")) mult *= 0.6;

            return Math.Round(mult, 3);
        }

        private double AdjustForRecentAndForecastRain(WeatherDto weather)
        {
            // Returns a fraction [0..0.9] to reduce irrigation due to rain.
            // Uses optional fields if available on WeatherDto:
            // - PrecipitationLast24h (mm)
            // - PrecipitationProbability (0-100)
            // If missing, we fall back to presence of "rain" in Condition only.
            double reduction = 0.0;

            // recent precipitation (if property exists)
            var precipLast24hProp = weather?.GetType().GetProperty("PrecipitationLast24h");
            if (precipLast24hProp != null)
            {
                var val = precipLast24hProp.GetValue(weather);
                if (val is double mm)
                {
                    if (mm >= RecentRainStrongReductionMm) return 0.9; // heavy rain recently -> skip most irrigation
                    if (mm >= RainReductionThresholdMm) reduction = Math.Max(reduction, 0.5);
                }
            }

            // precipitation probability in forecast (if exists)
            var precipProbProp = weather?.GetType().GetProperty("PrecipitationProbability");
            if (precipProbProp != null)
            {
                var val = precipProbProp.GetValue(weather);
                if (val is double prob)
                {
                    // prob is 0..100
                    var p = Math.Clamp(prob / 100.0, 0.0, 1.0);
                    // reduce proportionally but never fully cancel out (cap by RainProbabilityReductionFactor)
                    reduction = Math.Max(reduction, p * RainProbabilityReductionFactor);
                }
            }

            // fallback: if Condition mentions rain, apply moderate reduction
            var cond = weather?.Condition?.ToLowerInvariant() ?? string.Empty;
            if (cond.Contains("rain") && reduction < 0.35) reduction = 0.35;

            // clamp
            reduction = Math.Round(Math.Max(0.0, Math.Min(0.9, reduction)), 3);
            return reduction;
        }

        private IrrigationUrgency DetermineUrgency(ImageAnalysisDto image, WeatherDto weather, double waterNeed)
        {
            // Slightly tuned thresholds to include evap and crop factors
            if (waterNeed >= 15) return IrrigationUrgency.Critical;
            if (waterNeed >= 10) return IrrigationUrgency.High;
            if (waterNeed >= 5) return IrrigationUrgency.Medium;
            return IrrigationUrgency.Low;
        }

        private DateTime CalculateNextCheckDate(IrrigationUrgency urgency, WeatherDto weather)
        {
            var now = DateTime.UtcNow;
            return urgency switch
            {
                IrrigationUrgency.Critical => now.AddHours(6),
                IrrigationUrgency.High => now.AddHours(12),
                IrrigationUrgency.Medium => now.AddHours(24),
                _ => now.AddHours(36)
            };
        }

        private (string FarmerText, string TechnicalText) BuildRecommendationText(
    double waterNeed,
    double? fieldArea,
    string cropType,
    string soilType,
    double moisture,
    double baseWater,
    double soilRetention,
    double cropKc,
    double evapFactor,
    double weatherMultiplier,
    double rainAdjustment,
    IrrigationUrgency urgency)
        {
            // Farmer-friendly message
            var totalText = fieldArea.HasValue
                ? $"{Math.Round(waterNeed * fieldArea.Value, 1)} L total (~{waterNeed} L/m²)"
                : $"{waterNeed} L/m²";

            var urgencyWindow = urgency switch
            {
                IrrigationUrgency.Critical => "within 6 hours",
                IrrigationUrgency.High => "within 12 hours",
                IrrigationUrgency.Medium => "within 24 hours",
                _ => "within 36 hours"
            };

            var farmerText = $"For {cropType} on {soilType} soil: Apply {totalText} {urgencyWindow} (urgency: {urgency}).";

            // Technical details (for experts / debugging)
            var technicalText = $"Breakdown → base={baseWater}, soilFactor={soilRetention}, cropKc={cropKc}, evap={evapFactor}, weatherMult={weatherMultiplier}, rainAdj={rainAdjustment}. Moisture={moisture}%.";

            return (farmerText, technicalText);
        }


        private WeatherDto GetDefaultWeather()
        {
            return new WeatherDto
            {
                City = "Lahore",
                Condition = "Clear",
                Description = "Default weather",
                Temperature = 28,
                Humidity = 55
            };
        }
    }
}
