using Microsoft.ML;
using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.IO;
using System.Linq;

namespace AvalNeroSercher.Services
{
    public class Predictor : IDisposable
    {
        private readonly object _lock = new();
        private PredictionEngine<ImageData, ModelOutput>? _engine;
        private readonly string _tempFile;

        public string? CurrentModelPath { get; private set; }

        public Predictor(string? initialModelPath = null)
        {
            _tempFile = Path.Combine(Path.GetTempPath(), "digit_frame.png");
            if (!string.IsNullOrWhiteSpace(initialModelPath) && File.Exists(initialModelPath))
                LoadModel(initialModelPath);
        }

        public bool IsModelLoaded
        {
            get
            {
                lock (_lock) return _engine != null;
            }
        }

        /// <summary>
        /// Загружает новую модель. Потокобезопасен.
        /// </summary>
        public void LoadModel(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Модель не найдена", modelPath);

            var ctx = new MLContext();
            var model = ctx.Model.Load(modelPath, out _);
            var newEngine = ctx.Model.CreatePredictionEngine<ImageData, ModelOutput>(model);

            lock (_lock)
            {
                _engine?.Dispose();
                _engine = newEngine;
                CurrentModelPath = modelPath;
            }
        }

        public (string Digit, float Confidence) Predict(Mat frame)
        {
            using var bitmap = frame.ToImage<Bgr, byte>().ToBitmap();
            return PredictFromBitmap(bitmap);
        }

        public (string Digit, float Confidence) PredictFromImagePath(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Изображение не найдено", imagePath);

            using var bitmap = new System.Drawing.Bitmap(imagePath);
            return PredictFromBitmap(bitmap);
        }

        public (string Digit, float Confidence) PredictFromBytes(byte[] imageBytes)
        {
            using var stream = new MemoryStream(imageBytes);
            using var bitmap = new System.Drawing.Bitmap(stream);
            return PredictFromBitmap(bitmap);
        }

        private (string Digit, float Confidence) PredictFromBitmap(System.Drawing.Bitmap bitmap)
        {
            PredictionEngine<ImageData, ModelOutput> engine;
            lock (_lock)
            {
                engine = _engine ?? throw new InvalidOperationException("Модель не загружена");
            }

            bitmap.Save(_tempFile, System.Drawing.Imaging.ImageFormat.Png);

            var input = new ImageData { ImagePath = _tempFile, Label = "" };
            var output = engine.Predict(input);

            float maxScore = output.Score.Max();
            return (output.PredictedLabel, maxScore);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _engine?.Dispose();
                _engine = null;
                CurrentModelPath = null;
            }
        }
    }

    // Вспомогательные классы должны быть те же, что при обучении
    public class ImageData
    {
        public string ImagePath { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class ModelOutput
    {
        public string ImagePath { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string PredictedLabel { get; set; } = string.Empty;
        public float[] Score { get; set; } = Array.Empty<float>();
    }
}
