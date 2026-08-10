using AvalNeroSercher.Models;
using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace AvalNeroSercher.Services;

public class AppController : IDisposable
{
    private readonly CameraService _cameraService;
    private readonly Predictor _predictor;
    private readonly ModelManager _modelManager;
    private bool _disposed;

    public bool IsCapturing { get; private set; }
    public string RecognizedDigit { get; private set; } = string.Empty;
    public float Confidence { get; private set; }
    public Bitmap? CurrentFrame { get; private set; }
    public int? SelectedCameraIndex { get; private set; }
    public string? CurrentModelPath => _predictor.CurrentModelPath;
    public string? CurrentModelName =>
        CurrentModelPath == null ? null : Path.GetFileName(CurrentModelPath);

    public event Action? StateChanged;

    public AppController(CameraService cameraService, Predictor predictor, ModelManager modelManager)
    {
        _cameraService = cameraService;
        _predictor = predictor;
        _modelManager = modelManager;
        _cameraService.NewFrame += OnNewFrame;
    }

    public IReadOnlyList<CameraInfo> GetCameras() => _cameraService.GetAvailableCameras();

    public IReadOnlyList<ModelInfo> GetModels() =>
        _modelManager.RefreshModels(_predictor.CurrentModelPath);

    public void SelectCamera(int index)
    {
        SelectedCameraIndex = index;
        if (IsCapturing)
            _cameraService.StartCapture(index);

        NotifyStateChanged();
    }

    public void StartCapture(int? cameraIndex = null)
    {
        var index = cameraIndex ?? SelectedCameraIndex
            ?? throw new InvalidOperationException("Камера не выбрана");

        SelectedCameraIndex = index;
        _cameraService.StartCapture(index);
        IsCapturing = true;
        NotifyStateChanged();
    }

    public void StopCapture()
    {
        _cameraService.StopCapture();
        IsCapturing = false;
        CurrentFrame = null;
        NotifyStateChanged();
    }

    public void LoadModel(string modelPath)
    {
        _predictor.LoadModel(modelPath);
        NotifyStateChanged();
    }

    public void LoadModelByName(string modelName)
    {
        var model = _modelManager.FindByName(modelName)
            ?? throw new FileNotFoundException($"Модель '{modelName}' не найдена в {_modelManager.ModelsDirectory}");

        LoadModel(model.Path);
    }

    public (string Digit, float Confidence) PredictFromFile(string imagePath) =>
        _predictor.PredictFromImagePath(imagePath);

    public (string Digit, float Confidence) PredictFromBytes(byte[] imageBytes) =>
        _predictor.PredictFromBytes(imageBytes);

    public AppStatus GetStatus() => new()
    {
        IsCapturing = IsCapturing,
        RecognizedDigit = RecognizedDigit,
        Confidence = Confidence,
        SelectedCameraIndex = SelectedCameraIndex,
        CurrentModelName = CurrentModelName,
        CurrentModelPath = CurrentModelPath,
        IsModelLoaded = _predictor.IsModelLoaded,
        ModelsDirectory = _modelManager.ModelsDirectory
    };

    private void OnNewFrame(Bitmap frame)
    {
        Mat mat = frame.ToMat();
        try
        {
            var result = _predictor.Predict(mat);
            RecognizedDigit = result.Digit;
            Confidence = result.Confidence;
            CurrentFrame = frame;
            NotifyStateChanged();
        }
        catch
        {
            // Модель не загружена или ошибка распознавания
        }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cameraService.NewFrame -= OnNewFrame;
        StopCapture();
        _cameraService.Dispose();
        _predictor.Dispose();
    }
}

public class AppStatus
{
    public bool IsCapturing { get; set; }
    public string RecognizedDigit { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public int? SelectedCameraIndex { get; set; }
    public string? CurrentModelName { get; set; }
    public string? CurrentModelPath { get; set; }
    public bool IsModelLoaded { get; set; }
    public string ModelsDirectory { get; set; } = string.Empty;
}
