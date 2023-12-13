using System;
using TeleBlick.OpenTelemetry;

namespace TeleBlick.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private TelemetryServer _server;

    public string Greeting => "Welcome to Avalonia!";

    public MainViewModel()
    {
        _server = new TelemetryServer();
        _server.Start();
    }

    public void Dispose()
    {
        _server.Stop();
    }

}
