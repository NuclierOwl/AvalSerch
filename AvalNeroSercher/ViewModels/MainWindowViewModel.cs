using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvalNeroSercher.Models;
using AvalNeroSercher.Services;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using Emgu.CV;
using System.Threading.Tasks;
using System;
using Avalonia.Platform.Storage;
using Avalonia.Controls;

namespace AvalNeroSercher.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly CameraService _cameraService;
        private readonly Predictor _predictor;
        private bool _disposed;

        [ObservableProperty]
        private ObservableCollection<CameraInfo> _cameras = new();

        [ObservableProperty]
        private CameraInfo? _selectedCamera;

        [ObservableProperty]
        private bool _isCapturing;

        [ObservableProperty]
        private string _recognizedDigit = string.Empty;

        [ObservableProperty]
        private float _confidence;

        [ObservableProperty]
        private Bitmap? _currentFrame;

        // Путь к текущей модели
        [ObservableProperty]
        private string _modelPath = @"C:\DigitsModel.zip";

        public MainViewModel(CameraService cameraService, Predictor predictor)
        {
            _cameraService = cameraService;
            _predictor = predictor;

            _cameraService.NewFrame += OnNewFrame;

            Cameras = new ObservableCollection<CameraInfo>(_cameraService.GetAvailableCameras());
            if (Cameras.Any())
                SelectedCamera = Cameras[0];
        }

        private void OnNewFrame(Bitmap frame)
        {
            Mat mat = frame.ToMat();
            (string digit, float confidence) result;
            try
            {
                result = _predictor.Predict(mat);
            }
            catch
            {
                // Модель ещё не загружена или ошибка – просто пропускаем
                return;
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RecognizedDigit = result.digit;
                Confidence = result.confidence;
                CurrentFrame = frame;
            });
        }

        [RelayCommand]
        private void StartCapture()
        {
            if (SelectedCamera == null) return;
            _cameraService.StartCapture(SelectedCamera.Index);
            IsCapturing = true;
        }

        [RelayCommand]
        private void StopCapture()
        {
            _cameraService.StopCapture();
            IsCapturing = false;
            CurrentFrame = null;
        }

        partial void OnSelectedCameraChanged(CameraInfo? value)
        {
            if (IsCapturing && value != null)
                _cameraService.StartCapture(value.Index);
        }

        // Команда выбора файла модели
        [RelayCommand]
        private async Task BrowseModel()
        {
            // Получаем ссылку на текущее окно верхнего уровня
            var topLevel = TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null);

            if (topLevel == null) return;

            // Настраиваем фильтр для ZIP-файлов
            var fileTypes = new[] { new FilePickerFileType("ML.NET Model") { Patterns = new[] { "*.zip" } } };

            // Открываем диалог выбора файла
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выберите файл модели",
                AllowMultiple = false,
                FileTypeFilter = fileTypes
            });

            if (files.Count > 0)
            {
                // Получаем путь к выбранному файлу
                ModelPath = files[0].Path.LocalPath;
                LoadModelCommand.Execute(null);
            }
        }

        // Команда загрузки модели по указанному пути
        [RelayCommand]
        private void LoadModel()
        {
            try
            {
                _predictor.LoadModel(ModelPath);
                // Можно обновить статус или лог
            }
            catch (Exception ex)
            {
                // Обработка ошибки: показать диалог
            }
        }

        partial void OnModelPathChanged(string value)
        {
            // Автоматически загружать модель при изменении пути? Лучше оставить отдельную кнопку.
        }

        public void Cleanup()
        {
            if (_disposed) return;
            _disposed = true;
            _cameraService.NewFrame -= OnNewFrame;
            StopCapture();
            _cameraService.Dispose();
            _predictor.Dispose();
        }
    }
}