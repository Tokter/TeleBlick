using Avalonia.Media;
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
    }
}
