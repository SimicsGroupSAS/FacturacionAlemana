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

namespace FacturacionAlemana
{
    public partial class CreateInvoicePage : Page
    {
        private ObservableCollection<Producto> productos = new();
        private bool _updatingIban = false;
        private int _currentStepIndex = 0;
        // Maximizar solo una vez al cargar la página
        private bool _windowStateAdjusted = false;
        private bool _showGlobalAlerts = false;
        private bool _alertsDismissed = false;
        private bool _allowProgrammaticStepJump = false;

        public CreateInvoicePage()
        {
            InitializeComponent();
            // Prellenar el campo de número de factura
            InvoiceNumberTextBox.Text = $"STR-{DateTime.Now.Year.ToString().Substring(2)}-";
            // Maximizar al cargar sin forzar posteriormente minimizar/restaurar
            this.Loaded += OnCreateInvoicePageLoaded;
            ProductsDataGrid.ItemsSource = productos;
            IssueDatePicker.SelectedDate = DateTime.Now;
            DeliveryDatePicker.SelectedDate = DateTime.Now;
            DueDatePicker.SelectedDate = DateTime.Now.AddMonths(1);
            
            // Inicializar ComboBox de moneda
            var currencyItems = new List<ComboBoxItem>
            {
                new ComboBoxItem { Content = "EUR", IsSelected = true },
                new ComboBoxItem { Content = "USD" },
                new ComboBoxItem { Content = "GBP" },
                new ComboBoxItem { Content = "CHF" }
            };
            CurrencyComboBox.ItemsSource = currencyItems;
            CurrencyComboBox.SelectedIndex = 0;
            
            // Inicializar ComboBox de categoría de impuestos
            TaxCategoryComboBox.ItemsSource = new List<string> { "S", "AA", "Z", "E", "O", "AE" };
            
            // Conectar eventos ANTES de cambiar selecciones
            TaxCategoryComboBox.SelectionChanged += TaxCategoryComboBox_SelectionChanged;
            TaxRateTextBox.TextChanged += (s, e) => ActualizarResumenTotales();
            CurrencyComboBox.SelectionChanged += (s, e) => ActualizarResumenTotales();
            
            // Establecer valores iniciales DESPUÉS de conectar eventos
            TaxCategoryComboBox.SelectedValue = "S";
            TaxRateTextBox.Text = "19.00";
            
            // Forzar cálculo inicial
            ActualizarResumenTotales();            // Validación en tiempo real
            HookRealtimeValidation();
            
            // Validar campos OBLIGATORIOS desde el inicio (solo vendedor)
            // Vendedor (Paso 1) - Solo campos obligatorios
            UpdateCountryValidation(SellerCountryTextBox);
            UpdateVatValidation();
            // Email y Postcode del vendedor son opcionales, NO validar aquí
            
            // Comprador (Paso 2) - Los campos del comprador se validan en tiempo real
            // al momento de la interacción del usuario, NO en la carga inicial
            // UpdateBuyerPostcodeValidation();  // REMOVED - validated on real-time events
            // UpdateBuyerCityValidation();      // REMOVED - validated on real-time events
            // UpdateBuyerEmailValidation();     // REMOVED - validated on real-time events
            
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
                    MessageBox.Show("Por favor, completa todos los campos del producto.", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Normalizar separador decimal para aceptar coma o punto
                var cantidadNorm = (cantidadStr ?? string.Empty).Replace(',', '.');
                var precioNorm = (precioStr ?? string.Empty).Replace(',', '.');

                if (!decimal.TryParse(cantidadNorm, NumberStyles.Any, CultureInfo.InvariantCulture, out var cantidad) ||
                    !decimal.TryParse(precioNorm, NumberStyles.Any, CultureInfo.InvariantCulture, out var precio))
                {
                    MessageBox.Show("La cantidad y precio deben ser números válidos.", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Error al agregar producto: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Por favor, agrega al menos un producto.", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(SellerNameTextBox.Text) ||
                    string.IsNullOrWhiteSpace(BuyerNameTextBox.Text) ||
                    string.IsNullOrWhiteSpace(InvoiceNumberTextBox.Text))
                {
                    MessageBox.Show("Por favor, completa los datos del vendedor, comprador y número de factura.", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Validación de VAT-ID
                if (string.IsNullOrWhiteSpace(SellerVATTextBox.Text) || SellerVATTextBox.Text.Length < 5)
                {
                    MessageBox.Show("⚠️ VAT-ID del vendedor es inválido o está vacío.\n\nFormato esperado:\n" +
                        "- Alemania (DE): DExxxxxxxxx (11 dígitos)\n" +
                        "- España (ES): ESxxxxxxxxx (8 caracteres)\n\n" +
                        "Ejemplo: DE123456789", "Validación VAT-ID", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validación de IBAN
                if (string.IsNullOrWhiteSpace(IBANTextBox.Text))
                {
                    MessageBox.Show("⚠️ IBAN no puede estar vacío.", "Validación IBAN", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var ibanLimpio = IBANTextBox.Text.Replace(" ", "").ToUpper();
                if (ibanLimpio.Length != 22)
                {
                    MessageBox.Show($"⚠️ IBAN inválido.\n\n" +
                        $"Longitud actual: {ibanLimpio.Length} caracteres\n" +
                        $"Longitud requerida: 22 caracteres (sin espacios)\n\n" +
                        $"Formato esperado: DE89400900505012345678", "Validación IBAN", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
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

                PdfGeneratorService.GenerarFacturaPdf(factura, pdfPath);
                XmlGeneratorService.GenerarFacturaXml(factura, xmlPath);

                MessageBox.Show($"Factura generada exitosamente.\n\nPDF: {pdfPath}\nXML: {xmlPath}", 
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar factura: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }        
        }        
        private bool ValidarDatosFactura()
        {
            // Validar código de país del Vendedor (ISO 3166-1)
            string sellerCountry = SellerCountryTextBox.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(sellerCountry) || sellerCountry.Length != 2)
            {
                MessageBox.Show("El código de país del vendedor debe ser un código ISO 3166-1 de 2 letras.\n" +
                    "Ejemplos válidos:\n- DE (Alemania)\n- ES (España)\n- FR (Francia)\n- CO (Colombia)",
                    "Validación de País del Vendedor", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!sellerCountry.All(char.IsLetter))
            {
                MessageBox.Show("El código de país del vendedor debe contener solo letras.\n" +
                    "Ejemplos válidos: DE, ES, FR, CO",
                    "Validación de País del Vendedor", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validar código de país del Comprador (ISO 3166-1)
            string buyerCountry = BuyerCountryTextBox.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(buyerCountry) || buyerCountry.Length != 2)
            {
                MessageBox.Show("El código de país del comprador debe ser un código ISO 3166-1 de 2 letras.\n" +
                    "Ejemplos válidos:\n- DE (Alemania)\n- ES (España)\n- FR (Francia)\n- CO (Colombia)",
                    "Validación de País del Comprador", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!buyerCountry.All(char.IsLetter))
            {
                MessageBox.Show("El código de país del comprador debe contener solo letras.\n" +
                    "Ejemplos válidos: DE, ES, FR, CO",
                    "Validación de País del Comprador", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }            
            // Validar VAT-ID
            string vatID = SellerVATTextBox.Text.Trim();
            if (string.IsNullOrEmpty(vatID))
            {
                MessageBox.Show("El número de IVA (VAT-ID) es obligatorio.", "Validación", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("El IBAN es obligatorio.", "Validación", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // IBAN debe tener exactamente 22 caracteres sin espacios
            if (iban.Length != 22)
            {
                MessageBox.Show($"El IBAN debe tener exactamente 22 caracteres sin espacios.\n" +
                    $"Tu IBAN tiene {iban.Length} caracteres.\n\n" +
                    $"Formato correcto: Código país (2) + Dígitos de control (2) + Código banco + Número cuenta\n" +
                    $"Ejemplo válido: DE89400900505012345678",
                    "Validación de IBAN", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // IBAN debe comenzar con 2 letras (código país)
            if (!char.IsLetter(iban[0]) || !char.IsLetter(iban[1]))
            {
                MessageBox.Show("El IBAN debe comenzar con un código de país de 2 letras (ej: DE, ES, FR).",
                    "Validación de IBAN", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }            
            // El resto del IBAN debe ser numérico
            if (!iban.Substring(2).All(char.IsDigit))
            {
                MessageBox.Show("Después del código de país, el IBAN solo debe contener números.",
                    "Validación de IBAN", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validar BuyerReference (obligatorio según BR-DE-15)
            string buyerReference = BuyerReferenceTextBox.Text.Trim();
            if (string.IsNullOrEmpty(buyerReference))
            {
                MessageBox.Show("La referencia del comprador (BT-10) es obligatoria según la norma XRechnung.\n\n" +
                    "Ingresa un valor como:\n- REF-12345\n- PO-2025-001\n- ORDEN-123",
                    "Validación de Referencia del Comprador", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validar campos obligatorios del comprador según XRechnung/EN 16931
            string buyerCity = BuyerCityTextBox.Text.Trim();
            if (string.IsNullOrEmpty(buyerCity))
            {
                MessageBox.Show("La ciudad del comprador (BT-52) es obligatoria según la norma XRechnung.\n\n" +
                    "Ingresa la ciudad del comprador.",
                    "Validación de Ciudad del Comprador", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string buyerPostcode = BuyerPostcodeTextBox.Text.Trim();
            if (string.IsNullOrEmpty(buyerPostcode))
            {
                MessageBox.Show("El código postal del comprador (BT-53) es obligatorio según la norma XRechnung.\n\n" +
                    "Ingresa el código postal del comprador.",
                    "Validación de Código Postal del Comprador", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string buyerEmail = BuyerEmailTextBox.Text.Trim();
            if (string.IsNullOrEmpty(buyerEmail))
            {
                MessageBox.Show("El email del comprador es obligatorio según PEPPOL-EN16931-R010.\n\n" +
                    "Ingresa el email electrónico del comprador.",
                    "Validación de Email del Comprador", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validar formato de email básico
            if (!buyerEmail.Contains("@") || !buyerEmail.Contains("."))
            {
                MessageBox.Show("El email del comprador debe tener un formato válido.\n\n" +
                    "Ejemplo: nombre@empresa.com",
                    "Validación de Email del Comprador", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                GrandTotalAmount = totalConImpuestos.ToString("F2", CultureInfo.InvariantCulture),
                DuePayableAmount = totalConImpuestos.ToString("F2", CultureInfo.InvariantCulture),
                DateTimeFormat = "102",
                UnitCode = "EA",
                SchemeID = "0060",
                CurrencyID = monedaSeleccionada,
                IdElement = InvoiceNumberTextBox.Text,
                TypeCodeElement = "380",
                // Convertir fecha de yyyy-MM-dd a YYYYMMDD (formato requerido por EN 16931)
                IssueDateElement = IssueDatePicker.SelectedDate?.ToString("yyyyMMdd") ?? DateTime.Now.ToString("yyyyMMdd"),
                PaymentNoteElement = NotesTextBox.Text,
                TaxAmount = totalImpuestos.ToString("F2", CultureInfo.InvariantCulture),
                
                SellerName = SellerNameTextBox.Text,
                SellerPersonName = SellerPersonNameTextBox.Text,
                SellerDepartmentName = "Ventas",
                SellerCompleteNumber = SellerPhoneTextBox.Text,                
                SellerEmail = SellerEmailTextBox.Text,
                SellerPostcodeCode = SellerPostcodeTextBox.Text,
                SellerLineOne = SellerStreetTextBox.Text,
                SellerLineTwo = SellerStreet2TextBox.Text,
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
                PaymentTypeCode = "30",
                PaymentInformation = "SEPA",
                IBANID = IBANTextBox.Text.Replace(" ", "").ToUpper(), // Remover espacios y convertir a mayúsculas
                AccountName = AccountNameTextBox.Text,
                BICID = BICTextBox.Text,
                CalculatedAmount = totalImpuestos.ToString("F2", CultureInfo.InvariantCulture), // Monto del IVA (no el total)
                BasisAmount = totalGeneral.ToString("F2", CultureInfo.InvariantCulture), // Base imponible (neto sin impuestos)
                PaymentDescription = "Pago según términos acordados",
                
                InvoiceNumber = InvoiceNumberTextBox.Text,
                IssueDate = IssueDatePicker.SelectedDate ?? DateTime.Now,
                DeliveryDate = DeliveryDatePicker.SelectedDate ?? DateTime.Now,
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
                ShipToCityName = ShipToCityNameTextBox.Text,
                ShipToCountryID = ShipToCountryIDTextBox.Text,
                ShipToCountrySubDivisionName = ShipToCountrySubDivisionNameTextBox.Text,
                Notes = NotesTextBox.Text,
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
            if (sender is TextBox tb)
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
                        if (ctl is TextBox tbox)
                            tbox.SelectAll();
                    });
                    return;
                }
            }
        }

        /// <summary>
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
                errors.Add("Paso 1 - Vendedor: Nombre es obligatorio");
            if ((SellerCountryTextBox.Text ?? string.Empty).Trim().Length != 2) 
                errors.Add("Paso 1 - Vendedor: País debe ser ISO-2 (ej: DE)");
            if (!EsVATValidoSilencioso((SellerVATTextBox.Text ?? string.Empty).Trim(), (SellerCountryTextBox.Text ?? string.Empty).Trim())) 
                errors.Add("Paso 1 - Vendedor: VAT no es válido para el país");
            if (!string.IsNullOrEmpty(SellerPostcodeTextBox.Text) && !IsPostcodeValidForCountry(SellerPostcodeTextBox.Text.Trim(), (SellerCountryTextBox.Text ?? string.Empty).Trim().ToUpper())) 
                errors.Add("Paso 1 - Vendedor: Código Postal inválido");
            if (!string.IsNullOrEmpty(SellerEmailTextBox.Text) && !Regex.IsMatch(SellerEmailTextBox.Text.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")) 
                errors.Add("Paso 1 - Vendedor: Email inválido");            // ===== PASO 1: COMPRADOR =====
            if (string.IsNullOrWhiteSpace(BuyerNameTextBox.Text)) 
                errors.Add("Paso 2 - Comprador: Nombre es obligatorio");
            var buyerEmail = (BuyerEmailTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(buyerEmail)) 
                errors.Add("Paso 2 - Comprador: Email (PEPPOL-EN16931-R010) es obligatorio");
            else if (!Regex.IsMatch(buyerEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")) 
                errors.Add("Paso 2 - Comprador: Email inválido");
            var buyerCity = (BuyerCityTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(buyerCity)) 
                errors.Add("Paso 2 - Comprador: Ciudad (BT-52) es obligatoria");
            var buyerPostcode = (BuyerPostcodeTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(buyerPostcode)) 
                errors.Add("Paso 2 - Comprador: Código Postal (BT-53) es obligatorio");
            else if (!IsPostcodeValidForCountry(buyerPostcode, (BuyerCountryTextBox.Text ?? string.Empty).Trim().ToUpper())) 
                errors.Add("Paso 2 - Comprador: Código Postal inválido");
            if ((BuyerCountryTextBox.Text ?? string.Empty).Trim().Length != 2) 
                errors.Add("Paso 2 - Comprador: País debe ser ISO-2 (ej: DE)");

            // ===== PASO 2: PRODUCTOS =====
            if (productos.Count == 0) 
                errors.Add("Paso 3 - Productos: Agrega al menos una línea");

            // ===== PASO 3: PAGOS =====
            var bt10 = (BuyerReferenceTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(bt10)) 
                errors.Add("Paso 4 - Pagos: Referencia del Comprador (BT-10) es obligatoria");
            var iban = (IBANTextBox.Text ?? string.Empty).Replace(" ", string.Empty);
            if (string.IsNullOrEmpty(iban)) 
                errors.Add("Paso 4 - Pagos: IBAN es obligatorio");
            else if (iban.Length != 22) 
                errors.Add("Paso 4 - Pagos: IBAN debe tener 22 caracteres (sin espacios)");
            else if (!IsValidIbanMod97(iban)) 
                errors.Add("Paso 4 - Pagos: IBAN no supera validación (mod 97)");

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

        private void UpdateCountryValidation(TextBox tb)
        {
            var txt = (tb.Text ?? string.Empty).Trim().ToUpper();
            if (txt.Length == 0)
            {
                SetError(tb, "Obligatorio (código ISO-2)");
            }
            else if (txt.Length != 2)
            {
                SetError(tb, "Debe tener 2 letras (ISO-3166)");
            }
            else
            {
                SetError(tb, null);
            }
        }

        private void UpdateBuyerRefValidation()
        {
            var txt = (BuyerReferenceTextBox.Text ?? string.Empty).Trim();
            SetError(BuyerReferenceTextBox, string.IsNullOrEmpty(txt) ? "Obligatorio (BT-10)" : null);
        }

        private void UpdateBuyerCityValidation()
        {
            var txt = (BuyerCityTextBox.Text ?? string.Empty).Trim();
            SetError(BuyerCityTextBox, string.IsNullOrEmpty(txt) ? "Obligatorio (BT-52)" : null);
        }

        private void UpdateBuyerPostcodeValidation()
        {
            var txt = (BuyerPostcodeTextBox.Text ?? string.Empty).Trim();
            SetError(BuyerPostcodeTextBox, string.IsNullOrEmpty(txt) ? "Obligatorio (BT-53)" : null);
        }

        private void UpdateBuyerEmailValidation()
        {
            var txt = (BuyerEmailTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(txt))
            {
                SetError(BuyerEmailTextBox, "Obligatorio (PEPPOL-EN16931-R010)");
                return;
            }
            if (!Regex.IsMatch(txt, @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$"))
            {
                SetError(BuyerEmailTextBox, "Formato de email inválido");
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
                SetError(SellerVATTextBox, "Obligatorio");
                return;
            }
            if (country.Length != 2)
            {
                SetError(SellerVATTextBox, "Primero complete el país (2 letras)");
                return;
            }
            SetError(SellerVATTextBox, EsVATValidoSilencioso(vat, country) ? null : "Formato VAT no válido para " + country);
        }

        private void UpdateIbanValidation()
        {
            var txt = (IBANTextBox.Text ?? string.Empty).Replace(" ", string.Empty).ToUpper();
            if (string.IsNullOrEmpty(txt)) { SetError(IBANTextBox, "Obligatorio"); return; }
            if (txt.Length != 22) { SetError(IBANTextBox, "IBAN DE debe tener 22 caracteres"); return; }
            if (!IsValidIbanMod97(txt)) { SetError(IBANTextBox, "IBAN no supera validación (mod 97)"); return; }
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

        private void UpdateEmailValidation(TextBox tb)
        {
            var v = (tb.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(v)) { SetError(tb, null); return; }
            // Regex simple y segura
            bool ok = Regex.IsMatch(v, @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$");
            SetError(tb, ok ? null : "Email inválido");
        }

        private void UpdatePhoneValidation(TextBox tb)
        {
            var v = (tb.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(v)) { SetError(tb, null); return; }
            // Permitir +, espacios, guiones y dígitos, longitud mínima 7
            bool ok = Regex.IsMatch(v, @"^[+\d][\d\-\s()]{6,}$");
            SetError(tb, ok ? null : "Teléfono inválido");
        }

        private void UpdatePostcodeValidation(TextBox zipTb, TextBox countryTb)
        {
            var zip = (zipTb.Text ?? string.Empty).Trim();
            var country = (countryTb.Text ?? string.Empty).Trim().ToUpper();
            if (string.IsNullOrEmpty(zip)) return; // No cambiar el error existente si está vacío
            bool ok = IsPostcodeValidForCountry(zip, country);
            SetError(zipTb, ok ? null : "CP inválido para " + country);
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
                    if (string.IsNullOrWhiteSpace(SellerNameTextBox.Text)) { SetError(SellerNameTextBox, "Obligatorio"); return false; }
                    if (HasError(SellerVATTextBox)) return false;
                    if (HasError(SellerCountryTextBox)) return false;
                    SetError(SellerNameTextBox, null);
                    return true;                // Paso 2: Receptor
                case 1:
                    UpdateCountryValidation(BuyerCountryTextBox);
                    UpdateBuyerCityValidation();
                    UpdateBuyerPostcodeValidation();
                    UpdateBuyerEmailValidation();
                    
                    if (string.IsNullOrWhiteSpace(BuyerNameTextBox.Text)) { SetError(BuyerNameTextBox, "Obligatorio"); return false; }
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
                        MessageBox.Show("Agrega al menos una línea de producto.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"No se pudo abrir la previsualización: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Normaliza a mayúsculas y limita a ISO-2 (letras) manteniendo el caret
        private void Country_Uppercase_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb) return;
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
            if (sender is TextBox tb)
            {
                tb.Text = (tb.Text ?? string.Empty).Trim().ToUpperInvariant();
                if (tb == SellerVATTextBox) UpdateVatValidation();
            }
        }

        // Restringe a números con separador decimal (coma o punto)
        private void OnlyNumeric_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb) return;
            string text = tb.Text ?? string.Empty;
            // Inyectar el nuevo carácter respetando selección
            int selStart = tb.SelectionStart;
            int selLength = tb.SelectionLength;
            string next = text.Remove(selStart, Math.Min(selLength, text.Length - selStart)).Insert(selStart, e.Text);
            e.Handled = !Regex.IsMatch(next, @"^\d*([\.,]\d{0,4})?$");
        }

        private void OnlyNumeric_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox tb) return;
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
            if (sender is not TextBox tb) return;

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
                MessageBox.Show($"VAT-ID no válido para el país {countryCode}.\nEjemplos: DE123456789, ESX1234567.", "Validación VAT", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return ok;
        }
    }
}
