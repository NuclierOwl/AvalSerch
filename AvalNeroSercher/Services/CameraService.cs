using AvalNeroSercher.Models;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace AvalNeroSercher.Services
{
    public class CameraService : IDisposable
    {
        private VideoCapture? _capture;
        private CancellationTokenSource? _cts;
        public event Action<Bitmap>? NewFrame;

        public List<CameraInfo> GetAvailableCameras()
        {
            var cameras = new List<CameraInfo>();
            try
            {
                for (int i = 0; i < 10; i++) // проверяем первые 10 индексов
                {
                    try
                    {
                        using var cap = new VideoCapture(i);
                        if (cap.IsOpened)
                            cameras.Add(new CameraInfo { Index = i, Name = $"Камера {i}" });
                    }
                    catch
                    {
                        // индекс недоступен — пропускаем
                    }
                }
            }
            catch (DllNotFoundException)
            {
                // Нет нативных библиотек OpenCV (Emgu.CV.runtime.windows)
            }
            catch (TypeInitializationException)
            {
                // Emgu.CV не инициализировался
            }
            return cameras;
        }

        public void StartCapture(int cameraIndex)
        {
            StopCapture();
            _capture = new VideoCapture(cameraIndex);
            if (!_capture.IsOpened)
                throw new InvalidOperationException($"Не удалось открыть камеру {cameraIndex}");

            _capture.Set(CapProp.FrameWidth, 640);
            _capture.Set(CapProp.FrameHeight, 480);

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(() =>
            {
                using var frame = new Mat();
                while (!token.IsCancellationRequested)
                {
                    _capture.Read(frame);
                    if (frame.IsEmpty) continue;

                    // Конвертируем Mat -> Bitmap
                    Bitmap bitmap = frame.ToImage<Bgr, byte>().ToBitmap();
                    NewFrame?.Invoke(bitmap);
                }
            }, token);
        }

        public void StopCapture()
        {
            _cts?.Cancel();
            _capture?.Dispose();
            _capture = null;
        }

        public void Dispose()
        {
            StopCapture();
        }
    }
}