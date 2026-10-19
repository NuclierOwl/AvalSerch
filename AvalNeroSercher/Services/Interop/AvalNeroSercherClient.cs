using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvalNeroSercher.Services.Interop;

/// <summary>
/// Клиент для локального взаимодействия с AvalNeroSercher из других .NET-приложений.
/// </summary>
public static class AvalNeroSercherClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<LocalCommandResponse> SendPipeAsync(
        LocalCommandRequest request,
        string pipeName = NamedPipeHostService.DefaultPipeName,
        CancellationToken cancellationToken = default)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(3000, cancellationToken);

        using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), cancellationToken);
        var responseJson = await reader.ReadLineAsync(cancellationToken);

        return JsonSerializer.Deserialize<LocalCommandResponse>(responseJson ?? "{}", JsonOptions)
            ?? new LocalCommandResponse { Success = false, Error = "Пустой ответ" };
    }

    public static LocalCommandResponse SendPipe(LocalCommandRequest request, string pipeName = NamedPipeHostService.DefaultPipeName) =>
        SendPipeAsync(request, pipeName).GetAwaiter().GetResult();

    public static async Task<LocalCommandResponse> SendFileAsync(
        LocalCommandRequest request,
        string? bridgeDirectory = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        bridgeDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AvalNeroSercher",
            "bridge");

        var requestId = request.Id ?? Guid.NewGuid().ToString("N");
        request.Id = requestId;

        var requestsDir = Path.Combine(bridgeDirectory, "requests");
        var responsesDir = Path.Combine(bridgeDirectory, "responses");
        Directory.CreateDirectory(requestsDir);
        Directory.CreateDirectory(responsesDir);

        var requestPath = Path.Combine(requestsDir, $"{requestId}.json");
        var responsePath = Path.Combine(responsesDir, $"{requestId}.json");
        var tempPath = requestPath + ".tmp";

        File.WriteAllText(tempPath, JsonSerializer.Serialize(request));
        File.Move(tempPath, requestPath, overwrite: true);

        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(responsePath))
            {
                var json = await File.ReadAllTextAsync(responsePath, cancellationToken);
                try { File.Delete(responsePath); } catch { /* ignored */ }
                return JsonSerializer.Deserialize<LocalCommandResponse>(json, JsonOptions)
                    ?? new LocalCommandResponse { Success = false, Error = "Пустой ответ" };
            }

            await Task.Delay(50, cancellationToken);
        }

        return new LocalCommandResponse { Id = requestId, Success = false, Error = "Таймаут ожидания ответа" };
    }

    public static LocalCommandResponse SendFile(LocalCommandRequest request, string? bridgeDirectory = null, TimeSpan? timeout = null) =>
        SendFileAsync(request, bridgeDirectory, timeout).GetAwaiter().GetResult();
}
