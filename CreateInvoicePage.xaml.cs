using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using FacturacionAlemana.Models;
using FacturacionAlemana.Services;
using System.Windows.Media;
using Wpf.Ui.Controls;

using WinMessageBox = System.Windows.MessageBox;
using WinMessageBoxButton = System.Windows.MessageBoxButton;
using WinTextBox = System.Windows.Controls.TextBox;
using WinButton = System.Windows.Controls.Button;
using WinTextBlock = System.Windows.Controls.TextBlock;
using WinDataGrid = System.Windows.Controls.DataGrid;

namespace FacturacionAlemana
{
    public partial class CreateInvoicePage : Page
    {
        private ObservableCollection<Producto> productos = new();
        private bool _updatingIban = false;
        private int _currentStepIndex = 0;
        private bool _windowStateAdjusted = false;
        private bool _showGlobalAlerts = false;
        private bool _alertsDismissed = false;
        private bool _allowProgrammaticStepJump = false;
        private LocalizationService? _localization;

        public CreateInvoicePage()
        {
            InitializeComponent();
            _localization = LocalizationService.Instance;
            _localization.LanguageChangedUI += (s, e) => UpdateUIText();            InvoiceNumberTextBox.Text = $"STR-{DateTime.Now.Year.ToString().Substring(2)}-";
            this.Loaded += OnCreateInvoicePageLoaded;
            ProductsDataGrid.ItemsSource = productos;
            IssueDatePicker.SelectedDate = DateTime.Now;
            DeliveryDatePicker.SelectedDate = null;
            DueDatePicker.SelectedDate = DateTime.Now.AddMonths(1);
            
            var currencyItems = new List<ComboBoxItem>
            {
                new ComboBoxItem { Content = "EUR", IsSelected = true },
                new ComboBoxItem { Content = "USD" },
                new ComboBoxItem { Content = "GBP" },
                new ComboBoxItem { Content = "CHF" }
            };
            CurrencyComboBox.ItemsSource = currencyItems;
            CurrencyComboBox.SelectedIndex = 0;
            
            TaxCategoryComboBox.ItemsSource = new List<string> { "S", "AA", "Z", "E", "O", "AE" };
            
            TaxCategoryComboBox.SelectionChanged += TaxCategoryComboBox_SelectionChanged;
            TaxRateTextBox.TextChanged += (s, e) => ActualizarResumenTotales();
            CurrencyComboBox.SelectionChanged += (s, e) => ActualizarResumenTotales();
            
            TaxCategoryComboBox.SelectedValue = "S";
            TaxRateTextBox.Text = "19.00";
            
            ActualizarResumenTotales();            // Validación en tiempo real
            HookRealtimeValidation();
            
            // Validar campos OBLIGATORIOS desde el inicio (solo vendedor)
            // Vendedor (Paso 1) - Solo campos obligatorios
            UpdateCountryValidation(SellerCountryTextBox);
            UpdateVatValidation();
            // Email y Postcode del vendedor son opcionales, NO validar aquí
            
            // Comprador (Paso 2) - Los campos del comprador se validan en tiempo real            
            RefreshAlerts(); // calcula pero no muestra hasta que _showGlobalAlerts sea true

            // Configurar Stepper
            StepsTabControl.SelectionChanged += StepsTabControl_SelectionChanged;
            UpdateStepButtons();
        }

        private void OnCreateInvoicePageLoaded(object sender, RoutedEventArgs e)
        {
            if (_windowStateAdjusted) return;
            var window = Window.GetWindow(this);
            if (window == null) return;

            if (window.WindowState == WindowState.Minimized)
            {
                EventHandler? handler = null;
                handler = (s, args) =>
                {
                    if (window.WindowState != WindowState.Minimized)
                    {
                        window.StateChanged -= handler;
                        window.WindowState = WindowState.Maximized;
                        _windowStateAdjusted = true;
                    }
                };
                window.StateChanged += handler;
                return;
            }

            window.WindowState = WindowState.Maximized;
            _windowStateAdjusted = true;

            // Actualizar la UI después de que el árbol visual esté construido
            UpdateUIText();
        }
        
        private void OnAddProductClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = ProductDescTextBox.Text?.Trim();
                var cantidadStr = ProductQtyTextBox.Text?.Trim();
                var precioStr = ProductPriceTextBox.Text?.Trim();

                if (string.IsNullOrEmpty(name) || 
                    string.IsNullOrEmpty(cantidadStr) || string.IsNullOrEmpty(precioStr))
                {
                    WinMessageBox.Show("Por favor, completa todos los campos del producto.", "Error", 
                        WinMessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Normalizar separador decimal para aceptar coma o punto
                var cantidadNorm = (cantidadStr ?? string.Empty).Replace(',', '.');
                var precioNorm = (precioStr ?? string.Empty).Replace(',', '.');

                if (!decimal.TryParse(cantidadNorm, NumberStyles.Any, CultureInfo.InvariantCulture, out var cantidad) ||
                    !decimal.TryParse(precioNorm, NumberStyles.Any, CultureInfo.InvariantCulture, out var precio))
                {
                    WinMessageBox.Show("La cantidad y precio deben ser números válidos.", "Error", 
                        WinMessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var producto = new Producto
                {
                    Pos = productos.Count + 1,
                    Name = name,
                    Descripcion = "",
                    Cantidad = cantidad,
                    Unit = "H87",
                    PrecioUnitario = precio,
                    PrecioTotal = cantidad * precio
                };

                productos.Add(producto);

                // Abrir ventana de detalles automáticamente
                var detallesWindow = new ProductDetailsWindow(producto);
                detallesWindow.Owner = Window.GetWindow(this);
                detallesWindow.ShowDialog();

                ProductsDataGrid.Items.Refresh();

                ProductDescTextBox.Clear();
                ProductQtyTextBox.Clear();
                ProductPriceTextBox.Clear();

                ActualizarResumenTotales();
                RefreshAlerts();
            }
            catch (Exception ex)
            {
                WinMessageBox.Show($"Error al agregar producto: {ex.Message}", "Error", 
                    WinMessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TaxCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TaxCategoryComboBox.SelectedItem is string selectedCategory)
            {
                if (selectedCategory == "S")
                {
                    TaxRateTextBox.Text = "19.00";
                }
                else if (selectedCategory == "AA")
                {
                    TaxRateTextBox.Text = "7.00";
                }
                else
                {
                    TaxRateTextBox.Text = "0.00";
                }
            }
        }

        private void EditProductDetails_Click(object sender, RoutedEventArgs e)
        {
            // Obtener el producto del DataGrid
            if (ProductsDataGrid.SelectedItem is Producto producto)
            {
                var detallesWindow = new ProductDetailsWindow(producto);
                detallesWindow.Owner = Window.GetWindow(this);
                if (detallesWindow.ShowDialog() == true)
                {
                    ProductsDataGrid.Items.Refresh();
                    ActualizarResumenTotales();
                    RefreshAlerts();
                }
            }
        }        
        private void ActualizarResumenTotales()
        {
            try
            {
                decimal subtotal = 0;
                foreach (var prod in productos)
                {
                    subtotal += prod.PrecioTotal;
                }                
                decimal tasaIVA = 19m; // Valor por defecto
                if (!string.IsNullOrEmpty(TaxRateTextBox.Text))
                {
                    var tasaTxt = (TaxRateTextBox.Text ?? string.Empty).Replace(',', '.');
                    decimal.TryParse(tasaTxt, NumberStyles.Any, CultureInfo.InvariantCulture, out tasaIVA);
                }

                decimal impuestos = subtotal * (tasaIVA / 100m);
                decimal total = subtotal + impuestos;

                var moneda = (CurrencyComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EUR";
                string simboloMoneda = ObtenerSimboloMoneda(moneda);

                SubtotalTextBlock.Text = $"{simboloMoneda}{subtotal:F2}";
                TaxesTextBlock.Text = $"{simboloMoneda}{impuestos:F2}";
                TotalTextBlock.Text = $"{simboloMoneda}{total:F2}";
                RefreshAlerts();
            }
            catch
            {
                // Ignorar errores de cálculo
            }
        }

        private string ObtenerSimboloMoneda(string codigo)
        {
            return codigo switch
            {
                "EUR" => "€",
                "USD" => "$",
                "GBP" => "£",
                "CHF" => "CHF ",
                _ => codigo + " "
            };
        }        
        private void OnGenerateInvoiceClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (productos.Count == 0)
                {
                    WinMessageBox.Show(_localization?.Get("CreateInvoicePage.Errors.AddAtLeastOneProduct"), "Validación", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(SellerNameTextBox.Text) ||
                    string.IsNullOrWhiteSpace(BuyerNameTextBox.Text) ||
                    string.IsNullOrWhiteSpace(InvoiceNumberTextBox.Text))
                {
                    WinMessageBox.Show("Por favor, completa los datos del vendedor, comprador y número de factura.", 
                        "Error", WinMessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Validación de VAT-ID
                if (string.IsNullOrWhiteSpace(SellerVATTextBox.Text) || SellerVATTextBox.Text.Length < 5)
                {
                    WinMessageBox.Show("⚠️ VAT-ID del vendedor es inválido o está vacío.\n\nFormato esperado:\n" +
                        "- Alemania (DE): DExxxxxxxxx (11 dígitos)\n" +
                        "- España (ES): ESxxxxxxxxx (8 caracteres)\n\n" +
                        "Ejemplo: DE123456789", "Validación VAT-ID", 
                        WinMessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validación de IBAN
                if (string.IsNullOrWhiteSpace(IBANTextBox.Text))
                {
                    WinMessageBox.Show("⚠️ IBAN no puede estar vacío.", "Validación IBAN", 
                        WinMessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var ibanLimpio = IBANTextBox.Text.Replace(" ", "").ToUpper();
                if (ibanLimpio.Length != 22)
                {
                    WinMessageBox.Show($"⚠️ IBAN inválido.\n\n" +
                        $"Longitud actual: {ibanLimpio.Length} caracteres\n" +
                        $"Longitud requerida: 22 caracteres (sin espacios)\n\n" +
                        $"Formato esperado: DE89400900505012345678", "Validación IBAN", 
                        WinMessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }                
                // Validar VAT-ID e IBAN antes de generar la factura
                if (!ValidarDatosFactura())
                {
                    return;
                }

                string invoiceNumber = InvoiceNumberTextBox.Text.Trim();

                var factura = CrearFacturaDesdeFormulario();

                string rutaReal = Process.GetCurrentProcess().MainModule?.FileName ?? 
                    throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable.");
                var directorioReal = Path.GetDirectoryName(rutaReal) ?? 
                    throw new InvalidOperationException("No se pudo determinar el directorio del ejecutable.");
                  string fileNameBase = invoiceNumber;
                var pdfPath = Path.Combine(directorioReal, $"{fileNameBase}.pdf");
                var xmlPath = Path.Combine(directorioReal, $"{fileNameBase}.xml");

                // Establecer el idioma actual para el PDF
                PdfGeneratorService.SetLanguage(_localization?.CurrentLanguage ?? "es");
                PdfGeneratorService.GenerarFacturaPdf(factura, pdfPath);
                XmlGeneratorService.GenerarFacturaXml(factura, xmlPath);

                WinMessageBox.Show($"Factura generada exitosamente.\n\nPDF: {pdfPath}\nXML: {xmlPath}", 
                    "Éxito", WinMessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WinMessageBox.Show($"Error al generar factura: {ex.Message}", "Error", 
                    WinMessageBoxButton.OK, MessageBoxImage.Error);
            }
        }        
        private bool ValidarDatosFactura()
        {
            // Validar código de país del Vendedor (ISO 3166-1)
            string sellerCountry = SellerCountryTextBox.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(sellerCountry) || sellerCountry.Length != 2)
            {
                WinMessageBox.Show("El código de país del vendedor debe ser un código ISO 3166-1 de 2 letras.\n" +
                    "Ejemplos válidos:\n- DE (Alemania)\n- ES (España)\n- FR (Francia)\n- CO (Colombia)",
                    "Validación de País del Vendedor", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!sellerCountry.All(char.IsLetter))
            {
                WinMessageBox.Show("El código de país del vendedor debe contener solo letras.\n" +
                    "Ejemplos válidos: DE, ES, FR, CO",
                    "Validación de País del Vendedor", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validar código de país del Comprador (ISO 3166-1)
            string buyerCountry = BuyerCountryTextBox.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(buyerCountry) || buyerCountry.Length != 2)
            {
                WinMessageBox.Show("El código de país del comprador debe ser un código ISO 3166-1 de 2 letras.\n" +
                    "Ejemplos válidos:\n- DE (Alemania)\n- ES (España)\n- FR (Francia)\n- CO (Colombia)",
                    "Validación de País del Comprador", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!buyerCountry.All(char.IsLetter))
            {
                WinMessageBox.Show("El código de país del comprador debe contener solo letras.\n" +
                    "Ejemplos válidos: DE, ES, FR, CO",
                    "Validación de País del Comprador", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }            
            // Validar VAT-ID
            string vatID = SellerVATTextBox.Text.Trim();
            if (string.IsNullOrEmpty(vatID))
            {
                WinMessageBox.Show("El número de IVA (VAT-ID) es obligatorio.", "Validación", 
                    WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // VAT-ID debe tener formato válido según el país del vendedor
            if (!ValidarVATIDPorPais(vatID, sellerCountry))
            {
                return false;
            }

            // Validar IBAN
            string iban = IBANTextBox.Text.Trim().Replace(" ", "");
            if (string.IsNullOrEmpty(iban))
            {
                WinMessageBox.Show("El IBAN es obligatorio.", "Validación", 
                    WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // IBAN debe tener exactamente 22 caracteres sin espacios
            if (iban.Length != 22)
            {
                WinMessageBox.Show($"El IBAN debe tener exactamente 22 caracteres sin espacios.\n" +
                    $"Tu IBAN tiene {iban.Length} caracteres.\n\n" +
                    $"Formato correcto: Código país (2) + Dígitos de control (2) + Código banco + Número cuenta\n" +
                    $"Ejemplo válido: DE89400900505012345678",
                    "Validación de IBAN", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // IBAN debe comenzar con 2 letras (código país)
            if (!char.IsLetter(iban[0]) || !char.IsLetter(iban[1]))
            {
                WinMessageBox.Show("El IBAN debe comenzar con un código de país de 2 letras (ej: DE, ES, FR).",
                    "Validación de IBAN", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }            
            // El resto del IBAN debe ser numérico
            if (!iban.Substring(2).All(char.IsDigit))
            {
                WinMessageBox.Show("Después del código de país, el IBAN solo debe contener números.",
                    "Validación de IBAN", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validar BuyerReference (obligatorio según BR-DE-15)
            string buyerReference = BuyerReferenceTextBox.Text.Trim();
            if (string.IsNullOrEmpty(buyerReference))
            {
                WinMessageBox.Show("La referencia del comprador (BT-10) es obligatoria según la norma XRechnung.\n\n" +
                    "Ingresa un valor como:\n- REF-12345\n- PO-2025-001\n- ORDEN-123",
                    "Validación de Referencia del Comprador", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validar campos obligatorios del comprador según XRechnung/EN 16931
            string buyerCity = BuyerCityTextBox.Text.Trim();
            if (string.IsNullOrEmpty(buyerCity))
            {
                WinMessageBox.Show("La ciudad del comprador (BT-52) es obligatoria según la norma XRechnung.\n\n" +
                    "Ingresa la ciudad del comprador.",
                    "Validación de Ciudad del Comprador", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string buyerPostcode = BuyerPostcodeTextBox.Text.Trim();
            if (string.IsNullOrEmpty(buyerPostcode))
            {
                WinMessageBox.Show("El código postal del comprador (BT-53) es obligatorio según la norma XRechnung.\n\n" +
                    "Ingresa el código postal del comprador.",
                    "Validación de Código Postal del Comprador", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string buyerEmail = BuyerEmailTextBox.Text.Trim();
            if (string.IsNullOrEmpty(buyerEmail))
            {
                WinMessageBox.Show("El email del comprador es obligatorio según PEPPOL-EN16931-R010.\n\n" +
                    "Ingresa el email electrónico del comprador.",
                    "Validación de Email del Comprador", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validar formato de email básico
            if (!buyerEmail.Contains("@") || !buyerEmail.Contains("."))
            {
                WinMessageBox.Show("El email del comprador debe tener un formato válido.\n\n" +
                    "Ejemplo: nombre@empresa.com",
                    "Validación de Email del Comprador", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private Factura CrearFacturaDesdeFormulario()
        {
            decimal totalGeneral = 0;
            foreach (var prod in productos)
            {
                totalGeneral += prod.PrecioTotal;
            }            
            decimal tasaIVA = 19m; // Valor por defecto
            if (!string.IsNullOrEmpty(TaxRateTextBox.Text))
            {
                var tasaTxt = (TaxRateTextBox.Text ?? string.Empty).Replace(',', '.');
                decimal.TryParse(tasaTxt, NumberStyles.Any, CultureInfo.InvariantCulture, out tasaIVA);
            }
            
            decimal totalImpuestos = totalGeneral * (tasaIVA / 100m);
            decimal totalConImpuestos = totalGeneral + totalImpuestos;

            var monedaSeleccionada = (CurrencyComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EUR";
            var categoriaImpuesto = TaxCategoryComboBox.SelectedItem as string ?? "S";            
            return new Factura            
            {
                Cliente = BuyerNameTextBox.Text,
                Total = totalConImpuestos,
                DueDate = DueDatePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.AddMonths(1).ToString("yyyy-MM-dd"),
                GrandTotalAmount = totalConImpuestos.ToString("F2", CultureInfo.InvariantCulture),                DuePayableAmount = totalConImpuestos.ToString("F2", CultureInfo.InvariantCulture),
                DateTimeFormat = "102",
                UnitCode = "EA",
                SchemeID = "0060",
                CurrencyID = monedaSeleccionada,
                IdElement = InvoiceNumberTextBox.Text,
                TypeCodeElement = "380",
                // Convertir fecha de yyyy-MM-dd a YYYYMMDD (formato requerido por EN 16931)
                IssueDateElement = IssueDatePicker.SelectedDate?.ToString("yyyyMMdd") ?? DateTime.Now.ToString("yyyyMMdd"),
                PaymentNoteElement = GeneralNoteTextBox.Text,
                TaxAmount = totalImpuestos.ToString("F2", CultureInfo.InvariantCulture),
                
                SellerName = SellerNameTextBox.Text,
                SellerPersonName = SellerPersonNameTextBox.Text,
                SellerDepartmentName = "Ventas",
                SellerCompleteNumber = SellerPhoneTextBox.Text,                
                SellerEmail = SellerEmailTextBox.Text,
                SellerPostcodeCode = SellerPostcodeTextBox.Text,
                SellerLineOne = SellerStreetTextBox.Text,
                SellerLineTwo = SellerStreetTextBox.Text,
                SellerCityName = SellerCityTextBox.Text,
                SellerCountryID = SellerCountryTextBox.Text.Trim().ToUpper(),
                SellerVATID = SellerVATTextBox.Text,
                SellerTaxNumber = SellerTaxTextBox.Text,
                  BuyerReference = BuyerReferenceTextBox.Text.Trim(),
                BuyerID = "1",
                BuyerName = BuyerNameTextBox.Text,
                BuyerPersonName = BuyerPersonNameTextBox.Text,
                BuyerCompleteNumber = BuyerPhoneTextBox.Text,
                BuyerEmail = BuyerEmailTextBox.Text,
                BuyerPostcodeCode = BuyerPostcodeTextBox.Text,
                BuyerLineOne = BuyerStreetTextBox.Text,
                BuyerLineTwo = BuyerStreet2TextBox.Text,
                BuyerCityName = BuyerCityTextBox.Text,
                BuyerCountryID = BuyerCountryTextBox.Text.Trim().ToUpper(),
                BuyerVATID = BuyerVATTextBox.Text,
                BuyerEmailContact = BuyerEmailContactTextBox.Text,
                
                LineID = productos[0].Pos.ToString(),
                SellerAssignedID = productos[0].Pos.ToString(),
                ProductName = productos[0].Descripcion,
                ChargeAmount = productos[0].PrecioTotal.ToString("F2", CultureInfo.InvariantCulture),
                BilledQuantity = productos[0].Cantidad.ToString("F2", CultureInfo.InvariantCulture),
                TaxTypeCode = "VAT",
                TaxCategoryCode = categoriaImpuesto,                
                TaxRatePercent = tasaIVA.ToString("F0"),
                LineTotalAmount = productos[0].PrecioTotal.ToString("F2", CultureInfo.InvariantCulture),
                InvoiceCurrencyCode = monedaSeleccionada,                
                PaymentTypeCode = "30",                PaymentInformation = "SEPA",                IBANID = IBANTextBox.Text.Replace(" ", "").ToUpper(), // Remover espacios y convertir a mayúsculas
                AccountName = AccountNameTextBox.Text,
                BankName = BankNameTextBox.Text,
                BLZ = BLZTextBox.Text,
                BICID = BICTextBox.Text,CalculatedAmount = totalImpuestos.ToString("F2", CultureInfo.InvariantCulture), // Monto del IVA (no el total)
                BasisAmount = totalGeneral.ToString("F2", CultureInfo.InvariantCulture), // Base imponible (neto sin impuestos)
                PaymentDescription = string.Empty, // Sin descripción adicional
                  InvoiceNumber = InvoiceNumberTextBox.Text,
                IssueDate = IssueDatePicker.SelectedDate ?? DateTime.Now,
                DeliveryDate = DeliveryDatePicker.SelectedDate ?? default(DateTime),
                DueDateValue = DueDatePicker.SelectedDate ?? DateTime.Now.AddMonths(1),
                ProjectNumber = ProjectNumberTextBox.Text,
                ContractNumber = ContractNumberTextBox.Text,
                PurchaseOrderNumber = PurchaseOrderNumberTextBox.Text,
                SalesOrderNumber = SalesOrderNumberTextBox.Text,
                PaymentReference = PaymentReferenceTextBox.Text,
                  Productos = new List<Producto>(productos),
                ShipToID = ShipToIDTextBox.Text,
                ShipToName = ShipToNameTextBox.Text,
                ShipToPostcodeCode = ShipToPostcodeCodeTextBox.Text,
                ShipToLineOne = ShipToLineOneTextBox.Text,
                ShipToLineTwo = ShipToLineTwoTextBox.Text,
                ShipToLineThree = ShipToLineThreeTextBox.Text,
                ShipToCityName = ShipToCityNameTextBox.Text,                ShipToCountryID = ShipToCountryIDTextBox.Text,
                ShipToCountrySubDivisionName = ShipToCountrySubDivisionNameTextBox.Text,
                GeneralNote = GeneralNoteTextBox.Text,
                PaymentTermsDescription = PaymentTermsDescriptionTextBox.Text.Replace("{Total}", totalConImpuestos.ToString("F2", CultureInfo.InvariantCulture)).Replace("{DueDate}", DueDatePicker.SelectedDate?.ToString("dd.MM.yyyy") ?? DateTime.Now.AddMonths(1).ToString("dd.MM.yyyy"))
            };
        }        
        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            // Salir de la vista de creación: volver atrás o cerrar ventana
            if (NavigationService != null && NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
                return;
            }
            var window = Window.GetWindow(this);
            window?.Close();
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

        private void OnCloseWindowClick(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            window?.Close();
        }

        private void CreateInvoiceHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsDataGrid?.SelectedItem is Producto p)
            {
                int idx = productos.IndexOf(p);
                if (idx > 0)
                {
                    productos.Move(idx, idx - 1);
                    RenumerarPosiciones();
                    ProductsDataGrid.SelectedItem = p;
                    ActualizarResumenTotales();
                    RefreshAlerts();
                }
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsDataGrid?.SelectedItem is Producto p)
            {
                int idx = productos.IndexOf(p);
                if (idx >= 0 && idx < productos.Count - 1)
                {
                    productos.Move(idx, idx + 1);
                    RenumerarPosiciones();
                    ProductsDataGrid.SelectedItem = p;
                    ActualizarResumenTotales();
                    RefreshAlerts();
                }
            }
        }

        private void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsDataGrid?.SelectedItem is Producto p)
            {
                var copy = new Producto
                {
                    Pos = p.Pos + 1,
                    Name = p.Name,
                    Descripcion = p.Descripcion,
                    Cantidad = p.Cantidad,
                    Unit = p.Unit,
                    PrecioUnitario = p.PrecioUnitario,
                    PrecioTotal = p.PrecioTotal
                };
                int idx = productos.IndexOf(p);
                productos.Insert(Math.Min(idx + 1, productos.Count), copy);
                RenumerarPosiciones();
                ActualizarResumenTotales();
                RefreshAlerts();
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsDataGrid?.SelectedItem is Producto p)
            {
                productos.Remove(p);
                RenumerarPosiciones();
                ActualizarResumenTotales();
                RefreshAlerts();
            }
        }

        private void ProductsDataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                Delete_Click(sender, new RoutedEventArgs());
            }
            else if (e.Key == Key.F2)
            {
                EditProductDetails_Click(sender, new RoutedEventArgs());
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Up)
            {
                MoveUp_Click(sender, new RoutedEventArgs());
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Down)
            {
                MoveDown_Click(sender, new RoutedEventArgs());
            }
        }

        private void AddProduct_Enter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OnAddProductClick(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void FormatTwoDecimals_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is WinTextBox tb)
            {
                var txt = (tb.Text ?? string.Empty).Replace(',', '.');
                if (decimal.TryParse(txt, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                {
                    tb.Text = value.ToString("0.00", CultureInfo.InvariantCulture);
                }
            }
        }

        private void RenumerarPosiciones()
        {
            for (int i = 0; i < productos.Count; i++) productos[i].Pos = i + 1;
            ProductsDataGrid?.Items.Refresh();
        }

        private void ShowAlerts(params string[] messages)
        {
            int count = messages?.Length ?? 0;
            bool has = count > 0;
            if (TopAlertsPanel != null)
            {
                // Si el usuario las ocultó, sólo re-mostrar si cambió el conjunto de errores
                if (_alertsDismissed && has)
                {
                    // Si el usuario ha ocultado, mantener oculto hasta que intenten avanzar o cambie el conteo
                    if (_lastErrorCount == count && !_showGlobalAlerts)
                    {
                        TopAlertsPanel.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        TopAlertsPanel.Visibility = (_showGlobalAlerts && has) ? Visibility.Visible : Visibility.Collapsed;
                        _alertsDismissed = false;
                    }
                }
                else
                {
                    TopAlertsPanel.Visibility = (_showGlobalAlerts && has) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            if (TopAlertsItems != null)
            {
                TopAlertsItems.ItemsSource = has ? messages : null;
            }
            if (GenerateButton != null)
            {
                // Siempre deshabilitar Generar si hay errores, aunque no se muestren aún
                GenerateButton.IsEnabled = !has;
            }
            if (ErrorCounterTextBlock != null)
            {
                if (_showGlobalAlerts && has)
                {
                    ErrorCounterTextBlock.Text = $"{count} error(es)";
                    ErrorCounterTextBlock.Visibility = Visibility.Visible;
                }
                else
                {
                    ErrorCounterTextBlock.Text = string.Empty;
                    ErrorCounterTextBlock.Visibility = Visibility.Collapsed;
                }
            }
            _lastErrorCount = count;
        }

        private int _lastErrorCount = 0;

        private void OnDismissAlertsClick(object sender, RoutedEventArgs e)
        {
            _alertsDismissed = true;
            TopAlertsPanel.Visibility = Visibility.Collapsed;
        }        private void OnGoToFirstErrorClick(object sender, RoutedEventArgs e)
        {
            // Activar visualización de alertas y refrescar
            _showGlobalAlerts = true;
            RefreshAlerts();

            // Refrescar validaciones por si cambió algo (en orden de pasos)
            // Paso 0: Vendedor
            UpdateCountryValidation(SellerCountryTextBox);
            UpdateVatValidation();
            UpdatePostcodeValidation(SellerPostcodeTextBox, SellerCountryTextBox);
            UpdateEmailValidation(SellerEmailTextBox);

            // Paso 1: Comprador
            if (string.IsNullOrWhiteSpace(BuyerNameTextBox.Text))
                SetError(BuyerNameTextBox, "Obligatorio");
            else
                SetError(BuyerNameTextBox, null);
            UpdateCountryValidation(BuyerCountryTextBox);
            UpdatePostcodeValidation(BuyerPostcodeTextBox, BuyerCountryTextBox);
            UpdateBuyerCityValidation();
            UpdateBuyerEmailValidation();

            // Paso 3: Pagos
            UpdateBuyerRefValidation();
            UpdateIbanValidation();

            // Definir controles por paso en el orden correcto: 0 (Vendedor) → 1 (Comprador) → 2 (Productos) → 3 (Pagos)
            var stepControls = new List<(int step, Control ctl, Func<bool> isError)>
            {
                // Paso 0: Vendedor
                (0, SellerNameTextBox, () => string.IsNullOrWhiteSpace(SellerNameTextBox.Text)),
                (0, SellerCountryTextBox, () => HasError(SellerCountryTextBox)),
                (0, SellerVATTextBox, () => HasError(SellerVATTextBox)),
                (0, SellerPostcodeTextBox, () => HasError(SellerPostcodeTextBox)),
                (0, SellerEmailTextBox, () => HasError(SellerEmailTextBox)),

                // Paso 1: Comprador (nombre, email, código postal, ciudad, país)
                (1, BuyerNameTextBox, () => string.IsNullOrWhiteSpace(BuyerNameTextBox.Text)),
                (1, BuyerEmailTextBox, () => HasError(BuyerEmailTextBox)),
                (1, BuyerPostcodeTextBox, () => HasError(BuyerPostcodeTextBox)),
                (1, BuyerCityTextBox, () => HasError(BuyerCityTextBox)),
                (1, BuyerCountryTextBox, () => HasError(BuyerCountryTextBox)),

                // Paso 2: Productos/Líneas (mínimo 1 producto)
                (2, ProductsDataGrid, () => productos.Count == 0),

                // Paso 3: Pagos (BT-10 y IBAN)
                (3, BuyerReferenceTextBox, () => HasError(BuyerReferenceTextBox)),
                (3, IBANTextBox, () => HasError(IBANTextBox)),
            };

            foreach (var (step, ctl, isError) in stepControls)
            {
                if (isError())
                {
                    _allowProgrammaticStepJump = true;
                    StepsTabControl.SelectedIndex = step;
                    _currentStepIndex = step;
                    UpdateStepButtons();                    // Enfocar y asegurar visibilidad
                    this.Dispatcher.InvokeAsync(() =>
                    {
                        // Expandir Expanders padres si es necesario
                        ExpandParentExpanders(ctl);
                        
                        ctl.BringIntoView();
                        ctl.Focus();
                        if (ctl is WinTextBox tbox)
                            tbox.SelectAll();
                    });
                    return;
                }
            }
        }        /// <summary>
        /// Expande todos los Expander controls que son ancestros del control especificado.
        /// </summary>
        private void ExpandParentExpanders(Control ctl)
        {
            DependencyObject parent = LogicalTreeHelper.GetParent(ctl);
            while (parent != null)
            {
                if (parent is Expander expander)
                {
                    expander.IsExpanded = true;
                }
                parent = LogicalTreeHelper.GetParent(parent);
            }
        }

        private void RefreshAlerts()
        {
            var errors = new List<string>();

            // ===== PASO 0: VENDEDOR =====
            if (string.IsNullOrWhiteSpace(SellerNameTextBox.Text)) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.SellerNameRequired"));
            if ((SellerCountryTextBox.Text ?? string.Empty).Trim().Length != 2) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.SellerCountryISO2"));
            if (!EsVATValidoSilencioso((SellerVATTextBox.Text ?? string.Empty).Trim(), (SellerCountryTextBox.Text ?? string.Empty).Trim())) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.SellerVATInvalid"));
            if (!string.IsNullOrEmpty(SellerPostcodeTextBox.Text) && !IsPostcodeValidForCountry(SellerPostcodeTextBox.Text.Trim(), (SellerCountryTextBox.Text ?? string.Empty).Trim().ToUpper())) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.SellerPostcodeInvalid"));
            if (!string.IsNullOrEmpty(SellerEmailTextBox.Text) && !Regex.IsMatch(SellerEmailTextBox.Text.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.SellerEmailInvalid"));

            // ===== PASO 1: COMPRADOR =====
            if (string.IsNullOrWhiteSpace(BuyerNameTextBox.Text)) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.BuyerNameRequired"));
            var buyerEmail = (BuyerEmailTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(buyerEmail)) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.BuyerEmailRequired"));
            else if (!Regex.IsMatch(buyerEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.BuyerEmailInvalid"));
            var buyerCity = (BuyerCityTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(buyerCity)) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.BuyerCityRequired"));
            var buyerPostcode = (BuyerPostcodeTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(buyerPostcode)) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.BuyerPostcodeRequired"));
            else if (!IsPostcodeValidForCountry(buyerPostcode, (BuyerCountryTextBox.Text ?? string.Empty).Trim().ToUpper())) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.BuyerPostcodeInvalid"));
            if ((BuyerCountryTextBox.Text ?? string.Empty).Trim().Length != 2) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.BuyerCountryISO2"));

            // ===== PASO 2: PRODUCTOS =====
            if (productos.Count == 0) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.ProductsAtLeastOne"));

            // ===== PASO 3: PAGOS =====
            var bt10 = (BuyerReferenceTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(bt10)) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.PaymentBuyerRefRequired"));
            var iban = (IBANTextBox.Text ?? string.Empty).Replace(" ", string.Empty);
            if (string.IsNullOrEmpty(iban)) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.PaymentIBANRequired"));
            else if (iban.Length != 22) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.PaymentIBANLength"));
            else if (!IsValidIbanMod97(iban)) 
                errors.Add(_localization.Get("CreateInvoicePage.Errors.PaymentIBANInvalid"));

            ShowAlerts(errors.ToArray());
        }

        private void HookRealtimeValidation()
        {
            SellerVATTextBox.TextChanged += (_, __) => { UpdateVatValidation(); RefreshAlerts(); };
            SellerCountryTextBox.TextChanged += (_, __) => { UpdateCountryValidation(SellerCountryTextBox); UpdateVatValidation(); UpdatePostcodeValidation(SellerPostcodeTextBox, SellerCountryTextBox); RefreshAlerts(); };
            BuyerCountryTextBox.TextChanged += (_, __) => { 
                if (string.IsNullOrWhiteSpace(BuyerNameTextBox.Text))
                    SetError(BuyerNameTextBox, "Obligatorio");
                UpdateCountryValidation(BuyerCountryTextBox); 
                UpdatePostcodeValidation(BuyerPostcodeTextBox, BuyerCountryTextBox); 
                RefreshAlerts(); 
            };
            BuyerReferenceTextBox.TextChanged += (_, __) => { UpdateBuyerRefValidation(); RefreshAlerts(); };
            BuyerCityTextBox.TextChanged += (_, __) => { UpdateBuyerCityValidation(); RefreshAlerts(); };
            BuyerPostcodeTextBox.TextChanged += (_, __) => { UpdateBuyerPostcodeValidation(); RefreshAlerts(); };
            BuyerEmailTextBox.TextChanged += (_, __) => { UpdateBuyerEmailValidation(); RefreshAlerts(); };
            // IBAN ya tiene TextChanged para máscara; validación se invoca al final del handler            // Email / Teléfono
            SellerEmailTextBox.TextChanged += (_, __) => { UpdateEmailValidation(SellerEmailTextBox); RefreshAlerts(); };
            BuyerEmailContactTextBox.TextChanged += (_, __) => { UpdateEmailValidation(BuyerEmailContactTextBox); RefreshAlerts(); };
            SellerPhoneTextBox.TextChanged += (_, __) => { UpdatePhoneValidation(SellerPhoneTextBox); RefreshAlerts(); };
            BuyerPhoneTextBox.TextChanged += (_, __) => { UpdatePhoneValidation(BuyerPhoneTextBox); RefreshAlerts(); };
        }        private void SetError(Control ctl, string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ctl.ClearValue(Control.BorderBrushProperty);
                ctl.ToolTip = null;
            }
            else
            {
                ctl.BorderBrush = Brushes.Red;
                ctl.ToolTip = message;
            }
        }

        private void UpdateCountryValidation(Wpf.Ui.Controls.TextBox tb)
        {
            var txt = (tb.Text ?? string.Empty).Trim().ToUpper();
            if (_localization == null) return;
            if (txt.Length == 0)
            {
                SetError(tb, _localization.Get("CreateInvoicePage.Errors.RequiredCountryCode"));
            }
            else if (txt.Length != 2)
            {
                SetError(tb, _localization.Get("CreateInvoicePage.Errors.CountryMustBe2Letters"));
            }
            else
            {
                SetError(tb, null);
            }
        }

        private void UpdateBuyerRefValidation()
        {
            var txt = (BuyerReferenceTextBox.Text ?? string.Empty).Trim();
            SetError(BuyerReferenceTextBox, string.IsNullOrEmpty(txt) ? _localization?.Get("CreateInvoicePage.Errors.RequiredBT10") : null);
        }

        private void UpdateBuyerCityValidation()
        {
            var txt = (BuyerCityTextBox.Text ?? string.Empty).Trim();
            SetError(BuyerCityTextBox, string.IsNullOrEmpty(txt) ? _localization?.Get("CreateInvoicePage.Errors.RequiredBT52") : null);
        }

        private void UpdateBuyerPostcodeValidation()
        {
            var txt = (BuyerPostcodeTextBox.Text ?? string.Empty).Trim();
            SetError(BuyerPostcodeTextBox, string.IsNullOrEmpty(txt) ? _localization?.Get("CreateInvoicePage.Errors.RequiredBT53") : null);
        }

        private void UpdateBuyerEmailValidation()
        {
            var txt = (BuyerEmailTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(txt))
            {
                SetError(BuyerEmailTextBox, _localization?.Get("CreateInvoicePage.Errors.RequiredPEPPOL"));
                return;
            }
            if (!Regex.IsMatch(txt, @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$"))
            {
                SetError(BuyerEmailTextBox, _localization?.Get("CreateInvoicePage.Errors.InvalidEmailFormat"));
                return;
            }
            SetError(BuyerEmailTextBox, null);
        }

        private void UpdateVatValidation()
        {
            var country = (SellerCountryTextBox.Text ?? string.Empty).Trim().ToUpper();
            var vat = (SellerVATTextBox.Text ?? string.Empty).Trim().ToUpper();
            if (string.IsNullOrEmpty(vat))
            {
                SetError(SellerVATTextBox, _localization?.Get("CreateInvoicePage.Errors.Required"));
                return;
            }
            if (country.Length != 2)
            {
                SetError(SellerVATTextBox, _localization?.Get("CreateInvoicePage.Errors.CompleteCountryFirst"));
                return;
            }
            SetError(SellerVATTextBox, EsVATValidoSilencioso(vat, country) ? null : (_localization != null ? string.Format(_localization.Get("CreateInvoicePage.Errors.InvalidVATFormat"), country) : "Formato VAT no válido para " + country));
        }

        private void UpdateIbanValidation()
        {
            var txt = (IBANTextBox.Text ?? string.Empty).Replace(" ", string.Empty).ToUpper();
            if (string.IsNullOrEmpty(txt)) { SetError(IBANTextBox, _localization?.Get("CreateInvoicePage.Errors.Required")); return; }
            if (txt.Length != 22) { SetError(IBANTextBox, _localization?.Get("CreateInvoicePage.Errors.IBANDE22Chars")); return; }
            if (!IsValidIbanMod97(txt)) { SetError(IBANTextBox, _localization?.Get("CreateInvoicePage.Errors.IBANMod97Failed")); return; }
            SetError(IBANTextBox, null);
        }

        private bool IsValidIbanMod97(string iban)
        {
            if (iban.Length < 4) return false;
            // Mover los 4 primeros al final
            string rearranged = iban.Substring(4) + iban.Substring(0, 4);
            // Reemplazar letras por números (A=10..Z=35)
            var sb = new System.Text.StringBuilder();
            foreach (char c in rearranged)
            {
                if (char.IsLetter(c)) sb.Append((c - 'A' + 10).ToString());
                else sb.Append(c);
            }
            // Calcular mod 97 de forma incremental para evitar overflow
            int mod = 0;
            foreach (char ch in sb.ToString())
            {
                int digit = ch - '0';
                if (digit < 0 || digit > 9) return false;
                mod = (mod * 10 + digit) % 97;
            }
            return mod == 1;
        }

        private void UpdateEmailValidation(Wpf.Ui.Controls.TextBox tb)
        {
            var v = (tb.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(v)) { SetError(tb, null); return; }
            // Regex simple y segura
            bool ok = Regex.IsMatch(v, @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$");
            SetError(tb, ok ? null : _localization?.Get("CreateInvoicePage.Errors.InvalidEmail"));
        }

        private void UpdatePhoneValidation(Wpf.Ui.Controls.TextBox tb)
        {
            var v = (tb.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(v)) { SetError(tb, null); return; }
            // Permitir +, espacios, guiones y dígitos, longitud mínima 7
            bool ok = Regex.IsMatch(v, @"^[+\d][\d\-\s()]{6,}$");
            SetError(tb, ok ? null : _localization?.Get("CreateInvoicePage.Errors.InvalidPhone"));
        }

        private void UpdatePostcodeValidation(Wpf.Ui.Controls.TextBox zipTb, Wpf.Ui.Controls.TextBox countryTb)
        {
            var zip = (zipTb.Text ?? string.Empty).Trim();
            var country = (countryTb.Text ?? string.Empty).Trim().ToUpper();
            if (string.IsNullOrEmpty(zip)) return; // No cambiar el error existente si está vacío
            bool ok = IsPostcodeValidForCountry(zip, country);
            SetError(zipTb, ok ? null : (_localization != null ? string.Format(_localization.Get("CreateInvoicePage.Errors.InvalidPostcode"), country) : "CP inválido para " + country));
        }

        private bool IsPostcodeValidForCountry(string zip, string country)
        {
            return country switch
            {
                "DE" => Regex.IsMatch(zip, @"^\d{5}$"),
                "ES" => Regex.IsMatch(zip, @"^\d{5}$"),
                "FR" => Regex.IsMatch(zip, @"^\d{5}$"),
                _ => zip.Length >= 3 // fallback laxo
            };
        }

        private bool EsVATValidoSilencioso(string vatID, string countryCode)
        {
            vatID = vatID.Trim().ToUpper();
            countryCode = countryCode.Trim().ToUpper();
            if (!vatID.StartsWith(countryCode)) return false;
            switch (countryCode)
            {
                case "DE":
                    return vatID.Length == 11 && vatID.Substring(2).All(char.IsDigit);
                case "ES":
                    return vatID.Length == 10 && char.IsLetter(vatID[2]) && vatID.Substring(3).All(char.IsDigit);
                case "FR":
                    return vatID.Length == 13 && vatID.Substring(2).All(char.IsDigit);
                case "AT":
                    return vatID.Length == 10 && vatID.Substring(2).All(char.IsDigit);
                case "NL":
                    return vatID.Length == 14 && vatID.Substring(2).All(char.IsDigit);
                case "IT":
                    return vatID.Length == 13 && vatID.Substring(2).All(char.IsDigit);
                case "BE":
                    return vatID.Length == 12 && vatID.Substring(2).All(char.IsDigit);
                case "PL":
                    return vatID.Length == 12 && vatID.Substring(2).All(char.IsDigit);
                case "CZ":
                    return (vatID.Length == 10 || vatID.Length == 12) && vatID.Substring(2).All(char.IsDigit);
                case "HU":
                    return vatID.Length == 10 && vatID.Substring(2).All(char.IsDigit);
                default:
                    return vatID.Length >= 5;
            }
        }

        // Evitar saltar pasos seleccionando pestañas manualmente, pero permitir volver atrás
        private void StepsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StepsTabControl == null) return;
            var targetIndex = StepsTabControl.SelectedIndex;

            // Permitir saltos programáticos (por "Ir al primer error")
            if (_allowProgrammaticStepJump)
            {
                _currentStepIndex = targetIndex;
                _allowProgrammaticStepJump = false;
                UpdateStepButtons();
                return;
            }

            if (targetIndex <= _currentStepIndex)
            {
                _currentStepIndex = targetIndex;
                UpdateStepButtons();
                return;
            }

            // Bloquear avances por click; usar botón Siguiente para validar
            StepsTabControl.SelectionChanged -= StepsTabControl_SelectionChanged;
            StepsTabControl.SelectedIndex = _currentStepIndex;
            StepsTabControl.SelectionChanged += StepsTabControl_SelectionChanged;
        }

        private void PrevStep_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStepIndex > 0)
            {
                _currentStepIndex--;
                StepsTabControl.SelectedIndex = _currentStepIndex;
                UpdateStepButtons();
            }
        }

        private void NextStep_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateCurrentStep())
            {
                _showGlobalAlerts = true; // mostrar aviso a partir del primer fallo
                RefreshAlerts();
                return;
            }
            // Si es el último paso, ejecutar generación
            if (_currentStepIndex == StepsTabControl.Items.Count - 1)
            {
                OnGenerateInvoiceClick(sender, e);
                return;
            }
            if (_currentStepIndex < StepsTabControl.Items.Count - 1)
            {
                _currentStepIndex++;
                StepsTabControl.SelectedIndex = _currentStepIndex;
                UpdateStepButtons();
            }
        }

        private void UpdateStepButtons()
        {
            if (PrevStepButton != null)
                PrevStepButton.IsEnabled = _currentStepIndex > 0;
            if (NextStepButton != null)
            {
                bool isLast = StepsTabControl != null && _currentStepIndex == StepsTabControl.Items.Count - 1;
                NextStepButton.Content = isLast ? "Generar Factura" : "Siguiente →";
                NextStepButton.Visibility = isLast ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private bool HasError(Control ctl)
        {
            // Considera error si el borde está marcado en rojo por SetError
            if (ctl == null) return false;
            var brush = ctl.BorderBrush as SolidColorBrush;
            return brush != null && brush.Color == Colors.Red;
        }

        private bool ValidateCurrentStep()
        {
            switch (_currentStepIndex)
            {
                // Paso 1: Emisor
                case 0:
                    UpdateCountryValidation(SellerCountryTextBox);
                    UpdateVatValidation();
                    if (string.IsNullOrWhiteSpace(SellerNameTextBox.Text)) { SetError(SellerNameTextBox, _localization?.Get("CreateInvoicePage.Errors.Required")); return false; }
                    if (HasError(SellerVATTextBox)) return false;
                    if (HasError(SellerCountryTextBox)) return false;
                    SetError(SellerNameTextBox, null);
                    return true;                // Paso 2: Receptor
                case 1:
                    UpdateCountryValidation(BuyerCountryTextBox);
                    UpdateBuyerCityValidation();
                    UpdateBuyerPostcodeValidation();
                    UpdateBuyerEmailValidation();
                    
                    if (string.IsNullOrWhiteSpace(BuyerNameTextBox.Text)) { SetError(BuyerNameTextBox, _localization?.Get("CreateInvoicePage.Errors.Required")); return false; }
                    if (HasError(BuyerCountryTextBox)) return false;
                    if (HasError(BuyerCityTextBox)) return false;
                    if (HasError(BuyerPostcodeTextBox)) return false;
                    if (HasError(BuyerEmailTextBox)) return false;
                    
                    SetError(BuyerNameTextBox, null);
                    return true;
                // Paso 3: Líneas
                case 2:
                    if (productos.Count == 0)
                    {
                        WinMessageBox.Show(LocalizationService.Instance.Get("CreateInvoicePage.Errors.AddAtLeastOneProduct"), "Validación", WinMessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    return true;
                // Paso 4: Impuestos y pagos
                case 3:
                    UpdateBuyerRefValidation();
                    UpdateIbanValidation();
                    if (HasError(BuyerReferenceTextBox)) return false;
                    if (HasError(IBANTextBox)) return false;
                    return true;
                default:
                    return true;
            }
        }

        private void CopyIban_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var iban = (IBANTextBox.Text ?? string.Empty).Replace(" ", string.Empty);
                if (!string.IsNullOrEmpty(iban)) Clipboard.SetText(iban);
            }
            catch { }
        }

        private void OnPreviewInvoiceClick(object sender, RoutedEventArgs e)
        {
            // Validación básica antes de previsualizar
            if (!ValidarDatosFactura()) return;

            var factura = CrearFacturaDesdeFormulario();
            try
            {
                string rutaReal = Process.GetCurrentProcess().MainModule?.FileName
                                   ?? throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable.");
                var directorioReal = Path.GetDirectoryName(rutaReal)
                                   ?? throw new InvalidOperationException("No se pudo determinar el directorio del ejecutable.");
                var numeroFactura = (InvoiceNumberTextBox.Text ?? "PREVIEW").Replace("/", "_").Replace("\\", "_");
                var pdfPreviewPath = System.IO.Path.Combine(directorioReal, $"Factura_{numeroFactura}_preview.pdf");

                var prev = new PreviewWindow(factura, pdfPreviewPath)
                {
                    Owner = Window.GetWindow(this)
                };
                prev.ShowDialog();
            }
            catch (Exception ex)
            {
                WinMessageBox.Show(LocalizationService.Instance.Get("CreateInvoicePage.Errors.PreviewError", ex.Message), LocalizationService.Instance.Get("Messages.ErrorTitle"), WinMessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Normaliza a mayúsculas y limita a ISO-2 (letras) manteniendo el caret
        private void Country_Uppercase_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not WinTextBox tb) return;
            string original = tb.Text ?? string.Empty;
            int caret = tb.CaretIndex;
            // Remover no letras, a mayúsculas y limitar a 2 chars
            string letters = new string(original.Where(char.IsLetter).ToArray()).ToUpperInvariant();
            if (letters.Length > 2) letters = letters.Substring(0, 2);

            if (letters != original)
            {
                tb.Text = letters;
                // Recalcular caret: número de letras válidas antes de la posición previa
                int validBefore = new string((original.Substring(0, Math.Min(caret, original.Length))).Where(char.IsLetter).ToArray()).Length;
                tb.CaretIndex = Math.Min(validBefore, tb.Text.Length);
            }
        }

        // Pone en mayúsculas al perder foco (ej. VAT, BIC)
        private void Uppercase_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is WinTextBox tb)
            {
                tb.Text = (tb.Text ?? string.Empty).Trim().ToUpperInvariant();
                if (tb == SellerVATTextBox) UpdateVatValidation();
            }
        }

        // Restringe a números con separador decimal (coma o punto)
        private void OnlyNumeric_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not WinTextBox tb) return;
            string text = tb.Text ?? string.Empty;
            // Inyectar el nuevo carácter respetando selección
            int selStart = tb.SelectionStart;
            int selLength = tb.SelectionLength;
            string next = text.Remove(selStart, Math.Min(selLength, text.Length - selStart)).Insert(selStart, e.Text);
            e.Handled = !Regex.IsMatch(next, @"^\d*([\.,]\d{0,4})?$");
        }

        private void OnlyNumeric_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not WinTextBox tb) return;
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string paste = (e.DataObject.GetData(DataFormats.Text) as string) ?? string.Empty;
                string text = tb.Text ?? string.Empty;
                int selStart = tb.SelectionStart;
                int selLength = tb.SelectionLength;
                string next = text.Remove(selStart, Math.Min(selLength, text.Length - selStart)).Insert(selStart, paste);
                if (!Regex.IsMatch(next, @"^\d*([\.,]\d{0,4})?$"))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        // Máscara IBAN con grupos de 4 y caret estable; valida al final
        private void IBANTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingIban) return;
            if (sender is not WinTextBox tb) return;

            try
            {
                _updatingIban = true;
                string raw = (tb.Text ?? string.Empty).ToUpperInvariant();
                int caret = tb.CaretIndex;

                // Posición lógica (sin espacios) antes del caret
                int logicalBefore = 0;
                for (int i = 0; i < Math.Min(caret, raw.Length); i++)
                {
                    if (raw[i] != ' ') logicalBefore++;
                }

                // Quitar espacios y validar caracteres (A-Z 0-9)
                string compact = new string(raw.Where(ch => ch != ' ').ToArray());
                compact = new string(compact.Where(ch => char.IsLetterOrDigit(ch)).ToArray());
                compact = compact.ToUpperInvariant();

                // Insertar espacios cada 4 caracteres
                var parts = Enumerable.Range(0, (compact.Length + 3) / 4)
                    .Select(i => compact.Substring(i * 4, Math.Min(4, compact.Length - i * 4)));
                string formatted = string.Join(" ", parts);

                if (tb.Text != formatted)
                {
                    tb.Text = formatted;
                    // Recalcular caret en la posición lógica previa
                    int newCaret = 0;
                    int logicalCount = 0;
                    while (newCaret < tb.Text.Length && logicalCount < logicalBefore)
                    {
                        if (tb.Text[newCaret] != ' ') logicalCount++;
                        newCaret++;
                    }
                    tb.CaretIndex = newCaret;
                }
            }
            finally
            {
                _updatingIban = false;
                // Validación ligera tras formatear
                UpdateIbanValidation();
                RefreshAlerts();
            }
        }

        // Wrapper de validación con mensajes para uso en generación
        private bool ValidarVATIDPorPais(string vatID, string countryCode)
        {
            bool ok = EsVATValidoSilencioso(vatID, countryCode);
            if (!ok)
            {
                WinMessageBox.Show(string.Format(LocalizationService.Instance.Get("CreateInvoicePage.Errors.InvalidVATForCountry"), countryCode), "Validación VAT", WinMessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return ok;
        }

        /// <summary>
        /// Actualiza los textos de la UI según el idioma seleccionado
        /// </summary>
        private void UpdateUIText()
        {
            if (_localization == null) return;
            try
            {
                // Header titles
                var pageTitle = FindName("PageTitle") as WinTextBlock;
                if (pageTitle != null) pageTitle.Text = _localization.Get("CreateInvoicePage.Title");

                var mainTitle = FindName("MainTitle") as WinTextBlock;
                if (mainTitle != null) mainTitle.Text = _localization.Get("CreateInvoicePage.Title");

                // Alerts
                var topAlerts = FindName("TopAlertsTitle") as WinTextBlock;
                if (topAlerts != null) topAlerts.Text = _localization.Get("CreateInvoicePage.TopAlertsTitle");

                // Buttons
                var exitBtn = FindName("ExitButton") as WinButton;
                if (exitBtn != null) exitBtn.Content = _localization.Get("CreateInvoicePage.Exit");

                var goFirst = FindName("GoToFirstErrorButton") as WinButton;
                if (goFirst != null) goFirst.Content = _localization.Get("CreateInvoicePage.GoToFirstError");

                var dismiss = FindName("DismissAlertsButton") as WinButton;
                if (dismiss != null) dismiss.Content = _localization.Get("CreateInvoicePage.DismissAlerts");

                var addProd = FindName("AddProductButton") as WinButton;
                if (addProd != null) addProd.Content = "✓ " + _localization.Get("CreateInvoicePage.AddProduct");

                var copyIban = FindName("CopyIbanButton") as WinButton;
                if (copyIban != null) copyIban.Content = _localization.Get("CreateInvoicePage.CopyIban");

                // Tab headers
                var sellerTab = FindName("SellerTab") as TabItem;
                if (sellerTab != null) sellerTab.Header = _localization.Get("CreateInvoicePage.Step1");
                var buyerTab = FindName("BuyerTab") as TabItem;
                if (buyerTab != null) buyerTab.Header = _localization.Get("CreateInvoicePage.Step2");
                var linesTab = FindName("LinesTab") as TabItem;
                if (linesTab != null) linesTab.Header = _localization.Get("CreateInvoicePage.Step3");
                var taxesTab = FindName("TaxesTab") as TabItem;
                if (taxesTab != null) taxesTab.Header = _localization.Get("CreateInvoicePage.Step4");
                var summaryTab = FindName("SummaryTab") as TabItem;
                if (summaryTab != null) summaryTab.Header = _localization.Get("CreateInvoicePage.Step5");

                // Section titles and labels (nombres ya creados en XAML)
                var sellerSection = FindName("SellerSection") as WinTextBlock;
                if (sellerSection != null) sellerSection.Text = _localization.Get("CreateInvoicePage.Seller");
                var buyerSection = FindName("BuyerSection") as WinTextBlock;
                if (buyerSection != null) buyerSection.Text = _localization.Get("CreateInvoicePage.Buyer");

                // Field labels
                var mappings = new Dictionary<string, string>
                {
                    { "SellerNameLabel", "CreateInvoicePage.SellerName" },
                    { "SellerContactLabel", "CreateInvoicePage.SellerContact" },
                    { "SellerEmailLabel", "CreateInvoicePage.SellerEmail" },
                    { "SellerPhoneLabel", "CreateInvoicePage.SellerPhone" },
                    { "SellerAddressTitle", "CreateInvoicePage.SellerAddress" },
                    { "SellerStreetLabel", "CreateInvoicePage.SellerAddress" },
                    { "SellerStreet2Label", "CreateInvoicePage.SellerAddress" },
                    { "SellerCityLabel", "CreateInvoicePage.SellerCity" },
                    { "SellerPostcodeLabel", "CreateInvoicePage.SellerPostcode" },
                    { "SellerCountryLabel", "CreateInvoicePage.SellerCountry" },
                    { "SellerIdentifiersTitle", "CreateInvoicePage.SellerVAT" },
                    { "SellerVATLabel", "CreateInvoicePage.SellerVAT" },
                    { "SellerTaxNumberLabel", "CreateInvoicePage.SellerTaxNumber" },

                    { "BuyerNameLabel", "CreateInvoicePage.BuyerName" },
                    { "BuyerContactLabel", "CreateInvoicePage.BuyerContact" },
                    { "BuyerEmailLabel", "CreateInvoicePage.BuyerEmail" },
                    { "BuyerPhoneLabel", "CreateInvoicePage.BuyerPhone" },
                    { "BuyerAddressTitle", "CreateInvoicePage.BuyerAddress" },
                    { "BuyerStreetLabel", "CreateInvoicePage.BuyerAddress" },
                    { "BuyerStreet2Label", "CreateInvoicePage.BuyerAddress" },
                    { "BuyerCityLabel", "CreateInvoicePage.BuyerCity" },
                    { "BuyerPostcodeLabel", "CreateInvoicePage.BuyerPostcode" },
                    { "BuyerCountryLabel", "CreateInvoicePage.BuyerCountry" },
                    { "BuyerVATLabel", "CreateInvoicePage.BuyerVAT" },
                    { "BuyerEmailContactLabel", "CreateInvoicePage.BuyerEmailContact" },

                    { "ProductsServicesTitle", "CreateInvoicePage.ProductsServices" },
                    { "AddProductTitle", "CreateInvoicePage.AddProductTitle" },

                    { "InvoiceSummaryTitle", "CreateInvoicePage.InvoiceSummary" },
                    { "SubtotalLabel", "CreateInvoicePage.Subtotal" },
                    { "TaxesLabel", "CreateInvoicePage.Taxes" },
                    { "TotalLabel", "CreateInvoicePage.Total" },

                    { "InvoiceDataTitle", "CreateInvoicePage.InvoiceData" },
                    { "InvoiceNumberLabel", "CreateInvoicePage.InvoiceNumber" },
                    { "BuyerReferenceLabel", "CreateInvoicePage.BuyerReference" },
                    { "IssueDateLabel", "CreateInvoicePage.IssueDate" },
                    { "DueDateLabel", "CreateInvoicePage.DueDate" },
                    { "DeliveryDateLabel", "CreateInvoicePage.DeliveryDate" },
                    { "CurrencyLabel", "CreateInvoicePage.Currency" },
                    { "TaxDataTitle", "CreateInvoicePage.TaxData" },
                    { "TaxCategoryLabel", "CreateInvoicePage.TaxCategory" },
                    { "TaxRateLabel", "CreateInvoicePage.TaxRate" },

                    { "IBANLabel", "CreateInvoicePage.IBAN" },
                    { "BICLabel", "CreateInvoicePage.BIC" },
                    { "AccountNameLabel", "CreateInvoicePage.AccountName" },

                    { "ProjectNumberLabel", "CreateInvoicePage.ProjectNumber" },
                    { "ContractNumberLabel", "CreateInvoicePage.ContractNumber" },
                    { "PurchaseOrderNumberLabel", "CreateInvoicePage.PurchaseOrderNumber" },
                    { "SalesOrderNumberLabel", "CreateInvoicePage.SalesOrderNumber" },
                    { "PaymentReferenceLabel", "CreateInvoicePage.PaymentReference" },

                    { "GeneralNoteLabel", "CreateInvoicePage.GeneralNote" },
                    { "PaymentTermsDescriptionLabel", "CreateInvoicePage.PaymentTermsDescription" },

                    { "NotesLabel", "CreateInvoicePage.Notes" },

                    { "ShipToNameLabel", "CreateInvoicePage.ShipToName" },
                    { "ShipToIDLabel", "CreateInvoicePage.ShipToID" },
                    { "ShipToLineOneLabel", "CreateInvoicePage.ShipToLineOne" },
                    { "ShipToLineTwoLabel", "CreateInvoicePage.ShipToLineTwo" },
                    { "ShipToLineThreeLabel", "CreateInvoicePage.ShipToLineThree" },
                    { "ShipToCityNameLabel", "CreateInvoicePage.ShipToCityName" },
                    { "ShipToPostcodeCodeLabel", "CreateInvoicePage.ShipToPostcodeCode" },
                    { "ShipToCountryIDLabel", "CreateInvoicePage.ShipToCountryID" },
                    { "ShipToCountrySubDivisionNameLabel", "CreateInvoicePage.ShipToCountrySubDivisionName" },

                    { "SummaryTitle", "CreateInvoicePage.SummaryTitle" },
                    { "SummaryHint", "CreateInvoicePage.SummaryHint" },
                    { "SummarySellerTitle", "CreateInvoicePage.SummarySellerTitle" },
                    { "SummarySellerNameLabel", "CreateInvoicePage.SummarySellerNameLabel" },
                    { "SummarySellerVATLabel", "CreateInvoicePage.SummarySellerVATLabel" },
                    { "SummarySellerAddressLabel", "CreateInvoicePage.SummarySellerAddressLabel" },
                    { "SummarySellerContactLabel", "CreateInvoicePage.SummarySellerContactLabel" },
                    { "SummaryBuyerTitle", "CreateInvoicePage.SummaryBuyerTitle" },
                    { "SummaryBuyerNameLabel", "CreateInvoicePage.SummaryBuyerNameLabel" },
                    { "SummaryBuyerVATLabel", "CreateInvoicePage.SummaryBuyerVATLabel" },
                    { "SummaryBuyerAddressLabel", "CreateInvoicePage.SummaryBuyerAddressLabel" },
                    { "SummaryBuyerContactLabel", "CreateInvoicePage.SummaryBuyerContactLabel" },
                    { "SummaryInvoiceTitle", "CreateInvoicePage.SummaryInvoiceTitle" },
                    { "SummaryInvoiceNumberLabel", "CreateInvoicePage.SummaryInvoiceNumberLabel" },
                    { "SummaryInvoiceDatesLabel", "CreateInvoicePage.SummaryInvoiceDatesLabel" },
                    { "SummaryInvoiceCurrencyLabel", "CreateInvoicePage.SummaryInvoiceCurrencyLabel" },
                    { "SummaryReferencesTitle", "CreateInvoicePage.SummaryReferencesTitle" },
                    { "SummaryBuyerRefLabel", "CreateInvoicePage.SummaryBuyerRefLabel" },
                    { "SummaryPaymentReferenceLabel", "CreateInvoicePage.SummaryPaymentReferenceLabel" },
                    { "SummaryProjectContractOrderLabel", "CreateInvoicePage.SummaryProjectContractOrderLabel" },
                    { "SummaryLinesTotalsTitle", "CreateInvoicePage.SummaryLinesTotalsTitle" },
                    { "SummaryLinesCountLabel", "CreateInvoicePage.SummaryLinesCountLabel" },
                    { "SummarySubtotalLabel", "CreateInvoicePage.SummarySubtotalLabel" },
                    { "SummaryTaxesLabel", "CreateInvoicePage.SummaryTaxesLabel" },
                    { "SummaryTotalLabel", "CreateInvoicePage.SummaryTotalLabel" },
                    { "SummaryTaxesTitle", "CreateInvoicePage.SummaryTaxesTitle" },
                    { "SummaryTaxCategoryLabel", "CreateInvoicePage.SummaryTaxCategoryLabel" },
                    { "SummaryTaxRateLabel", "CreateInvoicePage.SummaryTaxRateLabel" },
                    { "SummaryPaymentTitle", "CreateInvoicePage.SummaryPaymentTitle" },
                    { "SummaryIBANLabel", "CreateInvoicePage.SummaryIBANLabel" },
                    { "SummaryBICLabel", "CreateInvoicePage.SummaryBICLabel" },
                    { "SummaryAccountNameLabel", "CreateInvoicePage.SummaryAccountNameLabel" },
                    { "SummaryDeliveryTitle", "CreateInvoicePage.SummaryDeliveryTitle" },
                    { "SummaryDeliveryNameLabel", "CreateInvoicePage.SummaryDeliveryNameLabel" },
                    { "SummaryDeliveryCityPostcodeCountryLabel", "CreateInvoicePage.SummaryDeliveryCityPostcodeCountryLabel" },
                    { "SummaryNotesTitle", "CreateInvoicePage.SummaryNotesTitle" },
                    { "SummaryGeneralNotesLabel", "CreateInvoicePage.SummaryGeneralNotesLabel" },
                    { "SummaryPaymentTermsLabel", "CreateInvoicePage.SummaryPaymentTermsLabel" },
                    { "SummaryAlertsTitle", "CreateInvoicePage.TopAlertsTitle" }
                };

                foreach (var kv in mappings)
                {
                    var tb = FindName(kv.Key) as WinTextBlock;
                    if (tb != null) tb.Text = _localization.Get(kv.Value);
                }

                // DataGrid headers
                var dg = FindName("ProductsDataGrid") as WinDataGrid;
                if (dg != null && dg.Columns.Count >= 9)
                {
                    dg.Columns[0].Header = _localization.Get("CreateInvoicePage.Pos");
                    dg.Columns[1].Header = _localization.Get("CreateInvoicePage.ProductName");
                    dg.Columns[2].Header = _localization.Get("CreateInvoicePage.Description");
                    dg.Columns[3].Header = _localization.Get("CreateInvoicePage.Quantity");
                    dg.Columns[4].Header = _localization.Get("CreateInvoicePage.Unit");
                    dg.Columns[5].Header = _localization.Get("CreateInvoicePage.UnitPrice");
                    dg.Columns[6].Header = _localization.Get("CreateInvoicePage.TotalPrice");
                    dg.Columns[7].Header = _localization.Get("CreateInvoicePage.Actions");
                    dg.Columns[8].Header = _localization.Get("CreateInvoicePage.Details");
                }

                // Summary labels
                var subtotal = FindName("SubtotalTextBlock") as WinTextBlock;
                if (subtotal != null) subtotal.SetValue(WinTextBlock.TagProperty, _localization.Get("CreateInvoicePage.Subtotal"));
                var taxes = FindName("TaxesTextBlock") as WinTextBlock;
                if (taxes != null) taxes.SetValue(WinTextBlock.TagProperty, _localization.Get("CreateInvoicePage.Taxes"));
                var total = FindName("TotalTextBlock") as WinTextBlock;
                if (total != null) total.SetValue(WinTextBlock.TagProperty, _localization.Get("CreateInvoicePage.Total"));

                // Also update static WinTextBlocks near them
                var grids = LogicalTreeHelper.GetChildren(this).OfType<DependencyObject>();
                // Note: Some static WinTextBlocks remain in XAML and will be updated via explicit FindName if named.

                // Expander headers
                var expanderBanking = FindName("ExpanderBanking") as Expander;
                if (expanderBanking != null) expanderBanking.Header = _localization.Get("CreateInvoicePage.Banking");
                var expanderDocumentRefs = FindName("ExpanderDocumentRefs") as Expander;
                if (expanderDocumentRefs != null) expanderDocumentRefs.Header = _localization.Get("CreateInvoicePage.DocumentRefs");
                var expanderNotesObs = FindName("ExpanderNotesObs") as Expander;
                if (expanderNotesObs != null) expanderNotesObs.Header = _localization.Get("CreateInvoicePage.NotesObs");
                var expanderNotes = FindName("ExpanderNotes") as Expander;
                if (expanderNotes != null) expanderNotes.Header = _localization.Get("CreateInvoicePage.Notes");
                var expanderDelivery = FindName("ExpanderDelivery") as Expander;
                if (expanderDelivery != null) expanderDelivery.Header = _localization.Get("CreateInvoicePage.Delivery");

                // Buttons
                var previewBtn = FindName("PreviewButton") as WinButton;
                if (previewBtn != null) previewBtn.Content = "👁 " + _localization.Get("CreateInvoicePage.Preview");
                var generateBtn = FindName("GenerateButton") as WinButton;
                if (generateBtn != null) generateBtn.Content = "✓ " + _localization.Get("CreateInvoicePage.Generate");
                var cancelBtn = FindName("CancelButton") as WinButton;
                if (cancelBtn != null) cancelBtn.Content = "✕ " + _localization.Get("CreateInvoicePage.Cancel");
                var prevBtn = FindName("PrevStepButton") as WinButton;
                if (prevBtn != null) prevBtn.Content = "← " + _localization.Get("CreateInvoicePage.Prev");
                var nextBtn = FindName("NextStepButton") as WinButton;
                if (nextBtn != null) nextBtn.Content = _localization.Get("CreateInvoicePage.Next") + " →";

                // Placeholders
                var placeholderMappings = new Dictionary<string, string>
                {
                    { "SellerNameTextBox", "CreateInvoicePage.SellerNamePlaceholder" },
                    { "SellerPersonNameTextBox", "CreateInvoicePage.SellerPersonNamePlaceholder" },
                    { "SellerEmailTextBox", "CreateInvoicePage.SellerEmailPlaceholder" },
                    { "SellerPhoneTextBox", "CreateInvoicePage.SellerPhonePlaceholder" },
                    { "SellerStreetTextBox", "CreateInvoicePage.SellerStreetPlaceholder" },
                    { "SellerStreet2TextBox", "CreateInvoicePage.SellerStreet2Placeholder" },
                    { "SellerCityTextBox", "CreateInvoicePage.SellerCityPlaceholder" },
                    { "SellerPostcodeTextBox", "CreateInvoicePage.SellerPostcodePlaceholder" },
                    { "SellerCountryTextBox", "CreateInvoicePage.SellerCountryPlaceholder" },
                    { "SellerVATTextBox", "CreateInvoicePage.SellerVATPlaceholder" },
                    { "SellerTaxTextBox", "CreateInvoicePage.SellerTaxNumberPlaceholder" },
                    { "BuyerNameTextBox", "CreateInvoicePage.BuyerNamePlaceholder" },
                    { "BuyerPersonNameTextBox", "CreateInvoicePage.BuyerPersonNamePlaceholder" },
                    { "BuyerEmailTextBox", "CreateInvoicePage.BuyerEmailPlaceholder" },
                    { "BuyerPhoneTextBox", "CreateInvoicePage.BuyerPhonePlaceholder" },
                    { "BuyerStreetTextBox", "CreateInvoicePage.BuyerStreetPlaceholder" },
                    { "BuyerStreet2TextBox", "CreateInvoicePage.BuyerStreet2Placeholder" },
                    { "BuyerCityTextBox", "CreateInvoicePage.BuyerCityPlaceholder" },
                    { "BuyerPostcodeTextBox", "CreateInvoicePage.BuyerPostcodePlaceholder" },
                    { "BuyerCountryTextBox", "CreateInvoicePage.BuyerCountryPlaceholder" },
                    { "BuyerVATTextBox", "CreateInvoicePage.BuyerVATPlaceholder" },
                    { "BuyerEmailContactTextBox", "CreateInvoicePage.BuyerEmailContactPlaceholder" },
                    { "ProductDescTextBox", "CreateInvoicePage.ProductDescPlaceholder" },
                    { "ProductQtyTextBox", "CreateInvoicePage.ProductQtyPlaceholder" },
                    { "ProductPriceTextBox", "CreateInvoicePage.ProductPricePlaceholder" },
                    { "InvoiceNumberTextBox", "CreateInvoicePage.InvoiceNumberPlaceholder" },
                    { "BuyerReferenceTextBox", "CreateInvoicePage.BuyerReferencePlaceholder" },
                    { "IBANTextBox", "CreateInvoicePage.IBANPlaceholder" },
                    { "BICTextBox", "CreateInvoicePage.BICPlaceholder" },
                    { "AccountNameTextBox", "CreateInvoicePage.AccountNamePlaceholder" },
                    { "ProjectNumberTextBox", "CreateInvoicePage.ProjectNumberPlaceholder" },
                    { "ContractNumberTextBox", "CreateInvoicePage.ContractNumberPlaceholder" },
                    { "PurchaseOrderNumberTextBox", "CreateInvoicePage.PurchaseOrderNumberPlaceholder" },
                    { "SalesOrderNumberTextBox", "CreateInvoicePage.SalesOrderNumberPlaceholder" },
                    { "PaymentReferenceTextBox", "CreateInvoicePage.PaymentReferencePlaceholder" },
                    { "GeneralNoteTextBox", "CreateInvoicePage.GeneralNotePlaceholder" },
                    { "PaymentTermsDescriptionTextBox", "CreateInvoicePage.PaymentTermsDescriptionPlaceholder" },
                    { "NotesTextBox", "CreateInvoicePage.NotesPlaceholder" },
                    { "ShipToNameTextBox", "CreateInvoicePage.ShipToNamePlaceholder" },
                    { "ShipToIDTextBox", "CreateInvoicePage.ShipToIDPlaceholder" },
                    { "ShipToLineOneTextBox", "CreateInvoicePage.ShipToLineOnePlaceholder" },
                    { "ShipToLineTwoTextBox", "CreateInvoicePage.ShipToLineTwoPlaceholder" },
                    { "ShipToLineThreeTextBox", "CreateInvoicePage.ShipToLineThreePlaceholder" },
                    { "ShipToCityNameTextBox", "CreateInvoicePage.ShipToCityPlaceholder" },
                    { "ShipToPostcodeCodeTextBox", "CreateInvoicePage.ShipToPostcodeCodePlaceholder" },
                    { "ShipToCountryIDTextBox", "CreateInvoicePage.ShipToCountryIDPlaceholder" },
                    { "ShipToCountrySubDivisionNameTextBox", "CreateInvoicePage.ShipToCountrySubDivisionNamePlaceholder" }
                };

                foreach (var kv in placeholderMappings)
                {
                    var tb = FindName(kv.Key) as Wpf.Ui.Controls.TextBox;
                    if (tb != null) tb.PlaceholderText = _localization.Get(kv.Value);
                }

                // Tooltips
                var tooltipMappings = new Dictionary<string, string>
                {
                    { "SellerCountryTextBox", "CreateInvoicePage.SellerCountryTooltip" },
                    { "SellerVATTextBox", "CreateInvoicePage.SellerVATTooltip" },
                    { "BuyerEmailTextBox", "CreateInvoicePage.BuyerEmailTooltip" },
                    { "BuyerCountryTextBox", "CreateInvoicePage.BuyerCountryTooltip" },
                    { "BuyerReferenceTextBox", "CreateInvoicePage.BuyerReferenceTooltip" },
                    { "TaxRateTextBox", "CreateInvoicePage.TaxRateTooltip" },
                    { "IBANTextBox", "CreateInvoicePage.IBANTooltip" }
                };

                foreach (var kv in tooltipMappings)
                {
                    var tb = FindName(kv.Key) as Wpf.Ui.Controls.TextBox;
                    if (tb != null) tb.ToolTip = _localization.Get(kv.Value);
                }

                // Window button tooltips
                var btnMinimize = FindName("BtnMinimizeCI") as WinButton;
                if (btnMinimize != null) btnMinimize.ToolTip = _localization.Get("HomePage.Minimize");
                var btnMaxRestore = FindName("BtnMaxRestoreCI") as WinButton;
                if (btnMaxRestore != null) btnMaxRestore.ToolTip = _localization.Get("HomePage.MaximizeRestore");
                var btnClose = FindName("BtnCloseCI") as WinButton;
                if (btnClose != null) btnClose.ToolTip = _localization.Get("HomePage.Close");
            }
            catch { }
        }
    }
}
