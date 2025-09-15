namespace AquaBotApi.Models.DTOs
{
    public class WeatherDto
    {
        public string City { get; set; }
        public string Condition { get; set; }
        public string Description { get; set; }
        public double Temperature { get; set; }
        public int Humidity { get; set; }
    }

}
