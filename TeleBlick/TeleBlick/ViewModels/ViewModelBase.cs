using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TeleBlick.ViewModels;

public class ViewModelBase : ObservableObject
{
    public Control? View { get; set; }
}

public class ViewModelRecipient : ObservableRecipient
{
}