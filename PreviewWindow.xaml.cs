using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FacturacionAlemana.Models;
using FacturacionAlemana.Services;

namespace FacturacionAlemana
{
    public partial class PreviewWindow : Window
    {
        private Factura factura;
        private string outputPath;
        private LocalizationService? _localization;

        public PreviewWindow(Factura factura, string outputPath)
        {
            InitializeComponent();
            _localization = LocalizationService.Instance;
            this.factura = factura;
            this.outputPath = outputPath;
            this.Loaded += PreviewWindow_Loaded;
        }        private void PreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Actualizar títulos y botones según localización
                if (_localization != null)
                {
                    this.Title = _localization.Exists("Preview.WindowTitle") ? _localization.Get("Preview.WindowTitle") : this.Title;
                    if (FindName("GenerarButton") is Button genBtn && _localization.Exists("Preview.Generate")) genBtn.Content = _localization.Get("Preview.Generate");
                    if (FindName("CancelarButton") is Button cancBtn && _localization.Exists("Preview.Cancel")) cancBtn.Content = _localization.Get("Preview.Cancel");
                      // Actualizar labels de la tabla de productos
                    if (FindName("ProductsHeaderLabel") is TextBlock prodHeader && _localization.Exists("Preview.ProductsSection")) 
                        prodHeader.Text = _localization.Get("Preview.ProductsSection");
                    if (FindName("PosColumnLabel") is TextBlock posCol && _localization.Exists("Preview.PosLabel")) 
                        posCol.Text = _localization.Get("Preview.PosLabel");
                    if (FindName("ArticleColumnLabel") is TextBlock artCol && _localization.Exists("Preview.ArticleLabel")) 
                        artCol.Text = _localization.Get("Preview.ArticleLabel");
                    if (FindName("QuantityColumnLabel") is TextBlock qtyCol && _localization.Exists("Preview.QuantityLabel")) 
                        qtyCol.Text = _localization.Get("Preview.QuantityLabel");
                    if (FindName("UnitPriceColumnLabel") is TextBlock unitCol && _localization.Exists("Preview.UnitPriceLabel")) 
                        unitCol.Text = _localization.Get("Preview.UnitPriceLabel");
                    if (FindName("TotalPriceColumnLabel") is TextBlock totalCol && _localization.Exists("Preview.TotalPriceLabel")) 
                        totalCol.Text = _localization.Get("Preview.TotalPriceLabel");
                    
                    // Actualizar labels de campos adicionales
                    if (FindName("PeriodColumnLabel") is TextBlock periodCol && _localization.Exists("Preview.PeriodLabel")) 
                        periodCol.Text = _localization.Get("Preview.PeriodLabel");
                    if (FindName("SellerCodeColumnLabel") is TextBlock sellerCodeCol && _localization.Exists("Preview.SellerCodeLabel")) 
                        sellerCodeCol.Text = _localization.Get("Preview.SellerCodeLabel");
                    if (FindName("BuyerCodeColumnLabel") is TextBlock buyerCodeCol && _localization.Exists("Preview.BuyerCodeLabel")) 
                        buyerCodeCol.Text = _localization.Get("Preview.BuyerCodeLabel");
                    if (FindName("OrderLineColumnLabel") is TextBlock orderLineCol && _localization.Exists("Preview.OrderLineLabel")) 
                        orderLineCol.Text = _localization.Get("Preview.OrderLineLabel");
                }

                MostrarPrevisualizacion();
            }
            catch (Exception ex)
            {
                var msg = (_localization != null && _localization.Exists("Messages.PreviewError"))
                    ? string.Format(_localization.Get("Messages.PreviewError"), ex.Message)
                    : $"Error al generar la previsualización: {ex.Message}";
                var title = (_localization != null && _localization.Exists("Messages.ErrorTitle"))
                    ? _localization.Get("Messages.ErrorTitle")
                    : "Error";
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MostrarPrevisualizacion()
        {
            // Obtener referencias a los controles definidos en XAML
            var previewTitle = FindName("PreviewTitle") as TextBlock;
            var sellerPanel = FindName("SellerPanel") as StackPanel;
            var buyerPanel = FindName("BuyerPanel") as StackPanel;
            var invoicePanel = FindName("InvoicePanel") as StackPanel;
            var productsItems = FindName("ProductsItems") as ItemsControl;
            var totalsPanel = FindName("TotalsPanel") as StackPanel;
            var paymentPanel = FindName("PaymentPanel") as StackPanel;
            var infoTextBlock = FindName("InfoTextBlock") as TextBlock;

            try
            {
                sellerPanel?.Children.Clear();
                buyerPanel?.Children.Clear();
                invoicePanel?.Children.Clear();
                productsItems?.Items.Clear();
                totalsPanel?.Children.Clear();
                paymentPanel?.Children.Clear();
                if (infoTextBlock != null) infoTextBlock.Text = string.Empty;

                var loc = _localization;

                // Título
                if (previewTitle != null)
                {
                    previewTitle.Text = loc != null && loc.Exists("Preview.PreviewTitle") ? loc.Get("Preview.PreviewTitle") : "PREVISUALIZACIÓN DE FACTURA";
                }                // Sección Vendedor
                var sellerHeader = loc != null && loc.Exists("Preview.SellerSection") ? loc.Get("Preview.SellerSection") : "DATOS DEL VENDEDOR";
                var sellerAddressLine = JoinNonDuplicates(" ", factura.SellerLineOne, factura.SellerLineTwo);
                var seccionVendedor = CrearSeccion(sellerHeader, new[]
                {
                    (string.Format(loc != null && loc.Exists("Preview.NameLabel") ? loc.Get("Preview.NameLabel") : "Nombre: {0}", factura.SellerName), ""),
                    (string.Format(loc != null && loc.Exists("Preview.AddressLabel") ? loc.Get("Preview.AddressLabel") : "Dirección: {0}", sellerAddressLine), ""),
                    (string.Format(loc != null && loc.Exists("Preview.PostcodeLabel") ? loc.Get("Preview.PostcodeLabel") : "Código Postal: {0}", factura.SellerPostcodeCode), string.Format(loc != null && loc.Exists("Preview.CityLabel") ? loc.Get("Preview.CityLabel") : "Ciudad: {0}", factura.SellerCityName)),
                    (string.Format(loc != null && loc.Exists("Preview.CountryLabel") ? loc.Get("Preview.CountryLabel") : "País: {0}", factura.SellerCountryID), string.Format(loc != null && loc.Exists("Preview.VatLabel") ? loc.Get("Preview.VatLabel") : "ID VAT: {0}", factura.SellerVATID)),
                    (string.Format(loc != null && loc.Exists("Preview.ContactLabel") ? loc.Get("Preview.ContactLabel") : "Contacto: {0}", factura.SellerPersonName), string.Format(loc != null && loc.Exists("Preview.PhoneLabel") ? loc.Get("Preview.PhoneLabel") : "Teléfono: {0}", factura.SellerCompleteNumber)),
                    (string.Format(loc != null && loc.Exists("Preview.EmailLabel") ? loc.Get("Preview.EmailLabel") : "Email: {0}", factura.SellerEmail), "")
                });
                sellerPanel?.Children.Add(seccionVendedor);                // Sección Comprador
                var buyerHeader = loc != null && loc.Exists("Preview.BuyerSection") ? loc.Get("Preview.BuyerSection") : "DATOS DEL COMPRADOR";
                var buyerAddressLine = JoinNonDuplicates(" ", factura.BuyerLineOne, factura.BuyerLineTwo);
                var seccionComprador = CrearSeccion(buyerHeader, new[]
                {
                    (string.Format(loc != null && loc.Exists("Preview.NameLabel") ? loc.Get("Preview.NameLabel") : "Nombre: {0}", factura.BuyerName), ""),
                    (string.Format(loc != null && loc.Exists("Preview.AddressLabel") ? loc.Get("Preview.AddressLabel") : "Dirección: {0}", buyerAddressLine), ""),
                    (string.Format(loc != null && loc.Exists("Preview.PostcodeLabel") ? loc.Get("Preview.PostcodeLabel") : "Código Postal: {0}", factura.BuyerPostcodeCode), string.Format(loc != null && loc.Exists("Preview.CityLabel") ? loc.Get("Preview.CityLabel") : "Ciudad: {0}", factura.BuyerCityName)),
                    (string.Format(loc != null && loc.Exists("Preview.CountryLabel") ? loc.Get("Preview.CountryLabel") : "País: {0}", factura.BuyerCountryID), string.Format(loc != null && loc.Exists("Preview.VatLabel") ? loc.Get("Preview.VatLabel") : "ID VAT: {0}", factura.BuyerVATID)),
                    (string.Format(loc != null && loc.Exists("Preview.EmailLabel") ? loc.Get("Preview.EmailLabel") : "Email: {0}", factura.BuyerEmail), "")
                });
                buyerPanel?.Children.Add(seccionComprador);

                // Sección Factura
                var invoiceHeader = loc != null && loc.Exists("Preview.InvoiceDataSection") ? loc.Get("Preview.InvoiceDataSection") : "DATOS DE LA FACTURA";
                var seccionFactura = CrearSeccion(invoiceHeader, new[]
                {
                    (string.Format(loc != null && loc.Exists("Preview.NumberLabel") ? loc.Get("Preview.NumberLabel") : "Número: {0}", factura.IdElement), string.Format(loc != null && loc.Exists("Preview.DateLabel") ? loc.Get("Preview.DateLabel") : "Fecha: {0}", factura.DueDate)),
                    (string.Format(loc != null && loc.Exists("Preview.CurrencyLabel") ? loc.Get("Preview.CurrencyLabel") : "Moneda: {0}", ConvertirMonedaASimolo(factura.CurrencyID)), "")
                });
                invoicePanel?.Children.Add(seccionFactura);                // Productos: filas profesionales
                if (factura.Productos != null && productsItems != null)
                {
                    var monedaSimbolo = ConvertirMonedaASimolo(factura.CurrencyID);

                    foreach (var producto in factura.Productos)
                    {
                        var container = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };

                        var row = new Grid();
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(116) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(116) });

                        var cellIndex = new TextBlock { Text = producto.Pos.ToString(), VerticalAlignment = VerticalAlignment.Center, Foreground = System.Windows.Media.Brushes.DarkGray, FontWeight = FontWeights.SemiBold, FontSize = 11 };
                        Grid.SetColumn(cellIndex, 0);
                        row.Children.Add(cellIndex);

                        var cellDesc = new TextBlock { Text = producto.Name ?? producto.Descripcion ?? "", TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Foreground = System.Windows.Media.Brushes.Black, FontWeight = FontWeights.SemiBold, FontSize = 11 };
                        Grid.SetColumn(cellDesc, 1);
                        row.Children.Add(cellDesc);

                        var cellQty = new TextBlock { Text = producto.Cantidad.ToString("F2"), VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, Foreground = System.Windows.Media.Brushes.Black, FontSize = 11 };
                        Grid.SetColumn(cellQty, 2);
                        row.Children.Add(cellQty);

                        var cellUnit = new TextBlock { Text = $"{producto.PrecioUnitario:F2} {monedaSimbolo}", VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, Foreground = System.Windows.Media.Brushes.Black, FontSize = 11 };
                        Grid.SetColumn(cellUnit, 3);
                        row.Children.Add(cellUnit);

                        var cellTotal = new TextBlock { Text = $"{producto.PrecioTotal:F2} {monedaSimbolo}", VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.Black, FontSize = 11 };
                        Grid.SetColumn(cellTotal, 4);
                        row.Children.Add(cellTotal);

                        container.Children.Add(row);

                        // Detalles extendidos debajo en gris
                        if (!string.IsNullOrWhiteSpace(producto.DescripcionCompleta))
                        {
                            var details = new TextBlock
                            {
                                Text = producto.DescripcionCompleta,
                                Margin = new Thickness(56, 6, 0, 0),
                                Foreground = System.Windows.Media.Brushes.DarkGray,
                                FontSize = 10,
                                TextWrapping = TextWrapping.Wrap,
                                Opacity = 0.75
                            };
                            container.Children.Add(details);
                        }

                        productsItems.Items.Add(container);
                    }
                }

                // Totales
                var totalsHeader = loc != null && loc.Exists("Preview.TotalsSection") ? loc.Get("Preview.TotalsSection") : "TOTALES";
                var seccionTotales = CrearSeccion(totalsHeader, new[]
                {
                    (string.Format(loc != null && loc.Exists("Preview.SubtotalLabel") ? loc.Get("Preview.SubtotalLabel") : "Subtotal (Netto): {0}", factura.BasisAmount), ""),
                    (string.Format(loc != null && loc.Exists("Preview.TaxesLabel") ? loc.Get("Preview.TaxesLabel") : "Impuestos: {0}", factura.CalculatedAmount), ""),
                    (string.Format(loc != null && loc.Exists("Preview.TotalLabel") ? loc.Get("Preview.TotalLabel") : "TOTAL: {0}", factura.GrandTotalAmount), "")
                });
                totalsPanel?.Children.Add(seccionTotales);                // Sección Referencias (si existen)
                var referencesData = new List<(string, string)>();
                if (!string.IsNullOrWhiteSpace(factura.ProjectNumber))
                    referencesData.Add((string.Format("Proyecto: {0}", factura.ProjectNumber), ""));
                if (!string.IsNullOrWhiteSpace(factura.ContractNumber))
                    referencesData.Add((string.Format("Contrato: {0}", factura.ContractNumber), ""));
                if (!string.IsNullOrWhiteSpace(factura.SalesOrderNumber))
                    referencesData.Add((string.Format("Orden de Venta: {0}", factura.SalesOrderNumber), ""));
                if (!string.IsNullOrWhiteSpace(factura.PurchaseOrderNumber))
                    referencesData.Add((string.Format("Orden de Compra: {0}", factura.PurchaseOrderNumber), ""));
                if (!string.IsNullOrWhiteSpace(factura.PaymentReference))
                    referencesData.Add((string.Format("Referencia de Pago: {0}", factura.PaymentReference), ""));

                if (referencesData.Count > 0)
                {
                    var refHeader = "REFERENCIAS";
                    var seccionReferencias = CrearSeccion(refHeader, referencesData.ToArray());
                    paymentPanel?.Children.Add(seccionReferencias);
                }

                // Sección Información de Entrega (ShipTo)
                if (!string.IsNullOrWhiteSpace(factura.ShipToName) || !string.IsNullOrWhiteSpace(factura.ShipToLineOne))
                {
                    var shiptoAddressLine = JoinNonDuplicates(" ", factura.ShipToLineOne, factura.ShipToLineTwo, factura.ShipToLineThree);
                    var seccionEntrega = CrearSeccion("INFORMACIÓN DE ENTREGA", new[]
                    {
                        (string.Format("Destinatario: {0}", factura.ShipToName), ""),
                        (string.Format("Dirección: {0}", shiptoAddressLine), ""),
                        (string.Format("Código Postal: {0}", factura.ShipToPostcodeCode), string.Format("Ciudad: {0}", factura.ShipToCityName)),
                        (string.Format("País: {0}", factura.ShipToCountryID), string.Format("Región: {0}", factura.ShipToCountrySubDivisionName))
                    });
                    paymentPanel?.Children.Add(seccionEntrega);
                }

                // Sección Información Bancaria
                var bankData = new List<(string, string)>();
                if (!string.IsNullOrWhiteSpace(factura.IBANID))
                    bankData.Add((string.Format("IBAN: {0}", factura.IBANID), ""));
                if (!string.IsNullOrWhiteSpace(factura.BICID))
                    bankData.Add((string.Format("BIC: {0}", factura.BICID), ""));
                if (!string.IsNullOrWhiteSpace(factura.BankName))
                    bankData.Add((string.Format("Banco: {0}", factura.BankName), ""));
                if (!string.IsNullOrWhiteSpace(factura.BLZ))
                    bankData.Add((string.Format("BLZ: {0}", factura.BLZ), ""));
                if (!string.IsNullOrWhiteSpace(factura.AccountName))
                    bankData.Add((string.Format("Titular: {0}", factura.AccountName), ""));

                if (bankData.Count > 0)
                {
                    var bankHeader = "INFORMACIÓN BANCARIA";
                    var seccionBanco = CrearSeccion(bankHeader, bankData.ToArray());
                    paymentPanel?.Children.Add(seccionBanco);
                }

                // Condiciones de pago
                var paymentHeader = loc != null && loc.Exists("Preview.PaymentTermsSection") ? loc.Get("Preview.PaymentTermsSection") : "CONDICIONES DE PAGO";
                var seccionPago = CrearSeccion(paymentHeader, new[] { (factura.PaymentTermsDescription ?? factura.PaymentDescription ?? string.Empty, "") });
                paymentPanel?.Children.Add(seccionPago);

                // Información adicional
                if (infoTextBlock != null)
                {
                    infoTextBlock.Text = loc != null && loc.Exists("Preview.PdfWillBeGenerated") ? string.Format(loc.Get("Preview.PdfWillBeGenerated"), outputPath) : $"PDF será generado en: {outputPath}";
                }
            }
            catch (Exception ex)
            {
                var msg = (_localization != null && _localization.Exists("Messages.PreviewError"))
                    ? string.Format(_localization.Get("Messages.PreviewError"), ex.Message)
                    : $"Error al generar la previsualización: {ex.Message}";
                var title = (_localization != null && _localization.Exists("Messages.ErrorTitle"))
                    ? _localization.Get("Messages.ErrorTitle")
                    : "Error";
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }        private StackPanel CrearSeccion(string titulo, (string, string)[] datos)
        {
            var panel = new StackPanel { Margin = new Thickness(0) };

            // Filtrar datos vacíos o que contengan "No encontrado"
            var datosValidos = datos.Where(d => 
            {
                var d1 = d.Item1 ?? "";
                var d2 = d.Item2 ?? "";
                
                // Omitir si contiene "No encontrado" o si ambos están vacíos
                if (d1.Contains("No encontrado") || d2.Contains("No encontrado"))
                    return false;
                
                // Omitir si el primer item es solo el label sin contenido (ej: "Contacto: " o "Dirección: ")
                // Detectar si es un label vacío: termina con ": " y no tiene contenido después
                if (d1.EndsWith(": ") || d1.EndsWith(": \0"))
                    return false;
                
                return !string.IsNullOrWhiteSpace(d1) || !string.IsNullOrWhiteSpace(d2);
            }).ToList();

            // Si no hay datos válidos, no mostrar la sección
            if (datosValidos.Count == 0)
                return panel;

            var tituloBlock = new TextBlock
            {
                Text = titulo,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = System.Windows.Media.Brushes.DarkSlateGray
            };
            panel.Children.Add(tituloBlock);

            foreach (var (dato1, dato2) in datosValidos)
            {
                // Omitir si contiene "No encontrado"
                if ((dato1 ?? "").Contains("No encontrado") || (dato2 ?? "").Contains("No encontrado"))
                    continue;

                if (string.IsNullOrEmpty(dato2))
                {
                    var block = new TextBlock
                    {
                        Text = dato1,
                        Margin = new Thickness(0, 4, 0, 4),
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 11,
                        Foreground = System.Windows.Media.Brushes.Black
                    };
                    panel.Children.Add(block);
                }
                else
                {
                    var block1 = new TextBlock
                    {
                        Text = dato1,
                        Margin = new Thickness(0, 4, 0, 4),
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 11,
                        Foreground = System.Windows.Media.Brushes.Black
                    };
                    panel.Children.Add(block1);

                    var block2 = new TextBlock
                    {
                        Text = dato2,
                        Margin = new Thickness(0, 4, 0, 4),
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 11,
                        Foreground = System.Windows.Media.Brushes.Black
                    };
                    panel.Children.Add(block2);
                }
            }

            return panel;
        }

        private void OnGenerarClick(object sender, RoutedEventArgs e)
        {
            try
            {
                PdfGeneratorService.GenerarFacturaPdf(factura, outputPath);
                MessageBox.Show($"Factura generada exitosamente en:\n{outputPath}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCancelarClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Convierte códigos de moneda ISO a símbolos
        /// </summary>
        private string ConvertirMonedaASimolo(string? codigoMoneda)
        {
            if (string.IsNullOrWhiteSpace(codigoMoneda))
                return "?";

            return codigoMoneda.ToUpper() switch
            {
                "EUR" => "€",
                "USD" => "$",
                "GBP" => "£",
                "JPY" => "¥",
                "CHF" => "CHF",
                "CAD" => "$",
                "AUD" => "$",
                "NZD" => "$",
                "CNY" => "¥",
                "INR" => "₹",
                "MXN" => "$",
                "BRL" => "R$",
                "ZAR" => "R",
                _ => codigoMoneda.ToUpper()
            };
        }        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                e.Handled = true;
                return;
            }

            try
            {
                this.DragMove();
                e.Handled = true;
            }
            catch { }
        }

        /// <summary>
        /// Une múltiples strings evitando duplicados consecutivos
        /// </summary>
        private string JoinNonDuplicates(string separator, params string?[] parts)
        {
            var nonEmptyParts = parts.Where(p => !string.IsNullOrWhiteSpace(p)).Cast<string>().ToList();
            
            // Evitar duplicados: si dos partes consecutivas son iguales, omitir la segunda
            var distinctParts = new List<string>();
            foreach (var part in nonEmptyParts)
            {
                if (distinctParts.Count == 0 || !distinctParts[distinctParts.Count - 1].Equals(part, StringComparison.OrdinalIgnoreCase))
                {
                    distinctParts.Add(part);
                }
            }
            
            return string.Join(separator, distinctParts);
        }
    }
}
