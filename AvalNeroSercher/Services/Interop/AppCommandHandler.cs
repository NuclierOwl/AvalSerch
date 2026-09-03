using System;
using System.Linq;
using System.Text.Json;

namespace AvalNeroSercher.Services.Interop;

public class LocalCommandRequest
{
    public string? Id { get; set; }
    public string Command { get; set; } = string.Empty;
    public JsonElement? Data { get; set; }
}

public class LocalCommandResponse
{
    public string? Id { get; set; }
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
}

public class AppCommandHandler
{
    private readonly AppController _controller;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AppCommandHandler(AppController controller) => _controller = controller;

    public LocalCommandResponse Handle(LocalCommandRequest request)
    {
        try
        {
            var data = request.Data ?? default;
            return request.Command.ToLowerInvariant() switch
            {
                "status" => Success(request.Id, _controller.GetStatus()),
                "cameras" => Success(request.Id, _controller.GetCameras()
                    .Select(c => new { c.Index, c.Name })),
                "camera/select" => HandleSelectCamera(request.Id, data),
                "capture/start" => HandleStartCapture(request.Id, data),
                "capture/stop" => HandleStopCapture(request.Id),
                "models" or "models/refresh" => Success(request.Id, _controller.GetModels()
                    .Select(m => new { m.Name, m.Path, m.IsLoaded })),
                "models/load" => HandleLoadModel(request.Id, data),
                "predict" => HandlePredict(request.Id, data),
                _ => Fail(request.Id, $"Неизвестная команда: {request.Command}")
            };
        }
        catch (Exception ex)
        {
            return Fail(request.Id, ex.Message);
        }
    }

    public LocalCommandResponse HandleJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Fail(null, "Пустой запрос");

        var request = JsonSerializer.Deserialize<LocalCommandRequest>(json, JsonOptions);
        if (request == null || string.IsNullOrWhiteSpace(request.Command))
            return Fail(null, "Укажите поле command");

        return Handle(request);
    }

    private LocalCommandResponse HandleSelectCamera(string? id, JsonElement data)
    {
        var index = GetInt(data, "index");
        if (index is not int cameraIndex)
            return Fail(id, "Укажите data.index");

        _controller.SelectCamera(cameraIndex);
        return Success(id, new { selectedCameraIndex = cameraIndex });
    }

    private LocalCommandResponse HandleStartCapture(string? id, JsonElement data)
    {
        var index = GetInt(data, "index");
        _controller.StartCapture(index);
        return Success(id, new { isCapturing = true });
    }

    private LocalCommandResponse HandleStopCapture(string? id)
    {
        _controller.StopCapture();
        return Success(id, new { isCapturing = false });
    }

    private LocalCommandResponse HandleLoadModel(string? id, JsonElement data)
    {
        var name = GetString(data, "name");
        var path = GetString(data, "path");

        if (!string.IsNullOrWhiteSpace(name))
            _controller.LoadModelByName(name!);
        else if (!string.IsNullOrWhiteSpace(path))
            _controller.LoadModel(path!);
        else
            return Fail(id, "Укажите data.name или data.path");

        return Success(id, new
        {
            loaded = true,
            modelName = _controller.CurrentModelName,
            modelPath = _controller.CurrentModelPath
        });
    }

    private LocalCommandResponse HandlePredict(string? id, JsonElement data)
    {
        var path = GetString(data, "path");
        var imageBase64 = GetString(data, "imageBase64");

        (string Digit, float Confidence) result;
        if (!string.IsNullOrWhiteSpace(imageBase64))
            result = _controller.PredictFromBytes(Convert.FromBase64String(imageBase64!));
        else if (!string.IsNullOrWhiteSpace(path))
            result = _controller.PredictFromFile(path!);
        else
            return Fail(id, "Укажите data.path или data.imageBase64");

        return Success(id, new
        {
            digit = result.Digit,
            confidence = result.Confidence,
            modelName = _controller.CurrentModelName
        });
    }

    private static LocalCommandResponse Success(string? id, object? data) => new()
    {
        Id = id,
        Success = true,
        Data = data
    };

    private static LocalCommandResponse Fail(string? id, string error) => new()
    {
        Id = id,
        Success = false,
        Error = error
    };

    private static int? GetInt(JsonElement data, string name)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static string? GetString(JsonElement data, string name)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
