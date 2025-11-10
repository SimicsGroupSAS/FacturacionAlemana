using System;
using System.Windows;
using System.Windows.Input;

namespace FacturacionAlemana
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.StateChanged += MainWindow_StateChanged;
            // aplicar estado inicial para que las sombras y cornerRadius se ajusten correctamente
            this.Loaded += (s, e) => MainWindow_StateChanged(this, EventArgs.Empty);
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            try
            {
                var root = this.FindName("WindowRootBorder") as System.Windows.Controls.Border;
                if (root == null) return;

                if (this.WindowState == WindowState.Maximized)
                {
                    // Al maximizar quitar radio y sombra para que la ventana "pegue" al borde
                    root.CornerRadius = new CornerRadius(0);
                    root.Margin = new Thickness(0);
                    root.Effect = null;
                }
                else
                {
                    root.CornerRadius = new CornerRadius(10);
                    root.Margin = new Thickness(10);
                    // restaurar efecto
                    root.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = System.Windows.Media.Color.FromArgb(0x22, 0, 0, 0), BlurRadius = 20, ShadowDepth = 6, Opacity = 0.45 };
                }
            }
            catch { }
        }

        private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }

            try
            {
                this.DragMove();
            }
            catch { }
        }
    }
}