using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using FacturacionAlemana.Models;
using System.IO;

namespace FacturacionAlemana.Services
{
    public static class PdfGeneratorService
    {
        public static void GenerarFacturaPdf(Factura factura, string outputPath)
        {
            QuestPDF.Settings.License = LicenseType.Community; // Configurar licencia comunitaria

            int pageNumber = 1; // Número de página actual
            int totalPages = 1; // Total de páginas del documento

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4); // Cambiar tamaño de la página a carta
                    page.Margin(2, Unit.Centimetre);

                    var plantillaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "plantilla.png");
                    if (File.Exists(plantillaPath))
                    {
                        page.Background().Image(plantillaPath, ImageScaling.FitArea);
                    }

                    page.Content().PaddingVertical(50).Column(column =>
                    {
                        // Título centrado
                        column.Item().AlignCenter().Text("Rechnung").FontSize(30).FontFamily("Century Gothic").Bold();

                        // Línea horizontal con margen hacia abajo
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().BorderBottom(1).BorderColor(Colors.Black).Height(1);
                        });
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().BorderBottom(1).BorderColor(Colors.Black).Height(1);
                        });

                        column.Item().PaddingTop(15); // Agregar espacio debajo de la línea horizontal

                        // Contenido en la columna izquierda y derecha
                        column.Item().Row(row =>
                        {
                            // Columna izquierda: Nombre de la empresa arriba y dirección abajo
                            row.RelativeItem(1).Column(leftColumn =>
                            {
                                leftColumn.Item().Text($"{factura.SellerName}").FontSize(12).Bold();
                                leftColumn.Item().Text($"{factura.SellerLineOne}, {factura.SellerPostcodeCode} {factura.SellerCityName}, {factura.SellerCountryID}").FontSize(10);
                                leftColumn.Item().Text($"Käufername: {factura.BuyerName}").FontSize(10);
                                leftColumn.Item().Text($"Kontaktperson: {factura.BuyerPersonName}").FontSize(10);
                                leftColumn.Item().Text($"Adresse: {factura.BuyerCityName}, {factura.BuyerPostcodeCode}, {factura.BuyerCountryID}").FontSize(10);
                            });

                            // Columna derecha dividida en filas
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

                        // Tabla de productos debajo de las columnas
                        column.Item().PaddingVertical(20).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1); // ID
                                columns.RelativeColumn(3); // Beschreibung
                                columns.RelativeColumn(1); // Menge
                                columns.RelativeColumn(1); // Einzelpreis
                                columns.RelativeColumn(1); // Gesamtpreis
                            });

                            // Encabezados de la tabla
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
                                var cantidad = producto.Cantidad / 10000000; // Ajustar cantidad
                                var precioUnitario = producto.PrecioUnitario / 10000000; // Ajustar precio unitario
                                var precioTotal = cantidad * precioUnitario; // Calcular precio total

                                var borderColor = (i == factura.Productos.Count - 1) ? Colors.Black : Colors.Grey.Lighten2; // Última línea negra

                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(5).AlignCenter().Text(producto.Id).FontSize(10);
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(5).AlignCenter().Text(producto.Descripcion).FontSize(10);
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(5).AlignCenter().Text(cantidad.ToString("F0")).FontSize(10);
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(5).AlignCenter().Text($"{precioUnitario:F2} {factura.CurrencyID}").FontSize(10);
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(5).AlignCenter().Text($"{precioTotal:F2} {factura.CurrencyID}").FontSize(10);
                            }
                        });

                        // Agregar dos columnas debajo de la tabla
                        column.Item().Row(row =>
                        {
                            // Columna izquierda: Condiciones de Pago
                            row.RelativeItem(1).PaddingRight(10).Column(leftColumn =>
                            {
                                leftColumn.Item().Text("Zahlungsbedingungen").FontSize(10).Bold();
                                leftColumn.Item().Text(factura.PaymentDescription).FontSize(10);
                            });

                            // Columna derecha: subdividida en dos columnas
                            row.RelativeItem(1).Column(rightColumn =>
                            {
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).Padding(5).AlignLeft().Text("Netto-Wert").FontSize(10).Bold();
                                    subRow.RelativeItem(1).Padding(5).AlignRight().Text($"{factura.BasisAmount} {factura.CurrencyID}").FontSize(10);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).Padding(5).AlignLeft().Text("Gesamtsteuer").FontSize(10).Bold();
                                    subRow.RelativeItem(1).Padding(5).AlignRight().Text($"{factura.CalculatedAmount} {factura.CurrencyID}").FontSize(10);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Height(1);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).Padding(5).AlignLeft().Text("Gesamtbetrag").FontSize(10).Bold();
                                    subRow.RelativeItem(1).Padding(5).AlignRight().Text($"{factura.GrandTotalAmount} {factura.CurrencyID}").FontSize(10);
                                });
                            });
                        });
                    });
                });
            });

            document.GeneratePdf(outputPath);
        }
    }
}