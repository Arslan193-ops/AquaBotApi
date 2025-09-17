// Models/ImageAnalysisResult.cs
using System.ComponentModel.DataAnnotations;

namespace AquaBotApi.Models
{
    public class ImageAnalysisResult
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        public string FileName { get; set; } = string.Empty;

        // Soil Analysis Results
        public string SoilCondition { get; set; } = string.Empty; // "Dry", "Moist", "Wet"
        public int EstimatedMoisture { get; set; } // 0-100%

        // Crop Analysis Results (if crop detected)
        public string CropHealth { get; set; } = string.Empty; // "Healthy", "Stressed", "Poor"
        public string CropType { get; set; } = string.Empty; // "Unknown", "Wheat", "Rice", etc.

        // Technical Data
        public double AvgBrightness { get; set; }
        public double GreenPercentage { get; set; }
        public double BrownPercentage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ApplicationUser User { get; set; }
    }
}

// Models/DTOs/ImageAnalysisDto.cs
namespace AquaBotApi.Models.DTOs
{
    public class ImageAnalysisDto
    {
        public int Id { get; set; }
        public string SoilCondition { get; set; } = string.Empty;
        public int EstimatedMoisture { get; set; }
        public string CropHealth { get; set; } = string.Empty;
        public string CropType { get; set; } = string.Empty;
        public double Confidence { get; set; } // 0-100%
        public DateTime AnalyzedAt { get; set; }
        public string Recommendations { get; set; } = string.Empty;
    }
}

