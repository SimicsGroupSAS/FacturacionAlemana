using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Globalization;
using FacturacionAlemana.Models;
using FacturacionAlemana.Services;

namespace FacturacionAlemana
{
    public partial class CreateInvoicePage : Page
    {
        private ObservableCollection<Producto> productos = new();        public CreateInvoicePage()
        {
            InitializeComponent();
            ProductsDataGrid.ItemsSource = productos;
            IssueDateTextBox.Text = DateTime.Now.ToString("yyyy-MM-dd");
            DeliveryDateTextBox.Text = DateTime.Now.ToString("yyyy-MM-dd");
            DueDateTextBox.Text = DateTime.Now.AddMonths(1).ToString("yyyy-MM-dd");
            CurrencyComboBox.SelectedIndex = 0;
            TaxRateTextBox.TextChanged += (s, e) => ActualizarResumenTotales();
            CurrencyComboBox.SelectionChanged += (s, e) => ActualizarResumenTotales();
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

                if (!decimal.TryParse(cantidadStr, out var cantidad) || !decimal.TryParse(precioStr, out var precio))
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

                ProductDescTextBox.Clear();
                ProductQtyTextBox.Clear();
                ProductPriceTextBox.Clear();

                ActualizarResumenTotales();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al agregar producto: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

                decimal.TryParse(TaxRateTextBox.Text, out var tasaIVA);
                if (tasaIVA <= 0) tasaIVA = 19m;

                decimal impuestos = subtotal * (tasaIVA / 100m);
                decimal total = subtotal + impuestos;

                var moneda = (CurrencyComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EUR";
                string simboloMoneda = ObtenerSimboloMoneda(moneda);

                SubtotalTextBlock.Text = $"{simboloMoneda}{subtotal:F2}";
                TaxesTextBlock.Text = $"{simboloMoneda}{impuestos:F2}";
                TotalTextBlock.Text = $"{simboloMoneda}{total:F2}";
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
        }        private void OnGenerateInvoiceClick(object sender, RoutedEventArgs e)
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
                }                // Validar VAT-ID e IBAN antes de generar la factura
                if (!ValidarDatosFactura())
                {
                    return;
                }

                var factura = CrearFacturaDesdeFormulario();

                string rutaReal = Process.GetCurrentProcess().MainModule?.FileName ?? 
                    throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable.");
                var directorioReal = Path.GetDirectoryName(rutaReal) ?? 
                    throw new InvalidOperationException("No se pudo determinar el directorio del ejecutable.");
                
                var numeroFactura = InvoiceNumberTextBox.Text.Replace("/", "_").Replace("\\", "_");
                var pdfPath = Path.Combine(directorioReal, $"Factura_{numeroFactura}.pdf");
                var xmlPath = Path.Combine(directorioReal, $"Factura_{numeroFactura}.xml");

                PdfGeneratorService.GenerarFacturaPdf(factura, pdfPath);
                XmlGeneratorService.GenerarFacturaXml(factura, xmlPath);

                MessageBox.Show($"Factura generada exitosamente.\n\nPDF: {pdfPath}\nXML: {xmlPath}", 
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar factura: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }        }        private bool ValidarDatosFactura()
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
            }            // Validar VAT-ID
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
            }            // El resto del IBAN debe ser numérico
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

            return true;
        }

        private Factura CrearFacturaDesdeFormulario()
        {
            decimal totalGeneral = 0;
            foreach (var prod in productos)
            {
                totalGeneral += prod.PrecioTotal;
            }

            decimal.TryParse(TaxRateTextBox.Text, out var tasaIVA);
            if (tasaIVA <= 0) tasaIVA = 19m;
            
            decimal totalImpuestos = totalGeneral * (tasaIVA / 100m);
            decimal totalConImpuestos = totalGeneral + totalImpuestos;

            var monedaSeleccionada = (CurrencyComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EUR";
            var categoriaImpuesto = (TaxCategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "S";            return new Factura            {
                Cliente = BuyerNameTextBox.Text,
                Total = totalConImpuestos,
                DueDate = DueDateTextBox.Text,
                GrandTotalAmount = totalConImpuestos.ToString("F2", CultureInfo.InvariantCulture),
                DuePayableAmount = totalConImpuestos.ToString("F2", CultureInfo.InvariantCulture),
                DateTimeFormat = "102",
                UnitCode = "EA",
                SchemeID = "0060",
                CurrencyID = monedaSeleccionada,
                IdElement = InvoiceNumberTextBox.Text,
                TypeCodeElement = "380",
                // Convertir fecha de yyyy-MM-dd a YYYYMMDD (formato requerido por EN 16931)
                IssueDateElement = DateTime.ParseExact(IssueDateTextBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("yyyyMMdd"),
                PaymentNoteElement = "Términos de pago: Neto 30",
                TaxAmount = totalImpuestos.ToString("F2", CultureInfo.InvariantCulture),
                
                SellerName = SellerNameTextBox.Text,
                SellerPersonName = SellerPersonNameTextBox.Text,
                SellerDepartmentName = "Ventas",
                SellerCompleteNumber = SellerPhoneTextBox.Text,                SellerEmail = SellerEmailTextBox.Text,
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
                TaxCategoryCode = categoriaImpuesto,                TaxRatePercent = tasaIVA.ToString("F0"),
                LineTotalAmount = productos[0].PrecioTotal.ToString("F2", CultureInfo.InvariantCulture),
                InvoiceCurrencyCode = monedaSeleccionada,                PaymentTypeCode = "30",
                PaymentInformation = "SEPA",
                IBANID = IBANTextBox.Text.Replace(" ", "").ToUpper(), // Remover espacios y convertir a mayúsculas
                AccountName = AccountNameTextBox.Text,
                BICID = BICTextBox.Text,
                CalculatedAmount = totalImpuestos.ToString("F2", CultureInfo.InvariantCulture), // Monto del IVA (no el total)
                BasisAmount = totalGeneral.ToString("F2", CultureInfo.InvariantCulture), // Base imponible (neto sin impuestos)
                PaymentDescription = "Pago según términos acordados",
                
                InvoiceNumber = InvoiceNumberTextBox.Text,
                IssueDate = DateTime.ParseExact(IssueDateTextBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                DeliveryDate = DateTime.ParseExact(DeliveryDateTextBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                DueDateValue = DateTime.ParseExact(DueDateTextBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                ProjectNumber = ProjectNumberTextBox.Text,
                ContractNumber = ContractNumberTextBox.Text,
                PurchaseOrderNumber = PurchaseOrderNumberTextBox.Text,
                SalesOrderNumber = SalesOrderNumberTextBox.Text,
                PaymentReference = PaymentReferenceTextBox.Text,
                
                Productos = new List<Producto>(productos),
                ShipToID = "",
                ShipToName = "",
                ShipToPostcodeCode = "",
                ShipToLineOne = "",
                ShipToLineTwo = "",
                ShipToLineThree = "",
                ShipToCityName = "",
                ShipToCountryID = "",
                ShipToCountrySubDivisionName = ""
            };
        }        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        /// <summary>
        /// Valida el formato del VAT ID según el país específico del vendedor
        /// Cumple con la norma EN 16931 / XRechnung 3.0
        /// </summary>
        private bool ValidarVATIDPorPais(string vatID, string countryCode)
        {
            vatID = vatID.Trim().ToUpper();

            // El VAT ID debe comenzar con el código de país
            if (!vatID.StartsWith(countryCode))
            {
                MessageBox.Show($"El VAT ID debe comenzar con el código de país {countryCode}.\n\n" +
                    $"Tu VAT ID: {vatID}\n" +
                    $"Formato esperado: {countryCode}... (ej: {countryCode}123456789)",
                    "Validación de VAT-ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validar según el país específico
            return countryCode switch
            {
                "DE" => ValidarVATID_Alemania(vatID),
                "ES" => ValidarVATID_Espana(vatID),
                "FR" => ValidarVATID_Francia(vatID),
                "AT" => ValidarVATID_Austria(vatID),
                "NL" => ValidarVATID_Holanda(vatID),
                "IT" => ValidarVATID_Italia(vatID),
                "BE" => ValidarVATID_Belgica(vatID),
                "PL" => ValidarVATID_Polonia(vatID),
                "CZ" => ValidarVATID_RepublicaCheca(vatID),
                "HU" => ValidarVATID_Hungria(vatID),
                _ => ValidarVATID_Generico(vatID, countryCode) // Formato genérico para otros países
            };
        }

        private bool ValidarVATID_Alemania(string vatID)
        {
            // Formato: DE + 9 dígitos = 11 caracteres totales
            if (vatID.Length != 11)
            {
                MessageBox.Show($"El VAT ID alemán debe tener 11 caracteres (DE + 9 dígitos).\n" +
                    $"Tu VAT ID tiene {vatID.Length} caracteres.\n\n" +
                    $"Ejemplo válido: DE123456789",
                    "Validación de VAT-ID (Alemania)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!vatID.Substring(2).All(char.IsDigit))
            {
                MessageBox.Show("Después de 'DE', el VAT ID debe contener solo dígitos (9 números).\n\n" +
                    "Ejemplo válido: DE123456789",
                    "Validación de VAT-ID (Alemania)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool ValidarVATID_Espana(string vatID)
        {
            // Formato: ES + letra + 7 dígitos = 10 caracteres totales
            if (vatID.Length != 10)
            {
                MessageBox.Show($"El VAT ID español debe tener 10 caracteres (ES + letra + 7 dígitos).\n" +
                    $"Tu VAT ID tiene {vatID.Length} caracteres.\n\n" +
                    $"Ejemplo válido: ESA1234567",
                    "Validación de VAT-ID (España)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!char.IsLetter(vatID[2]))
            {
                MessageBox.Show("Después de 'ES', el tercer carácter debe ser una letra.\n\n" +
                    "Ejemplo válido: ESA1234567",
                    "Validación de VAT-ID (España)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!vatID.Substring(3).All(char.IsDigit))
            {
                MessageBox.Show("Después de la letra, el VAT ID debe contener 7 dígitos.\n\n" +
                    "Ejemplo válido: ESA1234567",
                    "Validación de VAT-ID (España)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool ValidarVATID_Francia(string vatID)
        {
            // Formato: FR + 2 dígitos + 9 números = 13 caracteres totales
            if (vatID.Length != 13)
            {
                MessageBox.Show($"El VAT ID francés debe tener 13 caracteres (FR + 2 dígitos + 9 números).\n" +
                    $"Tu VAT ID tiene {vatID.Length} caracteres.\n\n" +
                    $"Ejemplo válido: FR12345678901",
                    "Validación de VAT-ID (Francia)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!vatID.Substring(2).All(char.IsDigit))
            {
                MessageBox.Show("Después de 'FR', el VAT ID debe contener solo dígitos (11 números).\n\n" +
                    "Ejemplo válido: FR12345678901",
                    "Validación de VAT-ID (Francia)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool ValidarVATID_Austria(string vatID)
        {
            // Formato: AT + 8 dígitos = 10 caracteres totales
            if (vatID.Length != 10)
            {
                MessageBox.Show($"El VAT ID austriaco debe tener 10 caracteres (AT + 8 dígitos).\n" +
                    $"Tu VAT ID tiene {vatID.Length} caracteres.\n\n" +
                    $"Ejemplo válido: AT12345678",
                    "Validación de VAT-ID (Austria)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!vatID.Substring(2).All(char.IsDigit))
            {
                MessageBox.Show("Después de 'AT', el VAT ID debe contener solo dígitos (8 números).\n\n" +
                    "Ejemplo válido: AT12345678",
                    "Validación de VAT-ID (Austria)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool ValidarVATID_Holanda(string vatID)
        {
            // Formato: NL + 12 dígitos = 14 caracteres totales
            if (vatID.Length != 14)
            {
                MessageBox.Show($"El VAT ID holandés debe tener 14 caracteres (NL + 12 dígitos).\n" +
                    $"Tu VAT ID tiene {vatID.Length} caracteres.\n\n" +
                    $"Ejemplo válido: NL123456789012",
                    "Validación de VAT-ID (Holanda)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!vatID.Substring(2).All(char.IsDigit))
            {
                MessageBox.Show("Después de 'NL', el VAT ID debe contener solo dígitos (12 números).\n\n" +
                    "Ejemplo válido: NL123456789012",
                    "Validación de VAT-ID (Holanda)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool ValidarVATID_Italia(string vatID)
        {
            // Formato: IT + 11 dígitos = 13 caracteres totales
            if (vatID.Length != 13)
            {
                MessageBox.Show($"El VAT ID italiano debe tener 13 caracteres (IT + 11 dígitos).\n" +
                    $"Tu VAT ID tiene {vatID.Length} caracteres.\n\n" +
                    $"Ejemplo válido: IT12345678901",
                    "Validación de VAT-ID (Italia)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!vatID.Substring(2).All(char.IsDigit))
            {
                MessageBox.Show("Después de 'IT', el VAT ID debe contener solo dígitos (11 números).\n\n" +
                    "Ejemplo válido: IT12345678901",
                    "Validación de VAT-ID (Italia)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool ValidarVATID_Belgica(string vatID)
        {
            // Formato: BE + 10 dígitos = 12 caracteres totales
            if (vatID.Length != 12)
            {
                MessageBox.Show($"El VAT ID belga debe tener 12 caracteres (BE + 10 dígitos).\n" +
                    $"Tu VAT ID tiene {vatID.Length} caracteres.\n\n" +
                    $"Ejemplo válido: BE1234567890",
                    "Validación de VAT-ID (Bélgica)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!vatID.Substring(2).All(char.IsDigit))
            {
                MessageBox.Show("Después de 'BE', el VAT ID debe contener solo dígitos (10 números).\n\n" +
                    "Ejemplo válido: BE1234567890",
                    "Validación de VAT-ID (Bélgica)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool ValidarVATID_Polonia(string vatID)
        {
            // Formato: PL + 10 dígitos = 12 caracteres totales
            if (vatID.Length != 12)
            {
                MessageBox.Show($"El VAT ID polaco debe tener 12 caracteres (PL + 10 dígitos).\n" +
                    $"Tu VAT ID tiene {vatID.Length} caracteres.\n\n" +
                    $"Ejemplo válido: PL1234567890",
                    "Validación de VAT-ID (Polonia)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!vatID.Substring(2).All(char.IsDigit))
            {
                MessageBox.Show("Después de 'PL', el VAT ID debe contener solo dígitos (10 números).\n\n" +
                    "Ejemplo válido: PL1234567890",
                    "Validación de VAT-ID (Polonia)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool ValidarVATID_RepublicaCheca(string vatID)
        {
            // Formato: CZ + 8 o 10 dígitos = 10 o 12 caracteres totales
            if (vatID.Length != 10 && vatID.Length != 12)
            {
                MessageBox.Show($"El VAT ID checo debe tener 10 o 12 caracteres (CZ + 8 o 10 dígitos).\n" +
                    $"Tu VAT ID tiene {vatID.Length} caracteres.\n\n" +
                    $"Ejemplos válidos: CZ12345678 o CZ1234567890",
                    "Validación de VAT-ID (República Checa)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!vatID.Substring(2).All(char.IsDigit))
            {
                MessageBox.Show("Después de 'CZ', el VAT ID debe contener solo dígitos.\n\n" +
                    "Ejemplos válidos: CZ12345678 o CZ1234567890",
                    "Validación de VAT-ID (República Checa)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool ValidarVATID_Hungria(string vatID)
        {
            // Formato: HU + 8 dígitos = 10 caracteres totales
            if (vatID.Length != 10)
            {
                MessageBox.Show($"El VAT ID húngaro debe tener 10 caracteres (HU + 8 dígitos).\n" +
                    $"Tu VAT ID tiene {vatID.Length} caracteres.\n\n" +
                    $"Ejemplo válido: HU12345678",
                    "Validación de VAT-ID (Hungría)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!vatID.Substring(2).All(char.IsDigit))
            {
                MessageBox.Show("Después de 'HU', el VAT ID debe contener solo dígitos (8 números).\n\n" +
                    "Ejemplo válido: HU12345678",
                    "Validación de VAT-ID (Hungría)", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool ValidarVATID_Generico(string vatID, string countryCode)
        {
            // Validación genérica: mínimo 4 caracteres (código país + al menos 2 dígitos)
            if (vatID.Length < 5)
            {
                MessageBox.Show($"El VAT ID para {countryCode} debe tener al menos 5 caracteres.\n" +
                    $"Tu VAT ID tiene {vatID.Length} caracteres.\n\n" +
                    $"Formato: {countryCode}... (código de país + identificación)",
                    "Validación de VAT-ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }            return true;
        }
    }
}
