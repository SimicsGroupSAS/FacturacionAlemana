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

        public PreviewWindow(Factura factura, string outputPath)
        {
            InitializeComponent();
            this.factura = factura;
            this.outputPath = outputPath;
            this.Loaded += PreviewWindow_Loaded;
        }

        private void PreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                MostrarPrevisualización();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar la previsualización: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MostrarPrevisualización()
        {
            PreviewPanel.Children.Clear();

            // Título
            var titulo = new TextBlock
            {
                Text = "PREVISUALIZACIÓN DE FACTURA",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20),
                TextAlignment = TextAlignment.Center
            };
            PreviewPanel.Children.Add(titulo);

            // Datos del Vendedor
            var seccionVendedor = CrearSeccion("DATOS DEL VENDEDOR", new[]
            {
                ($"Nombre: {factura.SellerName}", ""),
                ($"Dirección: {factura.SellerLineOne}", $"{factura.SellerLineTwo}"),
                ($"Código Postal: {factura.SellerPostcodeCode}", $"Ciudad: {factura.SellerCityName}"),
                ($"País: {factura.SellerCountryID}", $"ID VAT: {factura.SellerVATID}"),
                ($"Contacto: {factura.SellerPersonName}", $"Teléfono: {factura.SellerCompleteNumber}"),
                ($"Email: {factura.SellerEmail}", "")
            });
            PreviewPanel.Children.Add(seccionVendedor);

            // Datos del Comprador
            var seccionComprador = CrearSeccion("DATOS DEL COMPRADOR", new[]
            {
                ($"Nombre: {factura.BuyerName}", ""),
                ($"Contacto: {factura.BuyerPersonName}", ""),
                ($"Dirección: {factura.BuyerLineOne}", $"Código: {factura.BuyerPostcodeCode}"),
                ($"Ciudad: {factura.BuyerCityName}", $"País: {factura.BuyerCountryID}")
            });
            PreviewPanel.Children.Add(seccionComprador);            // Datos de la Factura
            var seccionFactura = CrearSeccion("DATOS DE LA FACTURA", new[]
            {
                ($"Número: {factura.IdElement}", $"Fecha: {factura.DueDate}"),
                ($"Moneda: {ConvertirMonedaASimolo(factura.CurrencyID)}", "")
            });
            PreviewPanel.Children.Add(seccionFactura);

            // Tabla de Productos
            var seccionProductos = new TextBlock
            {
                Text = "PRODUCTOS",
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
                    Text = $"ID: {producto.Id} | {producto.Descripcion}\n"
                });
                productosInfo.Inlines.Add(new System.Windows.Documents.Run
                {
                    Text = $"  Cantidad: {producto.Cantidad:F2} | Precio Unit.: {producto.PrecioUnitario:F2} {ConvertirMonedaASimolo(factura.CurrencyID)} | Total: {producto.PrecioTotal:F2} {ConvertirMonedaASimolo(factura.CurrencyID)}\n"
                });
            }

            PreviewPanel.Children.Add(productosInfo);

            // Totales
            var seccionTotales = CrearSeccion("TOTALES", new[]
            {
                ($"Subtotal (Netto): {factura.BasisAmount} {ConvertirMonedaASimolo(factura.CurrencyID)}", ""),
                ($"Impuestos: {factura.CalculatedAmount} {ConvertirMonedaASimolo(factura.CurrencyID)}", ""),
                ($"TOTAL: {factura.GrandTotalAmount} {ConvertirMonedaASimolo(factura.CurrencyID)}", "")
            });
            PreviewPanel.Children.Add(seccionTotales);

            // Condiciones de Pago
            var seccionPago = CrearSeccion("CONDICIONES DE PAGO", new[]
            {
                (factura.PaymentDescription, "")
            });
            PreviewPanel.Children.Add(seccionPago);

            // Información adicional
            var infoAdicional = new TextBlock
            {
                Text = $"\nPDF será generado en: {outputPath}",
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
        }        private void OnGenerarClick(object sender, RoutedEventArgs e)
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
