using System.Windows;

namespace FacturacionAlemana
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Permitir arrastrar la ventana haciendo clic en cualquier parte
            this.MouseLeftButtonDown += (s, e) => this.DragMove();
        }
    }
}