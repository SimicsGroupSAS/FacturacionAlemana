using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private LocalizationService? _localization;        
        public HomePage()
        {
            InitializeComponent();
            LoadIcon();
            InitializeLanguageSelector();  // Inicializar primero para que _localization esté disponible
            InitializeRecentList();
            UpdateUIText();  // Actualizar textos iniciales según el idioma actual

            // Restaurar ventana a tamaño normal cuando se carga HomePage
            this.Loaded += (s, e) => 
            {
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    window.WindowState = WindowState.Normal;
                }
            };

            // Registrar evento para doble click en la lista
            try
            {
                var lv = FindName("LvRecent") as ListView;
                if (lv != null) lv.MouseDoubleClick += LvRecent_MouseDoubleClick;
            }
            catch { }
        }

        /// <summary>
        /// Inicializa el selector de idiomas
        /// </summary>
        private void InitializeLanguageSelector()
        {
            try
            {
                _localization = LocalizationService.Instance;
                var comboBox = FindName("LanguageComboBox") as ComboBox;
                
                if (comboBox != null && _localization != null)
                {
                    // Agregar idiomas disponibles
                    foreach (var lang in _localization.AvailableLanguages)
                    {
                        comboBox.Items.Add(new ComboBoxItem 
                        { 
                            Content = lang.DisplayName, 
                            Tag = lang.Code 
                        });
                    }

                    // Seleccionar el idioma actual
                    var currentItem = comboBox.Items.Cast<ComboBoxItem>()
                        .FirstOrDefault(item => item.Tag?.ToString() == _localization.CurrentLanguage);
                    
                    if (currentItem != null)
                    {
                        comboBox.SelectedItem = currentItem;
                    }

                    // Suscribirse a cambios de idioma
                    _localization.LanguageChangedUI += (s, e) => UpdateUIText();
                }
            }
            catch { }
        }

        /// <summary>
        /// Maneja el cambio de idioma
        /// </summary>
        private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox || comboBox.SelectedItem is not ComboBoxItem item)
                return;

            var languageCode = item.Tag?.ToString();
            if (languageCode != null && _localization != null)
            {
                _localization.SetLanguage(languageCode);
            }
        }

        /// <summary>
        /// Actualiza el texto de la interfaz según el idioma actual
        /// </summary>
        private void UpdateUIText()
        {
            if (_localization == null)
                return;

            try
            {
                // Actualizar textos del header
                var appTitle = FindName("AppTitle") as TextBlock;
                if (appTitle != null) appTitle.Text = _localization.Get("App.Title");

                var subtitle = FindName("Subtitle") as TextBlock;
                if (subtitle != null) subtitle.Text = _localization.Get("App.Subtitle");

                var statusText = FindName("StatusText") as TextBlock;
                if (statusText != null) statusText.Text = _localization.Get("App.Ready");

                // Actualizar títulos de secciones
                var actionsTitle = FindName("ActionsTitle") as TextBlock;
                if (actionsTitle != null) actionsTitle.Text = _localization.Get("HomePage.Title");

                var recentFilesTitle = FindName("RecentFilesTitle") as TextBlock;
                if (recentFilesTitle != null) recentFilesTitle.Text = _localization.Get("HomePage.RecentFiles");

                var quickHelpText = FindName("QuickHelpText") as TextBlock;
                if (quickHelpText != null) quickHelpText.Text = _localization.Get("HomePage.QuickHelp");

                var recentFilesHint = FindName("RecentFilesHint") as TextBlock;
                if (recentFilesHint != null) recentFilesHint.Text = _localization.Get("HomePage.RecentFilesHint");

                // Actualizar textos de botones
                UpdateAllButtonTexts();

                // Actualizar placeholder de la lista de archivos recientes
                var lv = FindName("LvRecent") as ListView;
                if (lv != null)
                {
                    for (int i = 0; i < lv.Items.Count; i++)
                    {
                        if (lv.Items[i] is ListViewItem li && li.Tag?.ToString() == "PLACEHOLDER")
                        {
                            li.Content = _localization.Get("HomePage.NoRecentFiles");
                            break;
                        }
                    }
                }

                // Actualizar tooltips de botones del header
                var btnMinimize = FindName("BtnMinimize") as Button;
                if (btnMinimize != null) btnMinimize.ToolTip = _localization.Get("HomePage.Minimize");

                var btnMaxRestore = FindName("BtnMaxRestore") as Button;
                if (btnMaxRestore != null) btnMaxRestore.ToolTip = _localization.Get("HomePage.MaximizeRestore");

                var btnClose = FindName("BtnClose") as Button;
                if (btnClose != null) btnClose.ToolTip = _localization.Get("HomePage.Close");
            }
            catch { }
        }

        /// <summary>
        /// Actualiza todos los textos de botones
        /// </summary>
        private void UpdateAllButtonTexts()
        {
            if (_localization == null) return;

            // Mapeo directo de nombres de botones a claves de traducción
            var buttonMappings = new Dictionary<string, string>
            {
                { "BtnLoadXml", "HomePage.LoadXml" },
                { "BtnPreview", "CreateInvoicePage.Preview" },
                { "BtnGeneratePdf", "HomePage.GeneratePdf" },
                { "BtnCreateInvoice", "HomePage.CreateInvoice" }
            };

            foreach (var mapping in buttonMappings)
            {
                var button = FindName(mapping.Key) as Button;
                if (button != null && _localization.Exists(mapping.Value))
                {
                    button.Content = _localization.Get(mapping.Value);
                }
            }
        }

        private void OnLoadXmlClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var filePath = FileDialogHelper.AbrirArchivoXml();
                if (filePath == null) return;

                factura = XmlReaderService.LeerFacturaDesdeXml(filePath);
                string fileName = Path.GetFileName(filePath);
                var st = FindName("StatusText") as TextBlock;
                if (st != null) st.Text = _localization?.Get("HomePage.LoadedFile", fileName) ?? $"Archivo cargado: {fileName}";

                // Añadir a recientes
                try
                {
                    var item = new ListViewItem { Content = fileName, Tag = filePath };
                    var lv = FindName("LvRecent") as ListView;
                    if (lv != null)
                    {
                        // Eliminar placeholder si está presente (identificado por Tag = "PLACEHOLDER")
                        for (int i = lv.Items.Count - 1; i >= 0; i--)
                        {
                            if (lv.Items[i] is ListViewItem li && li.Tag?.ToString() == "PLACEHOLDER")
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
                var msg = (_localization != null && _localization.Exists("Messages.ErrorLoadingXml"))
                    ? string.Format(_localization.Get("Messages.ErrorLoadingXml"), ex.Message)
                    : $"Error al leer el archivo XML: {ex.Message}";
                var title = (_localization != null && _localization.Exists("Messages.ErrorTitle"))
                    ? _localization.Get("Messages.ErrorTitle")
                    : "Error";
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnPreviewClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (factura == null)
                {
                    const string noXmlKey = "HomePage.Errors.NoXmlLoaded";
                    var noXmlMsg = (_localization != null && _localization.Exists(noXmlKey))
                        ? _localization.Get(noXmlKey)
                        : "Por favor, carga un archivo XML primero.";
                    var errTitle = (_localization != null && _localization.Exists("Messages.ErrorTitle"))
                        ? _localization.Get("Messages.ErrorTitle")
                        : "Error";
                    MessageBox.Show(noXmlMsg, errTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Establecer idioma en PdfGeneratorService
                if (_localization != null)
                {
                    PdfGeneratorService.SetLanguage(_localization.CurrentLanguage);
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
                    var st = FindName("StatusText") as TextBlock;
                    if (st != null) st.Text = $"PDF generado: {outputPath}";
                }
                else
                {
                    var st = FindName("StatusText") as TextBlock;
                    if (st != null) st.Text = "Generación de PDF cancelada";
                }
            }
            catch (Exception ex)
            {
                var msg = (_localization != null && _localization.Exists("Messages.ErrorGeneratingPdf"))
                    ? string.Format(_localization.Get("Messages.ErrorGeneratingPdf"), ex.Message)
                    : $"Error al previsualizar: {ex.Message}";
                var title = (_localization != null && _localization.Exists("Messages.ErrorTitle"))
                    ? _localization.Get("Messages.ErrorTitle")
                    : "Error";
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnGeneratePdfClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (factura == null)
                {
                    const string noXmlKey = "HomePage.Errors.NoXmlLoaded";
                    var noXmlMsg = (_localization != null && _localization.Exists(noXmlKey))
                        ? _localization.Get(noXmlKey)
                        : "Por favor, carga un archivo XML primero.";
                    var errTitle = (_localization != null && _localization.Exists("Messages.ErrorTitle"))
                        ? _localization.Get("Messages.ErrorTitle")
                        : "Error";
                    MessageBox.Show(noXmlMsg, errTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Establecer idioma en PdfGeneratorService
                if (_localization != null)
                {
                    PdfGeneratorService.SetLanguage(_localization.CurrentLanguage);
                }

                string rutaReal = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable.");
                var directorioReal = Path.GetDirectoryName(rutaReal) ?? throw new InvalidOperationException("No se pudo determinar el directorio del ejecutable.");
                var outputPath = Path.Combine(directorioReal, "Factura.pdf");
                PdfGeneratorService.GenerarFacturaPdf(factura, outputPath);
                var st2 = FindName("StatusText") as TextBlock;
                if (st2 != null) st2.Text = $"PDF generado: {outputPath}";
                var successMsg = (_localization != null && _localization.Exists("Messages.SuccessPdfGenerated"))
                    ? string.Format(_localization.Get("Messages.SuccessPdfGenerated"), outputPath)
                    : $"PDF generado: {outputPath}";
                var successTitle = (_localization != null && _localization.Exists("Messages.SuccessTitle"))
                    ? _localization.Get("Messages.SuccessTitle")
                    : "Éxito";
                MessageBox.Show(successMsg, successTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var msg = (_localization != null && _localization.Exists("Messages.ErrorGeneratingPdf"))
                    ? string.Format(_localization.Get("Messages.ErrorGeneratingPdf"), ex.Message)
                    : $"Error al generar el PDF: {ex.Message}";
                var title = (_localization != null && _localization.Exists("Messages.ErrorTitle"))
                    ? _localization.Get("Messages.ErrorTitle")
                    : "Error";
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
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
                var msg = (_localization != null && _localization.Exists("Messages.UnexpectedError"))
                    ? string.Format(_localization.Get("Messages.UnexpectedError"), ex.Message)
                    : $"No se pudo abrir la configuración: {ex.Message}";
                var title = (_localization != null && _localization.Exists("Messages.ErrorTitle"))
                    ? _localization.Get("Messages.ErrorTitle")
                    : "Error";
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
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
                        var lv2 = FindName("LvRecent") as ListView;
                        if (lv2 != null) lv2.Items.Add(item);
                    }
                }
                else
                {
                    var lv3 = FindName("LvRecent") as ListView;
                    if (lv3 != null)
                    {
                        var placeholderText = _localization?.Get("HomePage.NoRecentFiles") ?? "(No hay archivos recientes)";
                        var placeholder = new ListViewItem { Content = placeholderText, Tag = "PLACEHOLDER", IsEnabled = false };
                        lv3.Items.Add(placeholder);
                    }
                }
            }
            catch
            {
                // No bloquear la UI
            }
        }

        private void LvRecent_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var lv4 = FindName("LvRecent") as ListView;
                if (lv4 != null && lv4.SelectedItem is ListViewItem item && item.Tag is string path && File.Exists(path))
                {
                    factura = XmlReaderService.LeerFacturaDesdeXml(path);
                    var st3 = FindName("StatusText") as TextBlock;
                    if (st3 != null) st3.Text = _localization?.Get("HomePage.LoadedFile", Path.GetFileName(path)) ?? $"Archivo cargado: {Path.GetFileName(path)}";
                }
            }
            catch (Exception ex)
            {
                var msg = (_localization != null && _localization.Exists("Messages.ErrorLoadingXml"))
                    ? string.Format(_localization.Get("Messages.ErrorLoadingXml"), ex.Message)
                    : $"Error al cargar el archivo seleccionado: {ex.Message}";
                var title = (_localization != null && _localization.Exists("Messages.ErrorTitle"))
                    ? _localization.Get("Messages.ErrorTitle")
                    : "Error";
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadIcon()
        {
            try
            {
                Uri iconUri = new Uri("pack://application:,,,/Assets/icono.ico", UriKind.Absolute);
                BitmapImage iconImage = new BitmapImage(iconUri);
                var img = FindName("IconImg") as Image;
                if (img != null) img.Source = iconImage;
            }
            catch (Exception ex)
            {
                var msg = (_localization != null && _localization.Exists("Messages.ErrorLoadingXml"))
                    ? string.Format(_localization.Get("Messages.ErrorLoadingXml"), ex.Message)
                    : $"Error al cargar el ícono: {ex.Message}";
                var title = (_localization != null && _localization.Exists("Messages.ErrorTitle"))
                    ? _localization.Get("Messages.ErrorTitle")
                    : "Error";
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void OnMinimizeWindowClick(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        private void OnToggleMaximizeClick(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window == null) return;

            if (e.ClickCount == 2)
            {
                window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                e.Handled = true;
                return;
            }

            try
            {
                window.DragMove();
                e.Handled = true;
            }
            catch { }
        }
    }
}
