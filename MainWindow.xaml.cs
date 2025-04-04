using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FacturacionAlemana.Models;
using FacturacionAlemana.Services;
using FacturacionAlemana.Utils;

namespace FacturacionAlemana
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Factura? factura;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnLoadXmlClick(object sender, RoutedEventArgs e)
        {
            var filePath = FileDialogHelper.AbrirArchivoXml();
            if (filePath == null) return;

            try
            {
                factura = XmlReaderService.LeerFacturaDesdeXml(filePath);
                StatusText.Text = $"Factura cargada: Cliente - {factura.Cliente}, Total - {factura.Total:C}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al leer el archivo XML: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnGeneratePdfClick(object sender, RoutedEventArgs e)
        {
            if (factura == null)
            {
                MessageBox.Show("Por favor, carga un archivo XML primero.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var outputPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Factura.pdf");
            try
            {
                PdfGeneratorService.GenerarFacturaPdf(factura, outputPath);
                StatusText.Text = $"PDF generado: {outputPath}";
                MessageBox.Show("Factura generada exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}