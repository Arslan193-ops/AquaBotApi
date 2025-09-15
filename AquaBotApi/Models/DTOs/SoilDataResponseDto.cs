namespace AquaBotApi.Models.DTOs
{
    public class SoilDataResponseDto
    {
        public int Id { get; set; }
        public string Condition { get; set; }
        public int MoisturePercentage { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
