namespace AquaBotApi.Models.DTOs
{
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
