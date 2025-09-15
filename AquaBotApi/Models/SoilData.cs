using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AquaBotApi.Models
{
    public class SoilData
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Condition { get; set; }  // e.g., "Dry", "Moist", "Wet"

        [Range(0, 100)]
        public int MoisturePercentage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🔑 User linking (foreign key)
        [Required]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }
    }
}
