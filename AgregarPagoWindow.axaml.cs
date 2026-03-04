using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLmanager
{
    public partial class AgregarPagoWindow : Window
    {
        public AgregarPagoWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
