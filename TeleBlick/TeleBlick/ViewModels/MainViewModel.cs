using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using TeleBlick.OpenTelemetry;

namespace TeleBlick.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private TelemetryServer _server;

    public IList<MenuViewModel>? MainMenu { get; set; }

    [ObservableProperty]
    private ViewModelBase _currentPage = Ioc.Default.GetService<DashViewModel>()!;

    public MainViewModel()
    {
        _server = Ioc.Default.GetService<TelemetryServer>()!;
        _server.Start();

        //MainMenu = BuildMenuItems(PlaygroundCommandLocation.MainMenu).Items;
    }

    public void Dispose()
    {
        _server.Stop();
    }

}
