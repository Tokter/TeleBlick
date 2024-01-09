using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TeleBlick.ViewModels
{
    public class MenuViewModel : ViewModelBase
    {
        public ICommand? Command { get; set; }

        public string Name { get; set; } = "Menu Item";
        public int Order { get; set; } = 100;
        public Geometry? Icon { get; set; }
        public IList<MenuViewModel>? Items { get; set; } = null;


        public void Sort()
        {
            if (Items != null)
            {
                var sorted = Items.OrderBy(x => x.Order).ToList();
                Items.Clear();
                foreach (var item in sorted)
                {
                    Items.Add(item);
                    item.Sort();
                }
            }
        }

        public static MenuViewModel Build(TeleBlickCommandLocation location, ViewModelBase? rootViewModel = null)
        {
            MenuViewModel root = new();
            var type = typeof(TeleBlickCommand);
            var commands = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => type.IsAssignableFrom(p));

            foreach (var command in commands)
            {
                if (command.GetCustomAttributes(typeof(TeleBlickCommandAttribute), false).FirstOrDefault() is TeleBlickCommandAttribute attribute && attribute.Location == location)
                {
                    var commandInstance = (Design.IsDesignMode ? Activator.CreateInstance(command) : Ioc.Default.GetService(command)) as ITeleBlickCommand;

                    if (commandInstance != null)
                    {
                        var menuItem = new MenuViewModel()
                        {
                            Name = attribute.Name,
                            Command = commandInstance,
                            Order = attribute.Order,
                        };

                        commandInstance.ViewModel = rootViewModel ?? menuItem;

                        if (!string.IsNullOrEmpty(attribute.Icon))
                        {
                            menuItem.Icon = Geometry.Parse(attribute.Icon);
                        }

                        //Find the place where to insert the menu item
                        var parts = menuItem.Name.Split("/");
                        MenuViewModel current = root;
                        for (int i = 0; i < parts.Length; i++)
                        {
                            //If it's the last item, we insert the created menu item
                            if (i == parts.Length - 1)
                            {
                                menuItem.Name = parts[i];
                                current.Items ??= new List<MenuViewModel>();
                                var insertIndex = current.Items?.TakeWhile(p => p.Order < menuItem.Order).Count() ?? 0;
                                current.Items?.Insert(insertIndex, menuItem);
                            }
                            else //Create a parent menu item
                            {
                                var parent = current.Items?.FirstOrDefault(p => p.Name == parts[i]);
                                if (parent == null)
                                {
                                    parent = new MenuViewModel()
                                    {
                                        Name = parts[i],
                                        Order = menuItem.Order
                                    };
                                    var insertIndex = current.Items?.TakeWhile(p => p.Order < menuItem.Order).Count() ?? 0;
                                    current.Items ??= new List<MenuViewModel>();
                                    current.Items.Insert(insertIndex, parent);
                                }
                                current = parent!;
                            }
                        }
                    }
                }
            }
            return root;
        }
    }
}
