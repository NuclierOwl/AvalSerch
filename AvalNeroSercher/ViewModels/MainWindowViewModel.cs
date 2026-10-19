using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvalNeroSercher.Models;
using AvalNeroSercher.Services;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System;
using Avalonia.Platform.Storage;
using Avalonia.Controls;

namespace AvalNeroSercher.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AppController _controller;
        private readonly ApiHostService _apiHost;
        private readonly LocalInteropService _localInterop;
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

        [ObservableProperty]
        private ObservableCollection<ModelInfo> _models = new();

        [ObservableProperty]
        private ModelInfo? _selectedModel;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _apiUrl = string.Empty;

        [ObservableProperty]
        private string _pipePath = string.Empty;

        [ObservableProperty]
        private string _bridgePath = string.Empty;

        public MainViewModel(AppController controller, ApiHostService apiHost, LocalInteropService localInterop)
        {
            _controller = controller;
            _apiHost = apiHost;
            _localInterop = localInterop;

            _controller.StateChanged += OnControllerStateChanged;

            Cameras = new ObservableCollection<CameraInfo>(_controller.GetCameras());
            if (Cameras.Any())
            {
                SelectedCamera = Cameras[0];
                _controller.SelectCamera(SelectedCamera.Index);
            }

            RefreshModels();

            var port = int.TryParse(Environment.GetEnvironmentVariable("AVALNERO_API_PORT"), out var p) ? p : 5050;
            var listenHost = Environment.GetEnvironmentVariable("AVALNERO_API_HOST") ?? "127.0.0.1";
            _apiHost.Start(port);
            _localInterop.Start();

            ApiUrl = $"http://{(listenHost is "0.0.0.0" or "*" ? "127.0.0.1" : listenHost)}:{port}";
            PipePath = _localInterop.PipePath;
            BridgePath = _localInterop.BridgeDirectory;
            StatusMessage = "Локальные каналы связи запущены";

            TryLoadFirstModel();
        }

        private void TryLoadFirstModel()
        {
            if (Models.Count == 0)
            {
                StatusMessage = "Поместите *.zip модели в папку Ml_Model";
                return;
            }

            SelectedModel = Models[0];
            LoadSelectedModel();
        }

        private void OnControllerStateChanged()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(SyncFromController);
        }

        private void SyncFromController()
        {
            IsCapturing = _controller.IsCapturing;
            RecognizedDigit = _controller.RecognizedDigit;
            Confidence = _controller.Confidence;
            CurrentFrame = _controller.CurrentFrame;
        }

        [RelayCommand]
        private void RefreshModels()
        {
            var models = _controller.GetModels();
            Models = new ObservableCollection<ModelInfo>(models);

            if (SelectedModel != null)
                SelectedModel = Models.FirstOrDefault(m => m.Path == SelectedModel.Path) ?? SelectedModel;
            else if (_controller.CurrentModelPath != null)
                SelectedModel = Models.FirstOrDefault(m => m.IsLoaded);
        }

        [RelayCommand]
        private void LoadSelectedModel()
        {
            if (SelectedModel == null)
            {
                StatusMessage = "Выберите модель";
                return;
            }

            try
            {
                _controller.LoadModel(SelectedModel.Path);
                StatusMessage = $"Модель загружена: {SelectedModel.Name}";
                RefreshModels();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка загрузки модели: {ex.Message}";
            }
        }

        [RelayCommand]
        private void StartCapture()
        {
            if (SelectedCamera == null) return;

            try
            {
                _controller.StartCapture(SelectedCamera.Index);
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void StopCapture()
        {
            _controller.StopCapture();
        }

        partial void OnSelectedCameraChanged(CameraInfo? value)
        {
            if (value != null)
                _controller.SelectCamera(value.Index);
        }

        [RelayCommand]
        private async Task BrowseModel()
        {
            var topLevel = TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null);

            if (topLevel == null) return;

            var fileTypes = new[] { new FilePickerFileType("ML.NET Model") { Patterns = new[] { "*.zip" } } };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выберите файл модели",
                AllowMultiple = false,
                FileTypeFilter = fileTypes
            });

            if (files.Count == 0)
                return;

            try
            {
                _controller.LoadModel(files[0].Path.LocalPath);
                StatusMessage = "Внешняя модель загружена";
                RefreshModels();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
            }
        }

        public void Cleanup()
        {
            if (_disposed) return;
            _disposed = true;
            _controller.StateChanged -= OnControllerStateChanged;
            _controller.Dispose();
            _apiHost.Dispose();
            _localInterop.Dispose();
        }
    }
}
