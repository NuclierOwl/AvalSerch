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
        private string _tempFile;

        public Predictor(string initialModelPath)
        {
            _tempFile = Path.Combine(Path.GetTempPath(), "digit_frame.png");
            LoadModel(initialModelPath);
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
            }
        }

        public (string Digit, float Confidence) Predict(Mat frame)
        {
            PredictionEngine<ImageData, ModelOutput> engine;
            lock (_lock)
            {
                engine = _engine ?? throw new InvalidOperationException("Модель не загружена");
            }

            using var bitmap = frame.ToImage<Emgu.CV.Structure.Bgr, byte>().ToBitmap();
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
            }
        }
    }

    // Вспомогательные классы должны быть те же, что при обучении
    public class ImageData
    {
        public string ImagePath { get; set; }
        public string Label { get; set; }
    }

    public class ModelOutput
    {
        public string ImagePath { get; set; }
        public string Label { get; set; }
        public string PredictedLabel { get; set; }
        public float[] Score { get; set; }
    }
}