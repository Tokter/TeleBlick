using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TeleBlick.ViewModels;

namespace TeleBlick
{
    public enum TeleBlickCommandLocation
    {
        MainMenu,
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class TeleBlickCommandAttribute : Attribute
    {
        public string Name { get; set; } = "Command Name";
        public TeleBlickCommandLocation Location { get; set; }
        public int Order { get; set; } = 100;
        public string? Icon { get; set; }

        public TeleBlickCommandAttribute(string name, TeleBlickCommandLocation location, int order = 100, string? icon = null)
        {
            Name = name;
            Location = location;
            Order = order;
            Icon = icon;
        }
    }

    /// <summary>
    /// A command that can be placed somewhere in the user interface
    /// </summary>
    public interface ITeleBlickCommand : ICommand
    {
        ViewModelBase? ViewModel { get; set; }
        Task ExecuteAsync(object? parameter);
        void RaiseCanExecuteChanged();
    }

    public class TeleBlickCommand : ITeleBlickCommand
    {
        public ViewModelBase? ViewModel { get; set; }

        public event EventHandler? CanExecuteChanged;

        public virtual bool CanExecute(object? parameter)
        {
            throw new NotImplementedException();
        }

        public virtual Task ExecuteAsync(object? parameter)
        {
            throw new NotImplementedException();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public IStorageProvider? GetStorageProvider() => ViewModel?.View != null ? TopLevel.GetTopLevel(ViewModel?.View)?.StorageProvider : null;

        #region Explicit implementations
        bool ICommand.CanExecute(object? parameter)
        {
            return CanExecute(parameter);
        }

        void ICommand.Execute(object? parameter)
        {
            ExecuteAsync(parameter);
        }
        #endregion
    }
}
