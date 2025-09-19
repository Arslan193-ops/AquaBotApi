using AquaBotApi.Models;
using AquaBotApi.Models.DTOs;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AquaBotApi.Services
{
    public class ImageAnalysisService
    {
        private readonly ILogger<ImageAnalysisService> _logger;

        public ImageAnalysisService(ILogger<ImageAnalysisService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Analyze soil/crop image and return condition assessment
        /// </summary>
        public async Task<ImageAnalysisDto> AnalyzeImageAsync(IFormFile imageFile, string? userCropType = null)
        {
            try
            {
                // ✅ Load with ImageSharp (cross-platform)
                using var image = await Image.LoadAsync<Rgb24>(imageFile.OpenReadStream());

                // Convert ImageSharp → OpenCV Mat
                using var mat = ImageToMat(image);

                // Perform analysis
                var colorAnalysis = AnalyzeColors(mat);
                var soilCondition = DetermineSoilCondition(colorAnalysis);
                var cropHealth = DetermineCropHealth(colorAnalysis);

                var result = new ImageAnalysisDto
                {
                    SoilCondition = soilCondition.Condition,
                    EstimatedMoisture = soilCondition.Moisture,
                    CropHealth = cropHealth.Health,
                    CropType = userCropType ?? DetectCropType(colorAnalysis),
                    Confidence = CalculateConfidence(colorAnalysis),
                    AnalyzedAt = DateTime.UtcNow,
                    Recommendations = GenerateRecommendations(soilCondition, cropHealth)
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing image");
                throw new InvalidOperationException("Failed to analyze image", ex);
            }
        }

        // ✅ ImageSharp → OpenCvSharp.Mat
        private Mat ImageToMat(Image<Rgb24> image)
        {
            var mat = new Mat(image.Height, image.Width, MatType.CV_8UC3);
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < image.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < image.Width; x++)
                    {
                        var pixel = row[x];
                        mat.Set(y, x, new Vec3b(pixel.B, pixel.G, pixel.R)); // OpenCV = BGR
                    }
                }
            });
            return mat;
        }

        /// <summary>
        /// Analyze color composition of the image
        /// </summary>
        private ColorAnalysis AnalyzeColors(Mat image)
        {
            using var hsv = new Mat();
            Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);

            var analysis = new ColorAnalysis();

            // Average brightness
            var channels = hsv.Split();
            using var vChannel = channels[2];
            analysis.AvgBrightness = Cv2.Mean(vChannel).Val0;

            // Percentages
            analysis.GreenPercentage = CalculateGreenPercentage(hsv);
            analysis.BrownPercentage = CalculateBrownPercentage(hsv);
            analysis.DarkSoilPercentage = CalculateDarkSoilPercentage(hsv);

            foreach (var channel in channels) channel.Dispose();

            return analysis;
        }

        private double CalculateGreenPercentage(Mat hsvImage)
        {
            var lowerGreen = new Scalar(40, 40, 40);
            var upperGreen = new Scalar(80, 255, 255);

            using var mask = new Mat();
            Cv2.InRange(hsvImage, lowerGreen, upperGreen, mask);

            var greenPixels = Cv2.CountNonZero(mask);
            var totalPixels = hsvImage.Width * hsvImage.Height;

            return (double)greenPixels / totalPixels * 100;
        }

        private double CalculateBrownPercentage(Mat hsvImage)
        {
            var lowerBrown = new Scalar(10, 30, 20);
            var upperBrown = new Scalar(25, 255, 200);

            using var mask = new Mat();
            Cv2.InRange(hsvImage, lowerBrown, upperBrown, mask);

            var brownPixels = Cv2.CountNonZero(mask);
            var totalPixels = hsvImage.Width * hsvImage.Height;

            return (double)brownPixels / totalPixels * 100;
        }

        private double CalculateDarkSoilPercentage(Mat hsvImage)
        {
            var lowerDark = new Scalar(0, 0, 0);
            var upperDark = new Scalar(179, 255, 80);

            using var mask = new Mat();
            Cv2.InRange(hsvImage, lowerDark, upperDark, mask);

            var darkPixels = Cv2.CountNonZero(mask);
            var totalPixels = hsvImage.Width * hsvImage.Height;

            return (double)darkPixels / totalPixels * 100;
        }

        private (string Condition, int Moisture) DetermineSoilCondition(ColorAnalysis analysis)
        {
            var moisture = 0;
            var condition = "Unknown";

            if (analysis.DarkSoilPercentage > 60 && analysis.AvgBrightness < 50)
            {
                condition = "Wet";
                moisture = 80 + (int)((100 - analysis.AvgBrightness) / 2);
            }
            else if (analysis.DarkSoilPercentage > 30 && analysis.AvgBrightness < 100)
            {
                condition = "Moist";
                moisture = 40 + (int)((80 - analysis.AvgBrightness) / 2);
            }
            else if (analysis.BrownPercentage > 40 && analysis.AvgBrightness > 80)
            {
                condition = "Dry";
                moisture = 10 + (int)((150 - analysis.AvgBrightness) / 3);
            }
            else
            {
                condition = "Moderate";
                moisture = (int)(50 + (80 - analysis.AvgBrightness) / 3);
            }

            moisture = Math.Max(0, Math.Min(100, moisture));
            return (condition, moisture);
        }

        private (string Health, double Confidence) DetermineCropHealth(ColorAnalysis analysis)
        {
            if (analysis.GreenPercentage > 60)
                return ("Healthy", 85.0);
            else if (analysis.GreenPercentage > 30)
                return ("Moderate", 70.0);
            else if (analysis.GreenPercentage > 10)
                return ("Stressed", 60.0);
            else if (analysis.GreenPercentage > 2)
                return ("Poor", 50.0);
            else
                return ("No Crop Detected", 40.0);
        }

        private string DetectCropType(ColorAnalysis analysis)
        {
            if (analysis.GreenPercentage > 50)
                return "Leafy Crop";
            else if (analysis.GreenPercentage > 20)
                return "Sparse Crop";
            else
                return "Soil/Fallow";
        }

        private double CalculateConfidence(ColorAnalysis analysis)
        {
            var confidence = 60.0;
            if (analysis.GreenPercentage > 30 || analysis.BrownPercentage > 40)
                confidence += 20;

            if (analysis.DarkSoilPercentage > 20 && analysis.DarkSoilPercentage < 80)
                confidence += 15;

            return Math.Min(95.0, confidence);
        }

        private string GenerateRecommendations(
            (string Condition, int Moisture) soil,
            (string Health, double Confidence) crop)
        {
            var recommendations = new List<string>();

            if (soil.Condition == "Dry" || soil.Moisture < 30)
            {
                recommendations.Add("🚿 Immediate irrigation recommended");
                recommendations.Add("💧 Apply 15-20L per square meter");
            }
            else if (soil.Condition == "Wet" || soil.Moisture > 80)
            {
                recommendations.Add("⏸️ Stop irrigation temporarily");
                recommendations.Add("🌬️ Ensure proper drainage");
            }
            else if (soil.Condition == "Moist")
            {
                recommendations.Add("✅ Soil moisture is optimal");
                recommendations.Add("📅 Check again in 2-3 days");
            }

            if (crop.Health == "Stressed" || crop.Health == "Poor")
            {
                recommendations.Add("🌱 Consider fertilizer application");
                recommendations.Add("🔍 Check for pests or diseases");
            }
            else if (crop.Health == "Healthy")
            {
                recommendations.Add("🌟 Crop looks healthy!");
                recommendations.Add("📈 Maintain current care routine");
            }

            return string.Join(" | ", recommendations);
        }
    }

    internal class ColorAnalysis
    {
        public double AvgBrightness { get; set; }
        public double GreenPercentage { get; set; }
        public double BrownPercentage { get; set; }
        public double DarkSoilPercentage { get; set; }
    }
}
