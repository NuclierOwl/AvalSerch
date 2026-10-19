using AvalNeroSercher.Services.Interop;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvalNeroSercher.Services;

public class ApiHostService : IDisposable
{
    private readonly AppCommandHandler _handler;
    private WebApplication? _app;
    private Task? _runTask;
    private CancellationTokenSource? _cts;

    public int Port { get; private set; }

    public ApiHostService(AppController controller)
    {
        _handler = new AppCommandHandler(controller);
    }

    public void Start(int port = 5050)
    {
        if (_app != null)
            return;

        Port = port;
        _cts = new CancellationTokenSource();

        var host = Environment.GetEnvironmentVariable("AVALNERO_API_HOST") ?? "127.0.0.1";
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://{host}:{port}");

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
        _runTask = _app.RunAsync();
    }

    private void MapRoutes(WebApplication app)
    {
        app.MapGet("/api/status", () => ToHttpResult(_handler.Handle(new LocalCommandRequest { Command = "status" })));
        app.MapGet("/api/cameras", () => ToHttpResult(_handler.Handle(new LocalCommandRequest { Command = "cameras" })));

        app.MapPost("/api/camera/select", async (HttpRequest request) =>
            ToHttpResult(await HandleHttpCommandAsync(request, "camera/select")));

        app.MapPost("/api/capture/start", async (HttpRequest request) =>
            ToHttpResult(await HandleHttpCommandAsync(request, "capture/start")));

        app.MapPost("/api/capture/stop", () =>
            ToHttpResult(_handler.Handle(new LocalCommandRequest { Command = "capture/stop" })));

        app.MapGet("/api/models", () =>
            ToHttpResult(_handler.Handle(new LocalCommandRequest { Command = "models" })));

        app.MapPost("/api/models/refresh", () =>
            ToHttpResult(_handler.Handle(new LocalCommandRequest { Command = "models/refresh" })));

        app.MapPost("/api/models/load", async (HttpRequest request) =>
            ToHttpResult(await HandleHttpCommandAsync(request, "models/load")));

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
                    return ToHttpResult(_handler.Handle(new LocalCommandRequest
                    {
                        Command = "predict",
                        Data = JsonSerializer.SerializeToElement(new
                        {
                            imageBase64 = Convert.ToBase64String(memory.ToArray())
                        })
                    }));
                }

                return ToHttpResult(await HandleHttpCommandAsync(request, "predict"));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/command", async (HttpRequest request) =>
            ToHttpResult(await HandleHttpCommandAsync(request, null)));
    }

    private async Task<LocalCommandResponse> HandleHttpCommandAsync(HttpRequest request, string? command)
    {
        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(json))
        {
            return _handler.Handle(new LocalCommandRequest { Command = command ?? string.Empty });
        }

        var parsed = JsonSerializer.Deserialize<LocalCommandRequest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (parsed == null)
            return new LocalCommandResponse { Success = false, Error = "Некорректный JSON" };

        if (!string.IsNullOrWhiteSpace(command))
            parsed.Command = command;

        return _handler.Handle(parsed);
    }

    private static IResult ToHttpResult(LocalCommandResponse response) =>
        response.Success
            ? Results.Json(response.Data)
            : Results.BadRequest(new { error = response.Error });

    public void Dispose()
    {
        _cts?.Cancel();
        _app?.StopAsync().GetAwaiter().GetResult();
        _app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _cts?.Dispose();
    }
}
