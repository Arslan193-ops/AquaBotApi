using System.ComponentModel.DataAnnotations;

namespace AquaBotApi.Models.DTOs
{

    public class SoilDataDto
    {
        [Required]
        [StringLength(50, ErrorMessage = "Condition length can't be more than 50.")]
        public string Condition { get; set; }

        [Range(0, 100, ErrorMessage = "Moisture percentage must be between 0 and 100.")]
        public int MoisturePercentage { get; set; }
    }

}
