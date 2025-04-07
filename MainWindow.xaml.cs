using System.Windows;
using Wpf.Ui.Controls;

namespace FacturacionAlemana
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            RootFrame.Navigate(new HomePage());
        }
    }
}