using System;
using System.Collections.Generic;
using TeleBlick.OpenTelemetry;

namespace TeleBlick.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private TelemetryServer _server;

    public IList<MenuViewModel>? MainMenu { get; set; }

    public string Greeting => "Welcome to Avalonia!";

    public MainViewModel()
    {
        _server = new TelemetryServer();
        _server.Start();

        //MainMenu = BuildMenuItems(PlaygroundCommandLocation.MainMenu).Items;
    }

    public void Dispose()
    {
        _server.Stop();
    }

}
