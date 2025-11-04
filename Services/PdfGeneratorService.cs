using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using FacturacionAlemana.Models;
using System.IO;
using System.Reflection;

namespace FacturacionAlemana.Services
{
    public static class PdfGeneratorService
    {
        private static string ConvertirMonedaASimolo(string? codigoMoneda)
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
        public static byte[] GenerarFacturaPdfEnMemoria(Factura factura)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            int pageNumber = 1;
            int totalPages = 1;
            var document = CrearDocumento(factura, pageNumber, totalPages);
            return document.GeneratePdf();
        }

        public static void GenerarFacturaPdf(Factura factura, string outputPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            int pageNumber = 1;
            int totalPages = 1;
            var document = CrearDocumento(factura, pageNumber, totalPages);
            document.GeneratePdf(outputPath);
        }

        private static IDocument CrearDocumento(Factura factura, int pageNumber, int totalPages)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);

                    var assembly = Assembly.GetExecutingAssembly();
                    var resourceName = "FacturacionAlemana.Assets.plantilla.png";

                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            page.Background().Image(stream, ImageScaling.FitArea);
                        }
                        else
                        {
                            throw new FileNotFoundException("No se pudo cargar la plantilla embebida.");
                        }
                    }

                    page.Content().PaddingVertical(50).Column(column =>
                    {
                        // Título centrado
                        column.Item().AlignCenter().Text("Rechnung").FontSize(30).FontFamily("Century Gothic").Bold();

                        // Línea horizontal
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().BorderBottom(1).BorderColor(Colors.Black).Height(1);
                        });
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().BorderBottom(1).BorderColor(Colors.Black).Height(1);
                        });

                        column.Item().PaddingTop(15);

                        // Columnas: Información del vendedor y datos de factura
                        column.Item().Row(row =>
                        {
                            // Columna izquierda
                            row.RelativeItem(1).Column(leftColumn =>
                            {
                                leftColumn.Item().Text($"{factura.SellerName}").FontSize(12).Bold();
                                leftColumn.Item().Text($"{factura.SellerLineOne}, {factura.SellerPostcodeCode} {factura.SellerCityName}, {factura.SellerCountryID}").FontSize(10);
                                leftColumn.Item().Text($"Käufername: {factura.BuyerName}").FontSize(10);
                                leftColumn.Item().Text($"Kontaktperson: {factura.BuyerPersonName}").FontSize(10);
                                leftColumn.Item().Text($"Adresse: {factura.BuyerCityName}, {factura.BuyerPostcodeCode}, {factura.BuyerCountryID}").FontSize(10);
                            });

                            // Columna derecha
                            row.RelativeItem(1).Column(rightColumn =>
                            {
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).AlignLeft().Text("Rechnung").FontSize(10).Bold();
                                    subRow.RelativeItem(1).AlignRight().Text(factura.IdElement).FontSize(10);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).AlignLeft().Text("Datum").FontSize(10).Bold();
                                    subRow.RelativeItem(1).AlignRight().Text(factura.DueDate).FontSize(10);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).AlignLeft().Text("Verkäufer-ID").FontSize(10).Bold();
                                    subRow.RelativeItem(1).AlignRight().Text(factura.SellerVATID).FontSize(10);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).AlignLeft().Text("Kontaktperson").FontSize(10).Bold();
                                    subRow.RelativeItem(1).AlignRight().Text(factura.SellerPersonName).FontSize(10);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).AlignLeft().Text("Telefon").FontSize(10).Bold();
                                    subRow.RelativeItem(1).AlignRight().Text(factura.SellerCompleteNumber).FontSize(10);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).AlignLeft().Text("E-Mail").FontSize(10).Bold();
                                    subRow.RelativeItem(1).AlignRight().Text(factura.SellerEmail).FontSize(10);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).AlignLeft().Text("Seiten").FontSize(10).Bold();
                                    subRow.RelativeItem(1).AlignRight().Text($"{pageNumber} von {totalPages}").FontSize(10);
                                });
                            });
                        });

                        // Tabla de productos
                        column.Item().PaddingVertical(20).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            // Encabezados
                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).BorderColor(Colors.Black).AlignCenter().Text("ID").FontSize(10).Bold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Black).AlignCenter().Text("Beschreibung").FontSize(10).Bold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Black).AlignCenter().Text("Menge").FontSize(10).Bold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Black).AlignCenter().Text("Einzelpreis").FontSize(10).Bold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Black).AlignCenter().Text("Gesamtpreis").FontSize(10).Bold();
                            });                            // Filas de la tabla
                            for (int i = 0; i < factura.Productos.Count; i++)
                            {
                                var producto = factura.Productos[i];

                                var borderColor = (i == factura.Productos.Count - 1) ? Colors.Black : Colors.Grey.Lighten2;                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(5).AlignCenter().Text(producto.Id).FontSize(10);
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(5).AlignCenter().Text(producto.Descripcion).FontSize(10);
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(5).AlignCenter().Text(producto.Cantidad.ToString("G")).FontSize(10);
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(5).AlignCenter().Text($"{producto.PrecioUnitario:F2} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(10);
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(5).AlignCenter().Text($"{producto.PrecioTotal:F2} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(10);
                            }
                        });

                        // Totales
                        column.Item().Row(row =>
                        {
                            // Columna izquierda: Condiciones de Pago
                            row.RelativeItem(1).PaddingRight(10).Column(leftColumn =>
                            {
                                leftColumn.Item().Text("Zahlungsbedingungen").FontSize(10).Bold();
                                leftColumn.Item().Text(factura.PaymentDescription).FontSize(10);
                            });

                            // Columna derecha
                            row.RelativeItem(1).Column(rightColumn =>
                            {                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).Padding(5).AlignLeft().Text("Netto-Wert").FontSize(10).Bold();
                                    subRow.RelativeItem(1).Padding(5).AlignRight().Text($"{factura.BasisAmount} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(10);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).Padding(5).AlignLeft().Text("Gesamtsteuer").FontSize(10).Bold();
                                    subRow.RelativeItem(1).Padding(5).AlignRight().Text($"{factura.TaxAmount} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(10);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Height(1);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).Padding(5).AlignLeft().Text("Gesamtbetrag").FontSize(10).Bold();
                                    subRow.RelativeItem(1).Padding(5).AlignRight().Text($"{factura.GrandTotalAmount} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(10);
                                });
                            });
                        });
                    });
                });
            });
        }
    }
}
