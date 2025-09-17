// Services/ImageAnalysisService.cs
using AquaBotApi.Models;
using AquaBotApi.Models.DTOs;
using OpenCvSharp;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

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
                using var stream = imageFile.OpenReadStream();
                using var bitmap = new Bitmap(stream);

                // Convert to OpenCV Mat
                var mat = BitmapToMat(bitmap);

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

                mat.Dispose();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing image");
                throw new InvalidOperationException("Failed to analyze image", ex);
            }
        }

        /// <summary>
        /// Analyze color composition of the image
        /// </summary>
        private ColorAnalysis AnalyzeColors(Mat image)
        {
            // Convert to HSV for better color analysis
            using var hsv = new Mat();
            Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);

            var analysis = new ColorAnalysis();

            // Calculate average brightness (V channel in HSV)
            var channels = hsv.Split();
            using var vChannel = channels[2]; // Value channel
            analysis.AvgBrightness = Cv2.Mean(vChannel).Val0;

            // Analyze color ranges
            analysis.GreenPercentage = CalculateGreenPercentage(hsv);
            analysis.BrownPercentage = CalculateBrownPercentage(hsv);
            analysis.DarkSoilPercentage = CalculateDarkSoilPercentage(hsv);

            // Dispose channels
            foreach (var channel in channels)
                channel.Dispose();

            return analysis;
        }

        /// <summary>
        /// Calculate percentage of green pixels (vegetation)
        /// </summary>
        private double CalculateGreenPercentage(Mat hsvImage)
        {
            // Green hue range: 40-80 (in OpenCV HSV: 0-179)
            var lowerGreen = new Scalar(40, 40, 40);
            var upperGreen = new Scalar(80, 255, 255);

            using var mask = new Mat();
            Cv2.InRange(hsvImage, lowerGreen, upperGreen, mask);

            var greenPixels = Cv2.CountNonZero(mask);
            var totalPixels = hsvImage.Width * hsvImage.Height;

            return (double)greenPixels / totalPixels * 100;
        }

        /// <summary>
        /// Calculate percentage of brown/soil colored pixels
        /// </summary>
        private double CalculateBrownPercentage(Mat hsvImage)
        {
            // Brown hue range: 10-20 (soil colors)
            var lowerBrown = new Scalar(10, 30, 20);
            var upperBrown = new Scalar(25, 255, 200);

            using var mask = new Mat();
            Cv2.InRange(hsvImage, lowerBrown, upperBrown, mask);

            var brownPixels = Cv2.CountNonZero(mask);
            var totalPixels = hsvImage.Width * hsvImage.Height;

            return (double)brownPixels / totalPixels * 100;
        }

        /// <summary>
        /// Calculate percentage of dark soil (potentially moist)
        /// </summary>
        private double CalculateDarkSoilPercentage(Mat hsvImage)
        {
            // Dark soil: low brightness values
            var lowerDark = new Scalar(0, 0, 0);
            var upperDark = new Scalar(179, 255, 80); // Low V value = dark

            using var mask = new Mat();
            Cv2.InRange(hsvImage, lowerDark, upperDark, mask);

            var darkPixels = Cv2.CountNonZero(mask);
            var totalPixels = hsvImage.Width * hsvImage.Height;

            return (double)darkPixels / totalPixels * 100;
        }

        /// <summary>
        /// Determine soil condition based on color analysis
        /// </summary>
        private (string Condition, int Moisture) DetermineSoilCondition(ColorAnalysis analysis)
        {
            var moisture = 0;
            var condition = "Unknown";

            // Logic: Darker soil = more moisture, lighter = drier
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

            // Clamp moisture between 0-100
            moisture = Math.Max(0, Math.Min(100, moisture));

            return (condition, moisture);
        }

        /// <summary>
        /// Determine crop health based on green vegetation analysis
        /// </summary>
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

        /// <summary>
        /// Simple crop type detection based on green patterns
        /// </summary>
        private string DetectCropType(ColorAnalysis analysis)
        {
            // This is a simplified detection - in reality you'd need more sophisticated analysis
            if (analysis.GreenPercentage > 50)
                return "Leafy Crop";
            else if (analysis.GreenPercentage > 20)
                return "Sparse Crop";
            else
                return "Soil/Fallow";
        }

        /// <summary>
        /// Calculate overall confidence in analysis
        /// </summary>
        private double CalculateConfidence(ColorAnalysis analysis)
        {
            // Higher confidence when we have clear color patterns
            var confidence = 60.0; // Base confidence

            if (analysis.GreenPercentage > 30 || analysis.BrownPercentage > 40)
                confidence += 20;

            if (analysis.DarkSoilPercentage > 20 && analysis.DarkSoilPercentage < 80)
                confidence += 15;

            return Math.Min(95.0, confidence);
        }

        /// <summary>
        /// Generate recommendations based on analysis
        /// </summary>
        private string GenerateRecommendations(
            (string Condition, int Moisture) soil,
            (string Health, double Confidence) crop)
        {
            var recommendations = new List<string>();

            // Soil-based recommendations
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

            // Crop-based recommendations
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

        /// <summary>
        /// Convert System.Drawing.Bitmap to OpenCvSharp.Mat
        /// </summary>
        [SupportedOSPlatform("windows6.1")]
        private Mat BitmapToMat(Bitmap bitmap)
        {
            // Use Mat.FromPixelData to avoid obsolete constructor and platform-specific BitmapData.Scan0
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                // Use FromPixelData instead of the obsolete constructor
                return Mat.FromPixelData(
                    bitmap.Height,
                    bitmap.Width,
                    MatType.CV_8UC3,
                    bmpData.Scan0
                );
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }
        }
    }

    /// <summary>
    /// Internal class for color analysis results
    /// </summary>
    internal class ColorAnalysis
    {
        public double AvgBrightness { get; set; }
        public double GreenPercentage { get; set; }
        public double BrownPercentage { get; set; }
        public double DarkSoilPercentage { get; set; }
    }
}