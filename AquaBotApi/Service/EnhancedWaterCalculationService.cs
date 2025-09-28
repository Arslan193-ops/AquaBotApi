//// Services/EnhancedWaterCalculationService.cs
//using AquaBotApi.Models.DTOs;

//namespace AquaBotApi.Services
//{
//    public class EnhancedWaterCalculationService
//    {
//        private readonly WeatherService _weatherService;
//        private readonly ILogger<EnhancedWaterCalculationService> _logger;

//        public EnhancedWaterCalculationService(WeatherService weatherService, ILogger<EnhancedWaterCalculationService> logger)
//        {
//            _weatherService = weatherService;
//            _logger = logger;
//        }

//        /// <summary>
//        /// Calculate water needs combining image analysis + weather data
//        /// </summary>
//        public async Task<WaterRecommendationResult> CalculateFromImageAndWeatherAsync(
//            ImageAnalysisDto imageData,
//            string location = "Lahore",
//            string cropType = null,
//            double? fieldAreaM2 = null)
//        {
//            try
//            {
//                // Get current weather
//                var weather = await _weatherService.GetWeatherAsync(location);
//                if (weather == null)
//                {
//                    _logger.LogWarning($"Could not fetch weather for {location}, using defaults");
//                    weather = GetDefaultWeather();
//                }

//                // Calculate base water need from image + weather
//                var calculation = CalculateWaterNeed(imageData, weather, cropType);

//                // Calculate total for field if area provided
//                double? totalWaterNeeded = null;
//                if (fieldAreaM2.HasValue && fieldAreaM2 > 0)
//                {
//                    totalWaterNeeded = Math.Round(calculation.WaterPerSquareMeter * fieldAreaM2.Value, 1);
//                }

//                return new WaterRecommendationResult
//                {
//                    // Image Analysis Data
//                    SoilCondition = imageData.SoilCondition,
//                    EstimatedMoisture = imageData.EstimatedMoisture,
//                    CropHealth = imageData.CropHealth,
//                    ImageConfidence = imageData.Confidence,

//                    // Weather Data
//                    Temperature = weather.Temperature,
//                    Humidity = weather.Humidity,
//                    WeatherCondition = weather.Condition,
//                    Location = weather.City,

//                    // Calculations
//                    WaterPerSquareMeter = calculation.WaterPerSquareMeter,
//                    FieldAreaM2 = fieldAreaM2,
//                    TotalWaterNeeded = totalWaterNeeded,

//                    // Recommendations
//                    IrrigationUrgency = calculation.Urgency,
//                    Recommendation = calculation.Recommendation,
//                    NextCheckDate = calculation.NextCheckDate,

//                    // Technical Details
//                    CalculationDetails = calculation.Details,
//                    CalculatedAt = DateTime.UtcNow
//                };
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error calculating water needs from image and weather");
//                throw new InvalidOperationException("Failed to calculate water needs", ex);
//            }
//        }

//        /// <summary>
//        /// Core water calculation logic
//        /// </summary>
//        private WaterCalculation CalculateWaterNeed(ImageAnalysisDto imageData, WeatherDto weather, string cropType)
//        {
//            // 1. BASE WATER NEED from image analysis
//            var baseMoistureFactor = CalculateMoistureFactor(imageData.EstimatedMoisture, imageData.SoilCondition);

//            // 2. WEATHER FACTORS
//            var temperatureFactor = CalculateTemperatureFactor(weather.Temperature);
//            var humidityFactor = CalculateHumidityFactor(weather.Humidity);
//            var weatherConditionFactor = CalculateWeatherConditionFactor(weather.Condition);

//            // 3. CROP FACTORS
//            var cropFactor = CalculateCropFactor(cropType, imageData.CropHealth);

//            // 4. PAKISTANI CLIMATE ADJUSTMENTS
//            var climateAdjustment = CalculatePakistaniClimateAdjustment(weather.Temperature, weather.Humidity);

//            // 5. FINAL CALCULATION
//            var waterPerSquareMeter = baseMoistureFactor *
//                                    temperatureFactor *
//                                    humidityFactor *
//                                    weatherConditionFactor *
//                                    cropFactor *
//                                    climateAdjustment;

//            // Round to practical value
//            waterPerSquareMeter = Math.Round(Math.Max(0.5, Math.Min(50, waterPerSquareMeter)), 1);

//            // Determine urgency and recommendations
//            var urgency = DetermineIrrigationUrgency(imageData, weather, waterPerSquareMeter);
//            var recommendation = GenerateDetailedRecommendation(imageData, weather, waterPerSquareMeter, urgency);
//            var nextCheck = CalculateNextCheckDate(urgency, imageData.SoilCondition);

//            var details = new CalculationBreakdown
//            {
//                BaseMoistureFactor = baseMoistureFactor,
//                TemperatureFactor = temperatureFactor,
//                HumidityFactor = humidityFactor,
//                WeatherConditionFactor = weatherConditionFactor,
//                CropFactor = cropFactor,
//                ClimateAdjustment = climateAdjustment,
//                FinalWaterAmount = waterPerSquareMeter
//            };

//            return new WaterCalculation
//            {
//                WaterPerSquareMeter = waterPerSquareMeter,
//                Urgency = urgency,
//                Recommendation = recommendation,
//                NextCheckDate = nextCheck,
//                Details = details
//            };
//        }

//        #region Calculation Factors

//        /// <summary>
//        /// Calculate base water need from soil moisture (from image)
//        /// </summary>
//        private double CalculateMoistureFactor(int moisturePercentage, string soilCondition)
//        {
//            // Base water need inversely related to current moisture
//            var baseWater = Math.Max(0, 100 - moisturePercentage) * 0.2; // 0-20L base range

//            // Adjust based on visual condition assessment
//            var conditionMultiplier = soilCondition.ToLower() switch
//            {
//                "dry" => 1.5,      // Dry soil needs more water
//                "wet" => 0.3,      // Wet soil needs minimal water
//                "moist" => 0.8,    // Moist soil needs moderate water
//                _ => 1.0
//            };

//            return baseWater * conditionMultiplier;
//        }

//        /// <summary>
//        /// Temperature factor (Pakistani climate optimized)
//        /// </summary>
//        private double CalculateTemperatureFactor(double temperature)
//        {
//            // Optimal crop temperature for Pakistan: 20-30°C
//            if (temperature <= 15) return 0.7;  // Cold weather, less evaporation
//            if (temperature <= 25) return 1.0;  // Optimal range
//            if (temperature <= 35) return 1.4;  // Hot, more evaporation
//            if (temperature <= 45) return 1.8;  // Very hot Pakistani summer
//            return 2.0; // Extreme heat
//        }

//        /// <summary>
//        /// Humidity factor (lower humidity = more water loss)
//        /// </summary>
//        private double CalculateHumidityFactor(int humidity)
//        {
//            // Higher humidity = less water evaporation
//            if (humidity >= 80) return 0.8;  // High humidity, less evaporation
//            if (humidity >= 60) return 1.0;  // Normal
//            if (humidity >= 40) return 1.2;  // Low humidity, more evaporation
//            if (humidity >= 20) return 1.4;  // Very dry air
//            return 1.6; // Extremely dry conditions
//        }

//        /// <summary>
//        /// Weather condition adjustments
//        /// </summary>
//        private double CalculateWeatherConditionFactor(string condition)
//        {
//            return condition.ToLower() switch
//            {
//                "clear" or "sunny" => 1.3,        // Sunny = more evaporation
//                "clouds" or "cloudy" => 0.9,      // Cloudy = less evaporation
//                "rain" or "drizzle" => 0.2,       // Rain = minimal irrigation needed
//                "thunderstorm" => 0.1,            // Heavy rain = no irrigation
//                "mist" or "fog" => 0.8,           // High humidity conditions
//                "dust" or "sand" => 1.2,          // Dusty conditions (common in Pakistan)
//                _ => 1.0
//            };
//        }

//        /// <summary>
//        /// Crop-specific water requirements
//        /// </summary>
//        private double CalculateCropFactor(string cropType, string cropHealth)
//        {
//            // Base crop water coefficients (simplified)
//            var baseFactor = (cropType?.ToLower()) switch
//            {
//                "rice" => 1.8,           // High water requirement
//                "wheat" => 1.2,          // Moderate water requirement
//                "cotton" => 1.4,         // Moderate-high requirement
//                "sugarcane" => 2.0,      // Very high requirement
//                "maize" or "corn" => 1.3, // Moderate requirement
//                "vegetables" => 1.1,     // Lower requirement
//                "leafy crop" => 1.0,     // Default from image detection
//                _ => 1.2                 // Default for unknown crops
//            };

//            // Adjust based on crop health (from image analysis)
//            var healthMultiplier = cropHealth?.ToLower() switch
//            {
//                "healthy" => 1.0,        // Normal water needs
//                "moderate" => 1.1,       // Slightly more water
//                "stressed" => 1.3,       // More water for recovery
//                "poor" => 1.4,           // Maximum water for recovery
//                _ => 1.0
//            };

//            return baseFactor * healthMultiplier;
//        }

//        /// <summary>
//        /// Pakistani climate-specific adjustments
//        /// </summary>
//        private double CalculatePakistaniClimateAdjustment(double temperature, int humidity)
//        {
//            // Adjust for Pakistan's specific climate patterns

//            // Summer months adjustment (April-September)
//            var month = DateTime.Now.Month;
//            var seasonFactor = 1.0;

//            if (month >= 4 && month <= 9) // Hot season
//            {
//                seasonFactor = 1.2;
//            }
//            else if (month >= 11 || month <= 2) // Cool season
//            {
//                seasonFactor = 0.8;
//            }

//            // Extreme conditions common in Pakistan
//            if (temperature > 40 && humidity < 30)
//            {
//                seasonFactor *= 1.3; // Extreme hot & dry
//            }

//            return seasonFactor;
//        }

//        #endregion

//        #region Recommendations

//        /// <summary>
//        /// Determine irrigation urgency level
//        /// </summary>
//        private IrrigationUrgency DetermineIrrigationUrgency(ImageAnalysisDto imageData, WeatherDto weather, double waterAmount)
//        {
//            // Critical conditions
//            if (imageData.SoilCondition == "Dry" && imageData.EstimatedMoisture < 20 && weather.Temperature > 35)
//                return IrrigationUrgency.Critical;

//            if (imageData.SoilCondition == "Dry" && waterAmount > 15)
//                return IrrigationUrgency.High;

//            if (imageData.SoilCondition == "Moist" && waterAmount > 8)
//                return IrrigationUrgency.Medium;

//            if (imageData.SoilCondition == "Wet" || waterAmount < 3)
//                return IrrigationUrgency.Low;

//            return IrrigationUrgency.Medium;
//        }

//        /// <summary>
//        /// Generate detailed recommendations
//        /// </summary>
//        private string GenerateDetailedRecommendation(ImageAnalysisDto imageData, WeatherDto weather, double waterAmount, IrrigationUrgency urgency)
//        {
//            var recommendations = new List<string>();

//            // Urgency-based primary recommendation
//            switch (urgency)
//            {
//                case IrrigationUrgency.Critical:
//                    recommendations.Add($"🚨 URGENT: Apply {waterAmount}L/m² immediately");
//                    recommendations.Add("🌡️ Extreme conditions detected - irrigate now to prevent crop damage");
//                    break;

//                case IrrigationUrgency.High:
//                    recommendations.Add($"⚡ HIGH PRIORITY: Apply {waterAmount}L/m² within 2-4 hours");
//                    recommendations.Add("🔥 Hot weather increasing water demand");
//                    break;

//                case IrrigationUrgency.Medium:
//                    recommendations.Add($"💧 Apply {waterAmount}L/m² within 12-24 hours");
//                    recommendations.Add("📊 Based on soil moisture and weather analysis");
//                    break;

//                case IrrigationUrgency.Low:
//                    recommendations.Add($"✅ Soil moisture adequate, light irrigation: {waterAmount}L/m²");
//                    recommendations.Add("⏰ Can wait 1-2 days before next irrigation");
//                    break;
//            }

//            // Weather-specific advice
//            if (weather.Condition.ToLower().Contains("rain"))
//            {
//                recommendations.Add("🌧️ Rain expected - reduce or skip irrigation");
//            }
//            else if (weather.Temperature > 40)
//            {
//                recommendations.Add("🌡️ Extreme heat - consider early morning irrigation");
//            }

//            // Crop health advice
//            if (imageData.CropHealth == "Stressed" || imageData.CropHealth == "Poor")
//            {
//                recommendations.Add("🌱 Consider adding fertilizer with irrigation");
//                recommendations.Add("🔍 Monitor closely for pest/disease issues");
//            }

//            return string.Join(" | ", recommendations);
//        }

//        /// <summary>
//        /// Calculate next check date based on conditions
//        /// </summary>
//        private DateTime CalculateNextCheckDate(IrrigationUrgency urgency, string soilCondition)
//        {
//            var hoursToAdd = urgency switch
//            {
//                IrrigationUrgency.Critical => 12,  // Check in 12 hours
//                IrrigationUrgency.High => 24,     // Check in 1 day
//                IrrigationUrgency.Medium => 48,   // Check in 2 days
//                IrrigationUrgency.Low => 72,      // Check in 3 days
//                _ => 48
//            };

//            return DateTime.UtcNow.AddHours(hoursToAdd);
//        }

//        /// <summary>
//        /// Get default weather when API fails
//        /// </summary>
//        private WeatherDto GetDefaultWeather()
//        {
//            return new WeatherDto
//            {
//                City = "Lahore",
//                Condition = "Clear",
//                Description = "Default weather",
//                Temperature = 28, // Average Pakistani temperature
//                Humidity = 55     // Average humidity
//            };
//        }

//        #endregion
//    }

//    #region Supporting Classes

//    public enum IrrigationUrgency
//    {
//        Low,
//        Medium,
//        High,
//        Critical
//    }

//    public class WaterRecommendationResult
//    {
//        // Image Analysis Data
//        public string SoilCondition { get; set; } = string.Empty;
//        public int EstimatedMoisture { get; set; }
//        public string CropHealth { get; set; } = string.Empty;
//        public double ImageConfidence { get; set; }

//        // Weather Data
//        public double Temperature { get; set; }
//        public int Humidity { get; set; }
//        public string WeatherCondition { get; set; } = string.Empty;
//        public string Location { get; set; } = string.Empty;

//        // Calculation Results
//        public double WaterPerSquareMeter { get; set; }
//        public double? FieldAreaM2 { get; set; }
//        public double? TotalWaterNeeded { get; set; }

//        // Recommendations
//        public IrrigationUrgency IrrigationUrgency { get; set; }
//        public string Recommendation { get; set; } = string.Empty;
//        public DateTime NextCheckDate { get; set; }

//        // Technical Details
//        public CalculationBreakdown CalculationDetails { get; set; } = new();
//        public DateTime CalculatedAt { get; set; }
//    }

//    public class WaterCalculation
//    {
//        public double WaterPerSquareMeter { get; set; }
//        public IrrigationUrgency Urgency { get; set; }
//        public string Recommendation { get; set; } = string.Empty;
//        public DateTime NextCheckDate { get; set; }
//        public CalculationBreakdown Details { get; set; } = new();
//    }

//    public class CalculationBreakdown
//    {
//        public double BaseMoistureFactor { get; set; }
//        public double TemperatureFactor { get; set; }
//        public double HumidityFactor { get; set; }
//        public double WeatherConditionFactor { get; set; }
//        public double CropFactor { get; set; }
//        public double ClimateAdjustment { get; set; }
//        public double FinalWaterAmount { get; set; }
//    }

//    #endregion
//}




using AquaBotApi.Models.DTOs;

namespace AquaBotApi.Services
{
    public class EnhancedWaterCalculationService
    {
        private readonly WeatherService _weatherService;
        private readonly ILogger<EnhancedWaterCalculationService> _logger;

        public EnhancedWaterCalculationService(WeatherService weatherService, ILogger<EnhancedWaterCalculationService> logger)
        {
            _weatherService = weatherService;
            _logger = logger;
        }

        public async Task<WaterRecommendationResult> CalculateFromImageAndWeatherAsync(
            ImageAnalysisDto imageData,
            string location = "Lahore",
            string cropType = null,
            double? fieldAreaM2 = null)
        {
            // 1. Get weather
            var weather = await _weatherService.GetWeatherAsync(location) ?? GetDefaultWeather();

            // 2. Estimate water per m² with simple rules
            var waterNeed = EstimateWaterNeed(imageData, weather);

            // 3. Calculate total if field size given
            double? totalWater = null;
            if (fieldAreaM2.HasValue && fieldAreaM2 > 0)
            {
                totalWater = Math.Round(waterNeed * fieldAreaM2.Value, 1);
            }

            // 4. Build result
            return new WaterRecommendationResult
            {
                SoilCondition = imageData.SoilCondition,
                EstimatedMoisture = imageData.EstimatedMoisture,
                CropHealth = imageData.CropHealth,
                ImageConfidence = imageData.Confidence,

                Temperature = weather.Temperature,
                Humidity = weather.Humidity,
                WeatherCondition = weather.Condition,
                Location = weather.City,

                WaterPerSquareMeter = waterNeed,
                FieldAreaM2 = fieldAreaM2,
                TotalWaterNeeded = totalWater,

                IrrigationUrgency = DetermineUrgency(imageData, weather, waterNeed),
                Recommendation = GenerateSimpleRecommendation(waterNeed, fieldAreaM2),
                NextCheckDate = DateTime.UtcNow.AddHours(24), // Always recheck after 1 day
                CalculatedAt = DateTime.UtcNow
            };
        }

        // === Simplified Logic ===

        private double EstimateWaterNeed(ImageAnalysisDto image, WeatherDto weather)
        {
            // Start with inverse of moisture (0 = dry → 15L, 100 = wet → 2L)
            var baseWater = Math.Max(2, 15 - (image.EstimatedMoisture / 10.0));

            // Adjust for soil condition
            if (image.SoilCondition.Equals("Dry", StringComparison.OrdinalIgnoreCase))
                baseWater *= 1.2;
            if (image.SoilCondition.Equals("Wet", StringComparison.OrdinalIgnoreCase))
                baseWater *= 0.5;

            // Adjust for weather
            if (weather.Temperature > 35) baseWater *= 1.3;
            if (weather.Humidity < 30) baseWater *= 1.2;
            if (weather.Condition.ToLower().Contains("rain")) baseWater *= 0.2;

            return Math.Round(Math.Min(20, Math.Max(2, baseWater)), 1);
        }

        private IrrigationUrgency DetermineUrgency(ImageAnalysisDto image, WeatherDto weather, double waterNeed)
        {
            if (waterNeed > 12) return IrrigationUrgency.Critical;
            if (waterNeed > 8) return IrrigationUrgency.High;
            if (waterNeed > 4) return IrrigationUrgency.Medium;
            return IrrigationUrgency.Low;
        }

        private string GenerateSimpleRecommendation(double waterNeed, double? fieldArea)
        {
            if (fieldArea.HasValue)
                return $"Apply {waterNeed} L/m² (≈ {Math.Round(waterNeed * fieldArea.Value, 1)} L total) within 24 hours.";
            else
                return $"Apply {waterNeed} L/m² within 24 hours.";
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

    public enum IrrigationUrgency
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class WaterRecommendationResult
    {
        public string SoilCondition { get; set; } = string.Empty;
        public int EstimatedMoisture { get; set; }
        public string CropHealth { get; set; } = string.Empty;
        public double ImageConfidence { get; set; }

        public double Temperature { get; set; }
        public int Humidity { get; set; }
        public string WeatherCondition { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        public double WaterPerSquareMeter { get; set; }
        public double? FieldAreaM2 { get; set; }
        public double? TotalWaterNeeded { get; set; }

        public IrrigationUrgency IrrigationUrgency { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public DateTime NextCheckDate { get; set; }
        public DateTime CalculatedAt { get; set; }
    }
}
