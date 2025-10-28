using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using FacturacionAlemana.Models;
using FacturacionAlemana.Services;
using FacturacionAlemana.Utils;

namespace FacturacionAlemana
{
    public partial class HomePage : Page
    {
        private Factura? factura;

        public HomePage()
        {
            InitializeComponent();
            LoadIcon();
        }

        private void OnLoadXmlClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var filePath = FileDialogHelper.AbrirArchivoXml();
                if (filePath == null) return;

                factura = XmlReaderService.LeerFacturaDesdeXml(filePath);
                string fileName = System.IO.Path.GetFileName(filePath);
                StatusText.Text = $"Archivo cargado: {fileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al leer el archivo XML: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnPreviewClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (factura == null)
                {
                    MessageBox.Show("Por favor, carga un archivo XML primero.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string rutaReal = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable.");
                var directorioReal = Path.GetDirectoryName(rutaReal) ?? throw new InvalidOperationException("No se pudo determinar el directorio del ejecutable.");
                var outputPath = Path.Combine(directorioReal, "Factura.pdf");

                // Abrir ventana de previsualización
                var previewWindow = new PreviewWindow(factura, outputPath)
                {
                    Owner = Window.GetWindow(this)
                };
                
                if (previewWindow.ShowDialog() == true)
                {
                    StatusText.Text = $"PDF generado: {outputPath}";
                }
                else
                {
                    StatusText.Text = "Generación de PDF cancelada";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al previsualizar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnGeneratePdfClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (factura == null)
                {
                    MessageBox.Show("Por favor, carga un archivo XML primero.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string rutaReal = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable.");
                var directorioReal = Path.GetDirectoryName(rutaReal) ?? throw new InvalidOperationException("No se pudo determinar el directorio del ejecutable.");
                var outputPath = Path.Combine(directorioReal, "Factura.pdf");
                PdfGeneratorService.GenerarFacturaPdf(factura, outputPath);
                StatusText.Text = $"PDF generado: {outputPath}";
                MessageBox.Show("Factura generada exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadIcon()
        {
            try
            {
                Uri iconUri = new Uri("pack://application:,,,/Assets/icono.ico", UriKind.Absolute);
                BitmapImage iconImage = new BitmapImage(iconUri);
                IconImg.Source = iconImage;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el ícono: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
