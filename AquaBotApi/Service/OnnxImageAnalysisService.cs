using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using AquaBotApi.Models.DTOs;

namespace AquaBotApi.Service
{
    public class OnnxImageAnalysisService
    {
        private readonly InferenceSession _session;
        private readonly ILogger<OnnxImageAnalysisService> _logger;
        private const int IMG_SIZE = 224;

        public OnnxImageAnalysisService(IConfiguration config, ILogger<OnnxImageAnalysisService> logger)
        {
            _logger = logger;
            var modelPath = config.GetValue<string>("SoilModel:OnnxPath")
                            ?? Path.Combine(AppContext.BaseDirectory, "model", "soil_model_v1.onnx");
            _session = new InferenceSession(modelPath);
            _logger.LogInformation("Loaded ONNX model: " + modelPath);
        }

        // Synchronous API (keeps it simple)
        public ImageAnalysisDto AnalyzeImage(IFormFile file, string? userCropType = null)
        {
            // Load image into OpenCV Mat
            using var ms = file.OpenReadStream();
            using var src = Mat.FromStream(ms, ImreadModes.Color);
            using var resized = new Mat();
            Cv2.Resize(src, resized, new OpenCvSharp.Size(IMG_SIZE, IMG_SIZE));
            using var rgb = new Mat();
            Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

            // Build NHWC tensor [1, H, W, C]
            var tensor = new DenseTensor<float>(new[] { 1, IMG_SIZE, IMG_SIZE, 3 });
            for (int y = 0; y < IMG_SIZE; y++)
                for (int x = 0; x < IMG_SIZE; x++)
                {
                    var px = rgb.At<Vec3b>(y, x);
                    tensor[0, y, x, 0] = px.Item0 / 255f;
                    tensor[0, y, x, 1] = px.Item1 / 255f;
                    tensor[0, y, x, 2] = px.Item2 / 255f;
                }

            var inputName = _session.InputMetadata.Keys.First();
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            using var results = _session.Run(inputs);
            var probs = results.First().AsEnumerable<float>().ToArray();

            var idx = ArgMax(probs);
            var labels = GetLabels(); // must match training mapping
            var predictedLabel = labels[idx];
            var confidence = Math.Round(probs[idx] * 100.0, 1);

            // Map label -> estimated moisture (simple mapping)
            var moisture = predictedLabel.ToLower() switch
            {
                "black soil" => 60,
                "cinder soil" => 40,
                "laterite soil" => 30,
                "peat soil" => 70,
                "yellow soil" => 50,
                _ => 50
            };

            return new ImageAnalysisDto
            {
                SoilCondition = predictedLabel,
                EstimatedMoisture = moisture,
                CropType = userCropType ?? "Unknown",
                CropHealth = "Unknown",
                Confidence = confidence,
                AnalyzedAt = DateTime.UtcNow,
                Recommendations = string.Empty
            };
        }

        private static int ArgMax(float[] arr)
        {
            int idx = 0; float max = arr[0];
            for (int i = 1; i < arr.Length; i++) if (arr[i] > max) { max = arr[i]; idx = i; }
            return idx;
        }

        private Dictionary<int, string> GetLabels()
        {
            // IMPORTANT: matches your training output (alphabetical order used by flow_from_directory)
            return new Dictionary<int, string>
            {
                {0, "Black Soil"},
                {1, "Cinder Soil"},
                {2, "Laterite Soil"},
                {3, "Peat Soil"},
                {4, "Yellow Soil"}
            };
        }
    }
}
