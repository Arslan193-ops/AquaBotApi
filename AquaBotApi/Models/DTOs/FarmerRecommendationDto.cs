namespace AquaBotApi.Models.DTOs
{
    public class FarmerRecommendationDto
    {
        public string SoilCondition { get; set; } = string.Empty;
        public string CropType { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public string Urgency { get; set; } = string.Empty; // e.g., "Low", "Medium", "High"
        public double WaterPerSquareMeter { get; set; }
        public double? TotalWaterNeeded { get; set; }
    }
}

