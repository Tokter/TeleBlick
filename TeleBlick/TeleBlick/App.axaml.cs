using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;
using TeleBlick.OpenTelemetry;
using TeleBlick.ViewModels;
using TeleBlick.Views;

namespace TeleBlick;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        SetupDependencyInjection();

        var locator = new ViewLocator();
        DataTemplates.Add(locator);

        //var mainVM = Ioc.Default.GetService<MainViewModel>();
        //var view = (Window)locator.Build(vm);
        //view.DataContext = vm;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var main = Ioc.Default.GetService<MainViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = main
            };
            if (main != null) main.View = desktop.MainWindow;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            throw new Exception("Not supported at this time");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupDependencyInjection()
    {
        var services = new ServiceCollection();
        ConfigureViewModels(services);
        ConfigureCommands(services);
        ConfigureTelemetry(services);
        var provider = services.BuildServiceProvider();
        Ioc.Default.ConfigureServices(provider);
    }

    private void ConfigureViewModels(IServiceCollection services)
    {
        foreach(var vm in Assembly.GetExecutingAssembly().GetTypes().Where(t => typeof(ViewModelBase).IsAssignableFrom(t)))
        {
            services.Add(new ServiceDescriptor(vm, vm, ServiceLifetime.Transient));
        }
    }

    private void ConfigureCommands(IServiceCollection services)
    {
        foreach (var vm in Assembly.GetExecutingAssembly().GetTypes().Where(t => typeof(TeleBlickCommand).IsAssignableFrom(t)))
        {
            services.Add(new ServiceDescriptor(vm, vm, ServiceLifetime.Transient));
        }
    }

    private void ConfigureTelemetry(IServiceCollection services)
    {
        services.Add(new ServiceDescriptor(typeof(TelemetryStorage), typeof(TelemetryStorage), ServiceLifetime.Singleton));
        services.Add(new ServiceDescriptor(typeof(TelemetryServer), typeof(TelemetryServer), ServiceLifetime.Singleton));
    }

}
