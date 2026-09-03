using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvalNeroSercher.Services.Interop;

public class FileBridgeService : IDisposable
{
    private readonly AppCommandHandler _handler;
    private FileSystemWatcher? _watcher;
    private int _processing;

    public string BridgeDirectory { get; }

    public FileBridgeService(AppCommandHandler handler, string? bridgeDirectory = null)
    {
        _handler = handler;
        BridgeDirectory = bridgeDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AvalNeroSercher",
                "bridge");

        Directory.CreateDirectory(Path.Combine(BridgeDirectory, "requests"));
        Directory.CreateDirectory(Path.Combine(BridgeDirectory, "responses"));
    }

    public void Start()
    {
        if (_watcher != null)
            return;

        var requestsDir = Path.Combine(BridgeDirectory, "requests");
        _watcher = new FileSystemWatcher(requestsDir)
        {
            Filter = "*.json",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnRequestFile;
        _watcher.Changed += OnRequestFile;

        foreach (var file in Directory.EnumerateFiles(requestsDir, "*.json"))
            QueueRequest(file);
    }

    private void OnRequestFile(object sender, FileSystemEventArgs e) => QueueRequest(e.FullPath);

    private void QueueRequest(string requestPath)
    {
        if (Interlocked.CompareExchange(ref _processing, 1, 0) != 0)
            return;

        Task.Run(() =>
        {
            try { ProcessRequestFile(requestPath); }
            finally { Interlocked.Exchange(ref _processing, 0); }
        });
    }

    private void ProcessRequestFile(string requestPath)
    {
        Thread.Sleep(50);
        if (!File.Exists(requestPath))
            return;

        string json;
        try { json = File.ReadAllText(requestPath); }
        catch { return; }

        var response = _handler.HandleJson(json);
        var requestId = response.Id ?? Path.GetFileNameWithoutExtension(requestPath);
        var responsePath = Path.Combine(BridgeDirectory, "responses", $"{requestId}.json");
        var tempPath = responsePath + ".tmp";

        File.WriteAllText(tempPath, JsonSerializer.Serialize(response));
        File.Move(tempPath, responsePath, overwrite: true);

        try { File.Delete(requestPath); } catch { /* ignored */ }
    }

    public void Dispose()
    {
        if (_watcher == null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnRequestFile;
        _watcher.Changed -= OnRequestFile;
        _watcher.Dispose();
    }
}
