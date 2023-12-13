using Avalonia.Controls;
using System;

namespace TeleBlick.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (this.DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
