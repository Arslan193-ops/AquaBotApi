using AquaBotApi.Services;
using System;

namespace AquaBotApi.Models.DTOs
{
    public class ImageAnalysisResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        // ✅ DB Reference
        public int ImageId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;

        // ✅ Analysis Results
        public ImageAnalysisDto ImageAnalysis { get; set; } = new();
        public string? FieldLocation { get; set; }

        // ✅ Water Recommendation
        public WaterRecommendationResult WaterRecommendation { get; set; } = new();

        public string? FarmerRecommendation => WaterRecommendation?.Recommendation;
        public string? DebugDetails => WaterRecommendation?.DebugDetails;
    }
}
