using System;
using System.Windows;
using System.Windows.Controls;
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
        }

        private void PreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Actualizar títulos y botones según localización
                if (_localization != null)
                {
                    this.Title = _localization.Exists("Preview.WindowTitle") ? _localization.Get("Preview.WindowTitle") : this.Title;
                    if (FindName("GenerarButton") is Button genBtn && _localization.Exists("Preview.Generate")) genBtn.Content = _localization.Get("Preview.Generate");
                    if (FindName("CancelarButton") is Button cancBtn && _localization.Exists("Preview.Cancel")) cancBtn.Content = _localization.Get("Preview.Cancel");
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
            PreviewPanel.Children.Clear();

            var loc = _localization;

            // Título (grande)
            var titulo = new TextBlock
            {
                Text = loc != null && loc.Exists("Preview.PreviewTitle") ? loc.Get("Preview.PreviewTitle") : "PREVISUALIZACIÓN DE FACTURA",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20),
                TextAlignment = TextAlignment.Center
            };
            PreviewPanel.Children.Add(titulo);

            // Datos del Vendedor
            var sellerHeader = loc != null && loc.Exists("Preview.SellerSection") ? loc.Get("Preview.SellerSection") : "DATOS DEL VENDEDOR";
            var seccionVendedor = CrearSeccion(sellerHeader, new[]
            {
                (string.Format(loc != null && loc.Exists("Preview.NameLabel") ? loc.Get("Preview.NameLabel") : "Nombre: {0}", factura.SellerName), ""),
                (string.Format(loc != null && loc.Exists("Preview.AddressLabel") ? loc.Get("Preview.AddressLabel") : "Dirección: {0}", factura.SellerLineOne), factura.SellerLineTwo ?? string.Empty),
                (string.Format(loc != null && loc.Exists("Preview.PostcodeLabel") ? loc.Get("Preview.PostcodeLabel") : "Código Postal: {0}", factura.SellerPostcodeCode), string.Format(loc != null && loc.Exists("Preview.CityLabel") ? loc.Get("Preview.CityLabel") : "Ciudad: {0}", factura.SellerCityName)),
                (string.Format(loc != null && loc.Exists("Preview.CountryLabel") ? loc.Get("Preview.CountryLabel") : "País: {0}", factura.SellerCountryID), string.Format(loc != null && loc.Exists("Preview.VatLabel") ? loc.Get("Preview.VatLabel") : "ID VAT: {0}", factura.SellerVATID)),
                (string.Format(loc != null && loc.Exists("Preview.ContactLabel") ? loc.Get("Preview.ContactLabel") : "Contacto: {0}", factura.SellerPersonName), string.Format(loc != null && loc.Exists("Preview.PhoneLabel") ? loc.Get("Preview.PhoneLabel") : "Teléfono: {0}", factura.SellerCompleteNumber)),
                (string.Format(loc != null && loc.Exists("Preview.EmailLabel") ? loc.Get("Preview.EmailLabel") : "Email: {0}", factura.SellerEmail), "")
            });
            PreviewPanel.Children.Add(seccionVendedor);

            // Datos del Comprador
            var buyerHeader = loc != null && loc.Exists("Preview.BuyerSection") ? loc.Get("Preview.BuyerSection") : "DATOS DEL COMPRADOR";
            var seccionComprador = CrearSeccion(buyerHeader, new[]
            {
                (string.Format(loc != null && loc.Exists("Preview.NameLabel") ? loc.Get("Preview.NameLabel") : "Nombre: {0}", factura.BuyerName), ""),
                (string.Format(loc != null && loc.Exists("Preview.ContactLabel") ? loc.Get("Preview.ContactLabel") : "Contacto: {0}", factura.BuyerPersonName), ""),
                (string.Format(loc != null && loc.Exists("Preview.AddressLabel") ? loc.Get("Preview.AddressLabel") : "Dirección: {0}", factura.BuyerLineOne), string.Format(loc != null && loc.Exists("Preview.PostcodeLabel") ? loc.Get("Preview.PostcodeLabel") : "Código: {0}", factura.BuyerPostcodeCode)),
                (string.Format(loc != null && loc.Exists("Preview.CityLabel") ? loc.Get("Preview.CityLabel") : "Ciudad: {0}", factura.BuyerCityName), string.Format(loc != null && loc.Exists("Preview.CountryLabel") ? loc.Get("Preview.CountryLabel") : "País: {0}", factura.BuyerCountryID))
            });
            PreviewPanel.Children.Add(seccionComprador);

            // Datos de la Factura
            var invoiceHeader = loc != null && loc.Exists("Preview.InvoiceDataSection") ? loc.Get("Preview.InvoiceDataSection") : "DATOS DE LA FACTURA";
            var seccionFactura = CrearSeccion(invoiceHeader, new[]
            {
                (string.Format(loc != null && loc.Exists("Preview.NumberLabel") ? loc.Get("Preview.NumberLabel") : "Número: {0}", factura.IdElement), string.Format(loc != null && loc.Exists("Preview.DateLabel") ? loc.Get("Preview.DateLabel") : "Fecha: {0}", factura.DueDate)),
                (string.Format(loc != null && loc.Exists("Preview.CurrencyLabel") ? loc.Get("Preview.CurrencyLabel") : "Moneda: {0}", ConvertirMonedaASimolo(factura.CurrencyID)), "")
            });
            PreviewPanel.Children.Add(seccionFactura);

            // Tabla de Productos
            var productosTitle = loc != null && loc.Exists("Preview.ProductsSection") ? loc.Get("Preview.ProductsSection") : "PRODUCTOS";
            var seccionProductos = new TextBlock
            {
                Text = productosTitle,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 20, 0, 10)
            };
            PreviewPanel.Children.Add(seccionProductos);

            var productosInfo = new TextBlock
            {
                Margin = new Thickness(0, 0, 0, 20),
                LineHeight = 25
            };

            foreach (var producto in factura.Productos)
            {
                productosInfo.Inlines.Add(new System.Windows.Documents.Run
                {
                    Text = string.Format(loc != null && loc.Exists("Preview.ProductLineFormat") ? loc.Get("Preview.ProductLineFormat") : "Pos: {0} | {1}\n", producto.Pos, producto.Descripcion)
                });
                productosInfo.Inlines.Add(new System.Windows.Documents.Run
                {
                    Text = string.Format(loc != null && loc.Exists("Preview.ProductDetailsFormat") ? loc.Get("Preview.ProductDetailsFormat") : "  Cantidad: {0:F2} | Precio Unit.: {1:F2} {2} | Total: {3:F2} {2}\n",
                        producto.Cantidad, producto.PrecioUnitario, ConvertirMonedaASimolo(factura.CurrencyID), producto.PrecioTotal)
                });
            }

            PreviewPanel.Children.Add(productosInfo);

            // Totales
            var totalsHeader = loc != null && loc.Exists("Preview.TotalsSection") ? loc.Get("Preview.TotalsSection") : "TOTALES";
            var seccionTotales = CrearSeccion(totalsHeader, new[]
            {
                (string.Format(loc != null && loc.Exists("Preview.SubtotalLabel") ? loc.Get("Preview.SubtotalLabel") : "Subtotal (Netto): {0}", factura.BasisAmount), ""),
                (string.Format(loc != null && loc.Exists("Preview.TaxesLabel") ? loc.Get("Preview.TaxesLabel") : "Impuestos: {0}", factura.CalculatedAmount), ""),
                (string.Format(loc != null && loc.Exists("Preview.TotalLabel") ? loc.Get("Preview.TotalLabel") : "TOTAL: {0}", factura.GrandTotalAmount), "")
            });
            PreviewPanel.Children.Add(seccionTotales);

            // Condiciones de Pago
            var paymentHeader = loc != null && loc.Exists("Preview.PaymentTermsSection") ? loc.Get("Preview.PaymentTermsSection") : "CONDICIONES DE PAGO";
            var seccionPago = CrearSeccion(paymentHeader, new[]
            {
                (factura.PaymentDescription ?? string.Empty, "")
            });
            PreviewPanel.Children.Add(seccionPago);

            // Información adicional
            var infoAdicional = new TextBlock
            {
                Text = loc != null && loc.Exists("Preview.PdfWillBeGenerated") ? string.Format(loc.Get("Preview.PdfWillBeGenerated"), outputPath) : $"\nPDF será generado en: {outputPath}",
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 20, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            PreviewPanel.Children.Add(infoAdicional);
        }

        private StackPanel CrearSeccion(string titulo, (string, string)[] datos)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 15, 0, 0) };

            var tituloBlock = new TextBlock
            {
                Text = titulo,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = System.Windows.Media.Brushes.DarkBlue
            };
            panel.Children.Add(tituloBlock);

            foreach (var (dato1, dato2) in datos)
            {
                if (string.IsNullOrEmpty(dato2))
                {
                    var block = new TextBlock
                    {
                        Text = dato1,
                        Margin = new Thickness(15, 3, 0, 3),
                        TextWrapping = TextWrapping.Wrap
                    };
                    panel.Children.Add(block);
                }
                else
                {
                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var block1 = new TextBlock
                    {
                        Text = dato1,
                        Margin = new Thickness(15, 3, 10, 3),
                        TextWrapping = TextWrapping.Wrap
                    };
                    Grid.SetColumn(block1, 0);
                    row.Children.Add(block1);

                    var block2 = new TextBlock
                    {
                        Text = dato2,
                        Margin = new Thickness(10, 3, 0, 3),
                        TextWrapping = TextWrapping.Wrap
                    };
                    Grid.SetColumn(block2, 1);
                    row.Children.Add(block2);

                    panel.Children.Add(row);
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
        }
    }
}
