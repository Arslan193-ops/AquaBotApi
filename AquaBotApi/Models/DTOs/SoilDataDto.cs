
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AquaBotApi.Models.DTOs
{
    public class SoilDataDto
    {
        [Required]
        [StringLength(50)]
        public string Condition { get; set; }

        [Range(0, 100)]
        public int MoisturePercentage { get; set; }

        [DefaultValue(null)]
        public double? FieldAreaM2 { get; set; }
    }
}