using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvalNeroSercher.Services.Interop;

public class NamedPipeHostService : IDisposable
{
    public const string DefaultPipeName = "AvalNeroSercher";

    private readonly AppCommandHandler _handler;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public string PipeName { get; }
    public string PipePath => $@"\\.\pipe\{PipeName}";

    public NamedPipeHostService(AppCommandHandler handler, string? pipeName = null)
    {
        _handler = handler;
        PipeName = pipeName ?? DefaultPipeName;
    }

    public void Start()
    {
        if (_listenTask != null)
            return;

        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(token);
                await HandleClientAsync(server, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Клиент отключился — ждём следующее подключение
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken token)
    {
        using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        var requestJson = await reader.ReadLineAsync(token);
        var response = _handler.HandleJson(requestJson ?? string.Empty);
        await writer.WriteLineAsync(JsonSerializer.Serialize(response).AsMemory(), token);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _listenTask?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignored */ }
        _cts?.Dispose();
    }
}
