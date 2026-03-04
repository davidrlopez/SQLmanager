using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;
using FluentAvalonia.UI.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Controls.Shapes;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using System;
using System.Runtime.InteropServices;

namespace SQLmanager.Controls
{
    public partial class LabeledInputWithIcon : UserControl
    {
        public static readonly StyledProperty<string> LabelTextProperty =
            AvaloniaProperty.Register<LabeledInputWithIcon, string>(nameof(LabelText));

        public string LabelText
        {
            get => GetValue(LabelTextProperty);
            set => SetValue(LabelTextProperty, value);
        }

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<LabeledInputWithIcon, string>(nameof(Text));

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly StyledProperty<Symbol> IconSymbolProperty =
            AvaloniaProperty.Register<LabeledInputWithIcon, Symbol>(nameof(IconSymbol), Symbol.ContactInfo);

        public Symbol IconSymbol
        {
            get => GetValue(IconSymbolProperty);
            set => SetValue(IconSymbolProperty, value);
        }
        public LabeledInputWithIcon()
        {
            InitializeComponent();
            this.DataContext = this;
            SetIconForPlatform();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _iconPresenter = this.FindControl<ContentControl>("IconPresenter");
        }

        private ContentControl? _iconPresenter;

        private void SetIconForPlatform()
        {
            if (_iconPresenter == null)
                return;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _iconPresenter.Content = new SymbolIcon()
                    {
                        Symbol = IconSymbol,
                        Width = 24,
                        Height = 24
                    };
                }
                else
                {
                    _iconPresenter.Content = new Image
                    {
                        Source = new Bitmap("Assets/Icons/user.png"),
                        Width = 24,
                        Height = 24,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    };
                }
            }
            catch (Exception ex)
            {
                // Si falla, mostrar algo simple para no romper la UI
                _iconPresenter.Content = new TextBlock
                {
                    Text = "?",
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    Foreground = Avalonia.Media.Brushes.Red,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                // Opcional: log del error en consola
                Console.WriteLine("Error cargando icono: " + ex);
            }
        }
    }
}