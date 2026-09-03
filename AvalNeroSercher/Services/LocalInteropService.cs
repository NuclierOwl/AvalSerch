using AvalNeroSercher.Services.Interop;
using System;

namespace AvalNeroSercher.Services;

public class LocalInteropService : IDisposable
{
    private readonly NamedPipeHostService _namedPipe;
    private readonly FileBridgeService _fileBridge;

    public string PipePath => _namedPipe.PipePath;
    public string BridgeDirectory => _fileBridge.BridgeDirectory;

    public LocalInteropService(AppController controller)
    {
        var handler = new AppCommandHandler(controller);
        _namedPipe = new NamedPipeHostService(handler);
        _fileBridge = new FileBridgeService(handler);
    }

    public void Start()
    {
        _namedPipe.Start();
        _fileBridge.Start();
    }

    public void Dispose()
    {
        _namedPipe.Dispose();
        _fileBridge.Dispose();
    }
}
