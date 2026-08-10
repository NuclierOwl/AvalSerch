using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvalNeroSercher.Services;

public class ApiHostService : IDisposable
{
    private readonly AppController _controller;
    private WebApplication? _app;
    private Task? _runTask;
    private CancellationTokenSource? _cts;

    public int Port { get; private set; }
    public bool IsRunning => _app != null;

    public ApiHostService(AppController controller)
    {
        _controller = controller;
    }

    public void Start(int port = 5050)
    {
        if (_app != null)
            return;

        Port = port;
        _cts = new CancellationTokenSource();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");

        _app = builder.Build();
        _app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");

            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            await next();
        });

        MapRoutes(_app);

        _runTask = _app.RunAsync(_cts.Token);
    }

    private void MapRoutes(WebApplication app)
    {
        app.MapGet("/api/status", () => Results.Json(_controller.GetStatus()));

        app.MapGet("/api/cameras", () =>
            Results.Json(_controller.GetCameras().Select(c => new { c.Index, c.Name })));

        app.MapPost("/api/camera/select", async (HttpRequest request) =>
        {
            var body = await ReadJsonAsync<SelectCameraRequest>(request);
            if (body?.Index is not int index)
                return Results.BadRequest(new { error = "Укажите index камеры" });

            _controller.SelectCamera(index);
            return Results.Json(new { selectedCameraIndex = index });
        });

        app.MapPost("/api/capture/start", (SelectCameraRequest? body) =>
        {
            try
            {
                _controller.StartCapture(body?.Index);
                return Results.Json(new { isCapturing = true });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/capture/stop", () =>
        {
            _controller.StopCapture();
            return Results.Json(new { isCapturing = false });
        });

        app.MapGet("/api/models", () =>
            Results.Json(_controller.GetModels().Select(m => new
            {
                m.Name,
                m.Path,
                m.IsLoaded
            })));

        app.MapPost("/api/models/refresh", () =>
            Results.Json(_controller.GetModels().Select(m => new
            {
                m.Name,
                m.Path,
                m.IsLoaded
            })));

        app.MapPost("/api/models/load", async (HttpRequest request) =>
        {
            var body = await ReadJsonAsync<LoadModelRequest>(request);
            if (body == null)
                return Results.BadRequest(new { error = "Пустое тело запроса" });

            try
            {
                if (!string.IsNullOrWhiteSpace(body.Name))
                    _controller.LoadModelByName(body.Name);
                else if (!string.IsNullOrWhiteSpace(body.Path))
                    _controller.LoadModel(body.Path!);
                else
                    return Results.BadRequest(new { error = "Укажите name или path" });

                return Results.Json(new
                {
                    loaded = true,
                    modelName = _controller.CurrentModelName,
                    modelPath = _controller.CurrentModelPath
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/predict", async (HttpRequest request) =>
        {
            try
            {
                if (request.HasFormContentType && request.Form.Files.Count > 0)
                {
                    var file = request.Form.Files[0];
                    await using var stream = file.OpenReadStream();
                    using var memory = new MemoryStream();
                    await stream.CopyToAsync(memory);
                    var result = _controller.PredictFromBytes(memory.ToArray());
                    return Results.Json(new
                    {
                        digit = result.Digit,
                        confidence = result.Confidence,
                        modelName = _controller.CurrentModelName
                    });
                }

                var body = await ReadJsonAsync<PredictPathRequest>(request);
                if (body?.Path is not string path || string.IsNullOrWhiteSpace(path))
                    return Results.BadRequest(new { error = "Передайте файл (multipart) или JSON { \"path\": \"...\" }" });

                var fileResult = _controller.PredictFromFile(path);
                return Results.Json(new
                {
                    digit = fileResult.Digit,
                    confidence = fileResult.Confidence,
                    modelName = _controller.CurrentModelName
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _app?.StopAsync().GetAwaiter().GetResult();
        _app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _cts?.Dispose();
    }
}

public class SelectCameraRequest
{
    public int? Index { get; set; }
}

public class LoadModelRequest
{
    public string? Name { get; set; }
    public string? Path { get; set; }
}

public class PredictPathRequest
{
    public string? Path { get; set; }
}
