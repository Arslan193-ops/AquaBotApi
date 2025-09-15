namespace AquaBotApi.Services
{
    public class WaterCalculationService
    {
        /// <summary>
        /// Estimate water need (liters per m²).
        /// Factors: soil moisture, temperature, humidity.
        /// </summary>
        public double Calculate(int moisture, double? temperature, int? humidity)
        {
            if (temperature == null || humidity == null) return 0;

            // 🌱 Base demand: how far from 100% moisture
            double deficit = (100 - moisture);

            // ☀️ Temp factor: hotter weather increases evapotranspiration
            // Assume normal crop comfort ~25°C
            double tempFactor = 1 + ((temperature.Value - 25) * 0.05);
            if (tempFactor < 0.5) tempFactor = 0.5; // minimum effect

            // 💨 Humidity factor: drier air = more water loss
            // At 100% humidity -> 0 extra loss; at 0% humidity -> 50% extra
            double humidityFactor = 1 + ((100 - humidity.Value) * 0.005);

            // Final formula: deficit × temp × humidity
            double waterNeeded = deficit * tempFactor * humidityFactor;

            // Round to 1 decimal for readability
            return Math.Round(waterNeeded, 1);
        }
    }
}
