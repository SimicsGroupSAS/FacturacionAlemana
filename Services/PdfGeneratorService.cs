using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using FacturacionAlemana.Models;

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
                {                    page.Size(PageSizes.A4);
                    page.Margin(0.5f, Unit.Centimetre);

                    page.Content().Column(column =>
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
                                leftColumn.Item().Text($"{factura.SellerLineOne}, {factura.SellerLineTwo}").FontSize(10);
                                leftColumn.Item().Text($"{factura.SellerPostcodeCode} {factura.SellerCityName}, {factura.SellerCountryID}").FontSize(10);
                                leftColumn.Item().Text("").FontSize(8); // Salto de línea
                                leftColumn.Item().Text($"USt-ID: {factura.SellerVATID}").FontSize(10);
                                leftColumn.Item().Text($"St.-Nr.: {factura.SellerTaxNumber}").FontSize(10);
                                leftColumn.Item().Text($"E-Adresse: {factura.SellerEmail}").FontSize(10);
                                leftColumn.Item().Text($"Web: TEXTO:WWW.CORREO.COM").FontSize(10);
                                leftColumn.Item().Text("").FontSize(8); // Salto
                                leftColumn.Item().Text("Empfänger").FontSize(12).Bold();
                                leftColumn.Item().Text($"{factura.BuyerName}").FontSize(10);
                                leftColumn.Item().Text($"{factura.BuyerLineOne}, {factura.BuyerLineTwo}").FontSize(10);
                                leftColumn.Item().Text($"{factura.BuyerPostcodeCode} {factura.BuyerCityName}, {factura.BuyerCountryID}").FontSize(10);
                                leftColumn.Item().Text("").FontSize(8); // Salto
                                leftColumn.Item().Text($"USt-ID: {factura.BuyerVATID}").FontSize(10);
                                leftColumn.Item().Text($"E-Adresse: {factura.BuyerEmail}").FontSize(10);
                                leftColumn.Item().Text("").FontSize(8); // Salto
                                leftColumn.Item().Text("Kontakt").FontSize(12).Bold();
                                leftColumn.Item().Text($"Name: {factura.BuyerPersonName}").FontSize(10);
                                leftColumn.Item().Text($"E-Mail: {factura.BuyerEmailContact}").FontSize(10);
                                leftColumn.Item().Text($"Tel: {factura.BuyerCompleteNumber}").FontSize(10);
                            });

                            // Columna derecha
                            row.RelativeItem(1).Column(rightColumn =>
                            {
                                // Zahlungsdetails
                                rightColumn.Item().AlignRight().Text("Zahlungsdetails").FontSize(12).Bold();
                                rightColumn.Item().AlignRight().Text("Bank: NOMBREBANCO").FontSize(10);
                                rightColumn.Item().AlignRight().Text($"Kontoinhaber: {factura.AccountName}").FontSize(10);
                                rightColumn.Item().AlignRight().Text($"IBAN: {factura.IBANID}").FontSize(10);
                                rightColumn.Item().AlignRight().Text($"BIC: {factura.BICID}").FontSize(10);
                                rightColumn.Item().Text("").FontSize(8); // Salto de línea
                                // Kontakt
                                rightColumn.Item().AlignRight().Text("Kontakt").FontSize(12).Bold();
                                rightColumn.Item().AlignRight().Text($"Name: {factura.SellerPersonName}").FontSize(10);
                                rightColumn.Item().AlignRight().Text($"E-Mail: {factura.SellerEmail}").FontSize(10);
                                rightColumn.Item().AlignRight().Text($"Tel: {factura.SellerCompleteNumber}").FontSize(10);
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
                            });

                            // Filas de la tabla
                            for (int i = 0; i < factura.Productos.Count; i++)
                            {
                                var producto = factura.Productos[i];

                                var borderColor = (i == factura.Productos.Count - 1) ? Colors.Black : Colors.Grey.Lighten2;
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(5).AlignCenter().Text(producto.Id).FontSize(10);
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
                            {
                                rightColumn.Item().Row(subRow =>
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
