using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
            InitializeRecentList();

            // Registrar evento para doble click en la lista (buscamos control dinámicamente)
            try
            {
                var lv = FindName("LvRecent") as System.Windows.Controls.ListView;
                if (lv != null) lv.MouseDoubleClick += LvRecent_MouseDoubleClick;
            }
            catch { }
        }

        private void OnLoadXmlClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var filePath = FileDialogHelper.AbrirArchivoXml();
                if (filePath == null) return;

                factura = XmlReaderService.LeerFacturaDesdeXml(filePath);
                string fileName = System.IO.Path.GetFileName(filePath);
                var st = FindName("StatusText") as System.Windows.Controls.TextBlock;
                if (st != null) st.Text = $"Archivo cargado: {fileName}";

                // Añadir a recientes (si está listado)
                try
                {
                    var item = new ListViewItem { Content = fileName, Tag = filePath };
                    var lv = FindName("LvRecent") as System.Windows.Controls.ListView;
                    if (lv != null)
                    {
                        // Eliminar placeholder '(No hay archivos de ejemplo)' si está presente
                        for (int i = lv.Items.Count - 1; i >= 0; i--)
                        {
                            if (lv.Items[i] is ListViewItem li && li.Content is string s && s == "(No hay archivos de ejemplo)")
                            {
                                lv.Items.RemoveAt(i);
                            }
                        }

                        lv.Items.Insert(0, item);
                    }
                }
                catch { }
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
                    var st = FindName("StatusText") as System.Windows.Controls.TextBlock;
                    if (st != null) st.Text = $"PDF generado: {outputPath}";
                }
                else
                {
                    var st = FindName("StatusText") as System.Windows.Controls.TextBlock;
                    if (st != null) st.Text = "Generación de PDF cancelada";
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
                var st2 = FindName("StatusText") as System.Windows.Controls.TextBlock;
                if (st2 != null) st2.Text = $"PDF generado: {outputPath}";
                MessageBox.Show("Factura generada exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }        
        private void OnCreateInvoiceClick(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new CreateInvoicePage());
        }

        private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService?.Navigate(new SettingsPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir la configuración: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeRecentList()
        {
            try
            {
                string rutaReal = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                var directorioReal = Path.GetDirectoryName(rutaReal) ?? AppDomain.CurrentDomain.BaseDirectory;
                var ejemplosDir = Path.Combine(directorioReal, "Ejemplos");

                if (!Directory.Exists(ejemplosDir))
                {
                    // intentar ruta relativa al proyecto (cuando se ejecuta desde VS)
                    var probable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Ejemplos");
                    probable = Path.GetFullPath(probable);
                    if (Directory.Exists(probable)) ejemplosDir = probable;
                }

                if (Directory.Exists(ejemplosDir))
                {
                    var files = Directory.GetFiles(ejemplosDir, "*.xml");
                    Array.Sort(files);
                    Array.Reverse(files);
                    foreach (var f in files)
                    {
                        var item = new ListViewItem { Content = Path.GetFileName(f), Tag = f };
                        var lv2 = FindName("LvRecent") as System.Windows.Controls.ListView;
                        if (lv2 != null) lv2.Items.Add(item);
                    }
                }
                else
                {
                    var lv3 = FindName("LvRecent") as System.Windows.Controls.ListView;
                    if (lv3 != null) lv3.Items.Add(new ListViewItem { Content = "(No hay archivos de ejemplo)", IsEnabled = false });
                }
            }
            catch
            {
                // no bloquear la UI por errores en carga de recientes
            }
        }

        private void LvRecent_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var lv4 = FindName("LvRecent") as System.Windows.Controls.ListView;
                if (lv4 != null && lv4.SelectedItem is ListViewItem item && item.Tag is string path && File.Exists(path))
                {
                    factura = XmlReaderService.LeerFacturaDesdeXml(path);
                    var st3 = FindName("StatusText") as System.Windows.Controls.TextBlock;
                    if (st3 != null) st3.Text = $"Archivo cargado: {Path.GetFileName(path)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el archivo seleccionado: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadIcon()
        {
            try
            {
                Uri iconUri = new Uri("pack://application:,,,/Assets/icono.ico", UriKind.Absolute);
                BitmapImage iconImage = new BitmapImage(iconUri);
                var img = FindName("IconImg") as System.Windows.Controls.Image;
                if (img != null) img.Source = iconImage;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el ícono: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }        
        private void OnCloseWindowClick(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.Close();
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window == null) return;

            if (e.ClickCount == 2)
            {
                // Doble clic: alternar maximizar
                window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }

            try
            {
                window.DragMove();
            }
            catch { }
        }
    }
}
