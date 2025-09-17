using System.ComponentModel.DataAnnotations;

namespace AquaBotApi.Models.DTOs
{
    public class ImageUploadDto
    {
        [Required]
        public IFormFile Image { get; set; }

        public string? FieldLocation { get; set; } // GPS coordinates or field name
        public string? CropType { get; set; } // User can specify crop type
        public string? Notes { get; set; } // Additional notes from farmer
        public double? FieldArea { get; set; } // Field area in square meters
    }
}