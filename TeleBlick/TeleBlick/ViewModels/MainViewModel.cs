using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TeleBlick.OpenTelemetry;

namespace TeleBlick.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private TelemetryServer _server;

    public IList<MenuViewModel>? MainMenu { get; set; }

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private bool _isPaneOpen = false;

    [ObservableProperty]
    private ListItemTemplate? _selectedListItem;

    public AvaloniaList<SearchItem> SearchTerms { get; } = new AvaloniaList<SearchItem>();

    public MainViewModel()
    {
        _server = Ioc.Default.GetService<TelemetryServer>()!;
        _server.Start();

        //MainMenu = BuildMenuItems(PlaygroundCommandLocation.MainMenu).Items;
        SelectedListItem = Items[0];

        SearchTerms.Add(new SearchItem("Server Startup", "Traces"));
        SearchTerms.Add(new SearchItem("CMDServer", "Applications"));
        SearchTerms.Add(new SearchItem("CMDWorker", "Applications"));
    }

    partial void OnSelectedListItemChanged(ListItemTemplate? value)
    {
        if (value is null) return;

        var instance = Design.IsDesignMode
            ? Activator.CreateInstance(value.ModelType)
            : Ioc.Default.GetService(value.ModelType);

        if (instance is null) return;
        CurrentPage = (ViewModelBase)instance;
    }

    public ObservableCollection<ListItemTemplate> Items { get; } = new()
    {
        new ListItemTemplate(typeof(DashViewModel), "Dashboard", "M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z"),
        new ListItemTemplate(typeof(TracesViewModel), "Traces", "M2,5H10V2H12V22H10V18H6V15H10V13H4V10H10V8H2V5M14,5H17V8H14V5M14,10H19V13H14V10M14,15H22V18H14V15Z"),
    };

    [RelayCommand]
    private void TriggerPane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    public void Dispose()
    {
        _server.Stop();
    }
}

public class ListItemTemplate
{
    public ListItemTemplate(Type type, string label, string iconSVG)
    {
        ModelType = type;
        Label = label;
        ListItemIcon = StreamGeometry.Parse(iconSVG);
    }

    public string Label { get; }
    public Type ModelType { get; }
    public StreamGeometry ListItemIcon { get; }
}

public class SearchItem
{
    public SearchItem(string searchText, string searchGroup)
    {
        SearchText = searchText;
        SearchGroup = searchGroup;
    }

    public string SearchText { get; set; }

    public string SearchGroup { get; set; }
}