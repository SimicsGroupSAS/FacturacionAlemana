using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using FacturacionAlemana.Models;
using System.IO;
using System.Globalization;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using System.Text.RegularExpressions;
using System.Text;
using IoPath = System.IO.Path;

namespace FacturacionAlemana.Services
{      public static class PdfGeneratorService
    {
        private static string _currentLanguage = "de"; // Idioma por defecto (Alemán)

        /// <summary>
        /// Establece el idioma para la generación del PDF
        /// </summary>
        public static void SetLanguage(string languageCode)
        {
            _currentLanguage = languageCode.ToLower();
        }

        /// <summary>
        /// Obtiene el título de la factura traducido
        /// </summary>
        private static string GetInvoiceTitle()
        {
            return _currentLanguage switch
            {
                "en" => "INVOICE",
                "de" => "RECHNUNG",
                "es" => "FACTURA",
                _ => "RECHNUNG"
            };
        }

        /// <summary>
        /// Obtiene un texto traducido
        /// </summary>
        private static string GetText(string key)
        {
            try
            {
                return LocalizationService.Instance.Get($"PDF.{key}");
            }
            catch
            {
                return key; // Fallback
            }
        }        private static string ConvertirFechaAleman(DateTime fecha)
        {
            var mesesAleman = new Dictionary<int, string>
            {
                { 1, "Januar" },
                { 2, "Februar" },
                { 3, "März" },
                { 4, "April" },
                { 5, "Mai" },
                { 6, "Juni" },
                { 7, "Juli" },
                { 8, "August" },
                { 9, "September" },
                { 10, "Oktober" },
                { 11, "November" },
                { 12, "Dezember" }
            };

            return $"{fecha.Day:D2}. {mesesAleman[fecha.Month]} {fecha.Year}";
        }

        /// <summary>
        /// Convierte una fecha al idioma actual
        /// </summary>
        private static string ConvertirFecha(DateTime fecha)
        {
            if (fecha == default || fecha.Year < 1900)
                return "";

            return _currentLanguage switch
            {
                "de" => ConvertirFechaAleman(fecha),
                "en" => fecha.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture),
                "es" => fecha.ToString("dd 'de' MMMM 'de' yyyy", new CultureInfo("es-ES")),
                _ => fecha.ToString("dd/MM/yyyy")
            };
        }

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

        // Helpers para ocultar campos vacíos en PDF
        private static bool HasAny(params string?[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v)) return true;
            }
            return false;
        }        private static string JoinNonEmpty(string separator, params string?[] parts)
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

        private static bool IsValidDate(DateTime dt) => dt != default && dt.Year > 1900;        public static byte[] GenerarFacturaPdfEnMemoria(Factura factura)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            
            // Primera pasada: generar PDF y contar páginas
            var firstPassDocument = CrearDocumento(factura);
            byte[] firstPassPdf = firstPassDocument.GeneratePdf();
            int totalPages = ObtenerNumeroPaginas(firstPassPdf);
            
            // Segunda pasada: generar PDF final y actualizar números de página
            var finalDocument = CrearDocumento(factura);
            byte[] finalPdf = finalDocument.GeneratePdf();
            
            // Post-procesar para actualizar numeración de páginas
            return ActualizarNumeracionPaginas(finalPdf, totalPages);
        }

        public static void GenerarFacturaPdf(Factura factura, string outputPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            
            // Primera pasada: generar PDF y contar páginas
            var firstPassDocument = CrearDocumento(factura);
            byte[] firstPassPdf = firstPassDocument.GeneratePdf();
            int totalPages = ObtenerNumeroPaginas(firstPassPdf);
            
            // Segunda pasada: generar PDF final y actualizar números de página
            var finalDocument = CrearDocumento(factura);
            byte[] finalPdf = finalDocument.GeneratePdf();
            
            // Post-procesar para actualizar numeración de páginas
            byte[] processedPdf = ActualizarNumeracionPaginas(finalPdf, totalPages);
            File.WriteAllBytes(outputPath, processedPdf);
        }        /// <summary>
        /// Post-procesa el PDF para superponer los números de página dinámicos
        /// Usa iText7 PdfCanvas para escribir texto en cada página sin modificar streams existentes
        /// </summary>
        private static byte[] ActualizarNumeracionPaginas(byte[] pdfBytes, int totalPages)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Iniciando superposición de números de página. Total: {totalPages}");
                
                using (var inputStream = new MemoryStream(pdfBytes))
                using (var outputStream = new MemoryStream())
                {
                    PdfReader reader = new PdfReader(inputStream);
                    PdfWriter writer = new PdfWriter(outputStream);
                    
                    using (PdfDocument pdfDoc = new PdfDocument(reader, writer))
                    {
                        int numPages = pdfDoc.GetNumberOfPages();
                        Console.WriteLine($"[DEBUG] PDF tiene {numPages} páginas");
                        
                        // Iterar sobre cada página para superponer números
                        for (int pageNum = 1; pageNum <= numPages; pageNum++)
                        {
                            try
                            {
                                PdfPage page = pdfDoc.GetPage(pageNum);
                                Rectangle pageSize = page.GetPageSize();
                                
                                // Crear canvas para superponer contenido
                                PdfCanvas canvas = new PdfCanvas(page);
                                  // Posición para el número de página (centrado abajo, en el footer)
                                // Página A4: ancho ~595 puntos, alto ~842 puntos
                                // Márgenes QuestPDF: 0.5cm = ~14.17 puntos
                                float pageWidth = pageSize.GetWidth();
                                float pageHeight = pageSize.GetHeight();
                                float centerX = pageWidth / 2;
                                
                                // Posición Y del footer: DEBAJO del footer completamente pero dentro del área de impresión
                                // El footer comienza a unos ~28.35 puntos desde abajo (0.5cm * 2)
                                // Colocamos el número a ~15 puntos desde abajo (dentro del margen pero visible)
                                float footerY = 15f; // 15 puntos desde el pie de la página (dentro del área segura)
                                
                                // Texto a superponer
                                string pageText = $"Seite {pageNum} von {totalPages}";
                                
                                // Escribir el texto centrado
                                canvas.SaveState();
                                
                                // Usar Helvetica de iText7
                                PdfFont font = PdfFontFactory.CreateFont();
                                float fontSize = 7;
                                canvas.SetFontAndSize(font, fontSize);
                                
                                // Calcular ancho aproximado del texto para centrarlo
                                // A tamaño 7 con Helvetica, aprox 3.5 puntos por carácter
                                float textWidth = pageText.Length * 3.5f;
                                float textX = centerX - (textWidth / 2);
                                
                                // Usar BeginText/EndText para escribir el texto
                                canvas.BeginText();
                                canvas.SetTextMatrix(1, 0, 0, 1, textX, footerY);
                                canvas.ShowText(pageText);
                                canvas.EndText();
                                
                                canvas.RestoreState();
                                
                                Console.WriteLine($"[DEBUG] Página {pageNum}: Superpuesto '{pageText}' en posición ({textX}, {footerY})");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[WARN] Error al procesar página {pageNum}: {ex.Message}");
                            }
                        }
                    }
                    
                    byte[] result = outputStream.ToArray();
                    Console.WriteLine($"[DEBUG] PDF procesado. Tamaño final: {result.Length} bytes");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] en ActualizarNumeracionPaginas: {ex.Message}");
                Console.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
                return pdfBytes;
            }
        }private static int ObtenerNumeroPaginas(byte[] pdfBytes)
        {
            try
            {
                using (var memoryStream = new MemoryStream(pdfBytes))
                {
                    using (PdfReader reader = new PdfReader(memoryStream))
                    {
                        using (PdfDocument pdfDoc = new PdfDocument(reader))
                        {
                            return pdfDoc.GetNumberOfPages();
                        }
                    }
                }
            }
            catch
            {
                // Si hay cualquier error, asumir 1 página
                return 1;
            }
        }        private static IDocument CrearDocumento(Factura factura)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {                    
                    page.Size(PageSizes.A4);
                    page.Margin(0.5f, Unit.Centimetre);

                    page.Content().Column(column =>
                    {
                        // Cabecera: logo a la izquierda + título centrado (con espacio simétrico a la derecha para centrar)
                        column.Item().Row(row =>                        {
                            // Intentar cargar logo desde la carpeta Assets del ejecutable
                            try
                            {
                                var exeDir = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
                                var logoPath = IoPath.Combine(exeDir, "Assets", "Logo-v1_2024-SIMICS-TRADING-GmbH.png");
                                if (File.Exists(logoPath))
                                {
                                    var logoBytes = File.ReadAllBytes(logoPath);
                                    // Anchura constante para que encaje bien; la imagen se escala por altura
                                    row.ConstantItem(140).AlignLeft().Image(logoBytes, ImageScaling.FitHeight);
                                }
                                else
                                {
                                    // Si no hay logo, reservar el mismo espacio para mantener consistencia
                                    row.ConstantItem(140);
                                }
                            }
                            catch
                            {
                                // No bloquear generación si falla la carga del logo; reservar espacio para simetría
                                row.ConstantItem(140);
                            }

                            // Título en mayúsculas centrado en el espacio restante
                            row.RelativeItem().AlignCenter().Column(titleCol =>
                            {
                                titleCol.Item().Text("RECHNUNG").FontSize(24).FontFamily("Century Gothic");
                            });

                            // Espacio derecho simétrico al logo para centrar el título en la página
                            row.ConstantItem(140);
                        });

                        column.Item().PaddingTop(10);

                        // Primera fila: Información del vendedor + Zahlungsdetails
                        column.Item().Row(row =>
                        {                            // Columna izquierda - Información del vendedor
                            row.RelativeItem(1).Column(leftColumn =>
                            {
                                if (!string.IsNullOrWhiteSpace(factura.SellerName))
                                    leftColumn.Item().Text(factura.SellerName).FontSize(11).Bold();

                                if (!string.IsNullOrWhiteSpace(factura.SellerPersonName))
                                    leftColumn.Item().Text(factura.SellerPersonName).FontSize(9);

                                var sellerStreet = JoinNonEmpty(", ", factura.SellerLineOne, factura.SellerLineTwo);
                                if (!string.IsNullOrWhiteSpace(sellerStreet))
                                    leftColumn.Item().Text(sellerStreet).FontSize(9);

                                var sellerCity = JoinNonEmpty(" ", factura.SellerPostcodeCode, factura.SellerCityName);
                                var sellerCountry = JoinNonEmpty(", ", sellerCity, factura.SellerCountryID);
                                if (!string.IsNullOrWhiteSpace(sellerCountry))
                                    leftColumn.Item().Text(sellerCountry).FontSize(9);

                                leftColumn.Item().Text("").FontSize(8); // Salto de línea

                                if (!string.IsNullOrWhiteSpace(factura.SellerVATID))
                                    leftColumn.Item().Text($"USt-ID: {factura.SellerVATID}").FontSize(9);
                                if (!string.IsNullOrWhiteSpace(factura.SellerTaxNumber))
                                    leftColumn.Item().Text($"St.-Nr.: {factura.SellerTaxNumber}").FontSize(9);
                                if (!string.IsNullOrWhiteSpace(factura.SellerEmail))
                                    leftColumn.Item().Text($"E-Adresse: {factura.SellerEmail}").FontSize(9);
                                if (!string.IsNullOrWhiteSpace(factura.SellerCompleteNumber))
                                    leftColumn.Item().Text($"Tel: {factura.SellerCompleteNumber}").FontSize(9);
                            });                            // Columna derecha - Zahlungsdetails + Kontakt
                            row.RelativeItem(1).Column(rightColumn =>
                            {                                // Zahlungsdetails                                if (HasAny(factura.AccountName, factura.IBANID, factura.BICID))
                                {
                                    rightColumn.Item().AlignRight().Text("Zahlungsdetails").FontSize(11).Bold();
                                    if (!string.IsNullOrWhiteSpace(factura.BankName))
                                        rightColumn.Item().AlignRight().Text($"Bank: {factura.BankName}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(factura.AccountName))
                                        rightColumn.Item().AlignRight().Text($"Kontoinhaber: {factura.AccountName}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(factura.IBANID))
                                        rightColumn.Item().AlignRight().Text($"IBAN: {factura.IBANID}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(factura.BICID))
                                        rightColumn.Item().AlignRight().Text($"BIC: {factura.BICID}").FontSize(9);                                    if (!string.IsNullOrWhiteSpace(factura.BLZ))
                                        rightColumn.Item().AlignRight().Text($"BLZ: {factura.BLZ}").FontSize(9);
                                    rightColumn.Item().Text("").FontSize(8); // Salto
                                }
                            });
                        });

                        // Espaciado antes de la línea separadora
                        column.Item().PaddingTop(10);

                        // Línea separadora horizontal completa
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().BorderBottom(1).BorderColor(Colors.Black).Height(1);
                        });                        
                        // Espaciado después de la línea separadora
                        column.Item().PaddingTop(10);

                        // Segunda fila: Empfänger + Kontakt
                        column.Item().Row(row =>
                        {                            // Columna izquierda - Empfänger
                            row.RelativeItem(1).Column(leftColumn =>
                            {
                                leftColumn.Item().Text("Kunde").FontSize(11).Bold();
                                if (!string.IsNullOrWhiteSpace(factura.BuyerName))
                                    leftColumn.Item().Text(factura.BuyerName).FontSize(9);

                                if (!string.IsNullOrWhiteSpace(factura.BuyerPersonName))
                                    leftColumn.Item().Text(factura.BuyerPersonName).FontSize(9);

                                var buyerStreet = JoinNonEmpty(", ", factura.BuyerLineOne, factura.BuyerLineTwo);
                                if (!string.IsNullOrWhiteSpace(buyerStreet))
                                    leftColumn.Item().Text(buyerStreet).FontSize(9);

                                var buyerCity = JoinNonEmpty(" ", factura.BuyerPostcodeCode, factura.BuyerCityName);
                                var buyerCountry = JoinNonEmpty(", ", buyerCity, factura.BuyerCountryID);
                                if (!string.IsNullOrWhiteSpace(buyerCountry))
                                    leftColumn.Item().Text(buyerCountry).FontSize(9);

                                leftColumn.Item().Text("").FontSize(8); // Salto
                                if (!string.IsNullOrWhiteSpace(factura.BuyerVATID))
                                    leftColumn.Item().Text($"USt-ID: {factura.BuyerVATID}").FontSize(9);
                                if (!string.IsNullOrWhiteSpace(factura.BuyerEmail))
                                    leftColumn.Item().Text($"E-Adresse: {factura.BuyerEmail}").FontSize(9);
                                if (!string.IsNullOrWhiteSpace(factura.BuyerEmailContact))
                                    leftColumn.Item().Text($"E-Adresse (Kontakt): {factura.BuyerEmailContact}").FontSize(9);
                                if (!string.IsNullOrWhiteSpace(factura.BuyerCompleteNumber))
                                    leftColumn.Item().Text($"Tel: {factura.BuyerCompleteNumber}").FontSize(9);
                            });

                            // Columna derecha - Información de Factura
                            row.RelativeItem(1).Column(rightColumn =>
                            {
                                if (!string.IsNullOrWhiteSpace(factura.InvoiceNumber))
                                {
                                    rightColumn.Item().Row(subRow =>
                                    {
                                        subRow.RelativeItem(1).Text("Rechnungs-Nr.").FontSize(9).Bold();
                                        subRow.RelativeItem(1).AlignRight().Text(factura.InvoiceNumber).FontSize(11).Bold();
                                    });
                                }
                                if (IsValidDate(factura.IssueDate))
                                {
                                    rightColumn.Item().Row(subRow =>
                                    {
                                        subRow.RelativeItem(1).Text("Rechnungsdatum").FontSize(9).Bold();
                                        subRow.RelativeItem(1).AlignRight().Text($"{factura.IssueDate:dd.MM.yyyy}").FontSize(9);
                                    });
                                }
                                if (IsValidDate(factura.DeliveryDate))
                                {
                                    rightColumn.Item().Row(subRow =>
                                    {
                                        subRow.RelativeItem(1).Text("Lieferdatum").FontSize(9).Bold();
                                        subRow.RelativeItem(1).AlignRight().Text($"{factura.DeliveryDate:dd.MM.yyyy}").FontSize(9);
                                    });
                                }
                                if (IsValidDate(factura.DueDateValue))
                                {
                                    rightColumn.Item().Row(subRow =>
                                    {
                                        subRow.RelativeItem(1).Text("Fälligkeitsdatum").FontSize(9).Bold();
                                        subRow.RelativeItem(1).AlignRight().Text($"{factura.DueDateValue:dd.MM.yyyy}").FontSize(9);
                                    });
                                }
                                if (!string.IsNullOrWhiteSpace(factura.ProjectNumber))
                                {
                                    rightColumn.Item().Row(subRow =>
                                    {
                                        subRow.RelativeItem(1).Text("Projektnummer").FontSize(9).Bold();
                                        subRow.RelativeItem(1).AlignRight().Text(factura.ProjectNumber).FontSize(9);
                                    });
                                }
                                if (!string.IsNullOrWhiteSpace(factura.ContractNumber))
                                {
                                    rightColumn.Item().Row(subRow =>
                                    {
                                        subRow.RelativeItem(1).Text("Vertragsnummer").FontSize(9).Bold();
                                        subRow.RelativeItem(1).AlignRight().Text(factura.ContractNumber).FontSize(9);
                                    });
                                }
                                if (!string.IsNullOrWhiteSpace(factura.PurchaseOrderNumber))
                                {
                                    rightColumn.Item().Row(subRow =>
                                    {
                                        subRow.RelativeItem(1).Text("Bestellnummer").FontSize(9).Bold();
                                        subRow.RelativeItem(1).AlignRight().Text(factura.PurchaseOrderNumber).FontSize(9);
                                    });
                                }
                                if (!string.IsNullOrWhiteSpace(factura.SalesOrderNumber))
                                {
                                    rightColumn.Item().Row(subRow =>
                                    {
                                        subRow.RelativeItem(1).Text("Auftragsnummer").FontSize(9).Bold();
                                        subRow.RelativeItem(1).AlignRight().Text(factura.SalesOrderNumber).FontSize(9);
                                    });
                                }
                                if (!string.IsNullOrWhiteSpace(factura.PaymentReference))
                                {
                                    rightColumn.Item().Row(subRow =>
                                    {
                                        subRow.RelativeItem(1).Text("Verwendungszweck").FontSize(9).Bold();
                                        subRow.RelativeItem(1).AlignRight().Text(factura.PaymentReference).FontSize(9);
                                    });
                                }
                            });                        
                        });                        

                        // Tabla de productos
                        column.Item().PaddingVertical(15).Table(table =>
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
                                header.Cell().BorderBottom(1).BorderColor(Colors.Black).AlignCenter().Text("Pos").FontSize(9).Bold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Black).AlignLeft().Text("Artikel").FontSize(9).Bold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Black).AlignCenter().Text("Anzahl").FontSize(9).Bold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Black).AlignCenter().Text("Einzelpreis").FontSize(9).Bold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Black).AlignCenter().Text("Gesamtpreis").FontSize(9).Bold();
                            });

                            // Filas de la tabla
                            for (int i = 0; i < factura.Productos.Count; i++)
                            {
                                var producto = factura.Productos[i];

                                var borderColor = (i == factura.Productos.Count - 1) ? Colors.Black : Colors.Grey.Lighten2;
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(3).AlignCenter().Text((i + 1).ToString()).FontSize(9);
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(3).AlignLeft().Column(col =>
                                {
                                    col.Item().Text(producto.Name).FontSize(9).Bold();
                                    if (!string.IsNullOrEmpty(producto.Descripcion))
                                    {
                                        col.Item().Text(producto.Descripcion).FontSize(8).FontColor(Colors.Grey.Darken1);
                                    }
                                    if (producto.BillingStartDate.HasValue && producto.BillingEndDate.HasValue)
                                    {
                                        col.Item().Text($"Zeitraum: {producto.BillingStartDate.Value:dd.MM.yyyy} - {producto.BillingEndDate.Value:dd.MM.yyyy}").FontSize(7).FontColor(Colors.Grey.Darken2);
                                    }
                                    if (!string.IsNullOrEmpty(producto.SellerAssignedID))
                                    {
                                        col.Item().Text($"Artikel-Nr. (Verkäufer): {producto.SellerAssignedID}").FontSize(7).FontColor(Colors.Grey.Darken2);
                                    }
                                    if (!string.IsNullOrEmpty(producto.BuyerAssignedID))
                                    {
                                        col.Item().Text($"Artikel-Nr. (Käufer): {producto.BuyerAssignedID}").FontSize(7).FontColor(Colors.Grey.Darken2);
                                    }
                                    if (!string.IsNullOrEmpty(producto.BuyerOrderLineID))
                                    {
                                        col.Item().Text($"Auftragsposition: {producto.BuyerOrderLineID}").FontSize(7).FontColor(Colors.Grey.Darken2);
                                    }
                                });
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(3).AlignCenter().Text(text =>
                                {
                                    text.Span(producto.Cantidad.ToString("G")).FontSize(9);
                                    text.Span(" ");
                                    text.Span(producto.Unit ?? "H87").FontSize(7).FontColor(Colors.Grey.Darken2);
                                });                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(3).AlignCenter().Text($"{producto.PrecioUnitario.ToString("N2", new CultureInfo("de-DE"))} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(9);
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(3).AlignCenter().Text($"{producto.PrecioTotal.ToString("N2", new CultureInfo("de-DE"))} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(9);
                            }
                        });                        // Lieferdetails y Totales
                        column.Item().PaddingTop(15).Row(row =>
                        {
                            // Columna izquierda - Lieferdetails
                            row.RelativeItem(1).Column(leftColumn =>
                            {
                                if (HasAny(factura.ShipToName, factura.ShipToID, factura.ShipToLineOne, factura.ShipToLineTwo, factura.ShipToLineThree, factura.ShipToPostcodeCode, factura.ShipToCityName, factura.ShipToCountryID, factura.ShipToCountrySubDivisionName))
                                {
                                    leftColumn.Item().Text("Lieferdetails").FontSize(9).Bold();
                                    if (!string.IsNullOrWhiteSpace(factura.ShipToName))
                                        leftColumn.Item().Text($"Name: {factura.ShipToName}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(factura.ShipToID))
                                        leftColumn.Item().Text($"Kennung des Ortes: {factura.ShipToID}").FontSize(9);

                                    var shipStreet = JoinNonEmpty(", ", factura.ShipToLineOne, factura.ShipToLineTwo, factura.ShipToLineThree);
                                    var shipCity = JoinNonEmpty(" ", factura.ShipToPostcodeCode, factura.ShipToCityName);
                                    var shipCountry = JoinNonEmpty(", ", factura.ShipToCountryID, factura.ShipToCountrySubDivisionName);
                                    var shipAddress = JoinNonEmpty(", ", shipStreet, shipCity, shipCountry);
                                    if (!string.IsNullOrWhiteSpace(shipAddress))
                                        leftColumn.Item().Text($"Anschrift: {shipAddress}").FontSize(9);
                                }
                            });

                            // Columna derecha - Totales
                            row.RelativeItem(1).Column(rightColumn =>
                            {                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).Padding(3).AlignLeft().Text("Netto-Wert").FontSize(9).Bold();
                                    subRow.RelativeItem(1).Padding(3).AlignRight().Text($"{decimal.Parse(factura.BasisAmount, CultureInfo.InvariantCulture).ToString("N2", new CultureInfo("de-DE"))} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(9);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).Padding(3).AlignLeft().Text($"Gesamtsteuer {factura.TaxRatePercent}% ({factura.TaxCategoryCode})").FontSize(9).Bold();
                                    subRow.RelativeItem(1).Padding(3).AlignRight().Text($"{decimal.Parse(factura.TaxAmount, CultureInfo.InvariantCulture).ToString("N2", new CultureInfo("de-DE"))} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(9);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Height(1);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).Padding(3).AlignLeft().Text("Gesamtbetrag").FontSize(9).Bold();
                                    subRow.RelativeItem(1).Padding(3).AlignRight().Text($"{decimal.Parse(factura.GrandTotalAmount, CultureInfo.InvariantCulture).ToString("N2", new CultureInfo("de-DE"))} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(9);
                                });
                            });
                        });                        // Sección de Hinweise und Bemerkungen
                        column.Item().PaddingTop(15).Column(remarksColumn =>
                        {                            remarksColumn.Item().Text("Hinweise und Bemerkungen").FontSize(9).Bold();
                            if (!string.IsNullOrWhiteSpace(factura.PaymentDescription))
                                remarksColumn.Item().Text(factura.PaymentDescription).FontSize(9);
                            if (!string.IsNullOrWhiteSpace(factura.PaymentNoteElement))
                                remarksColumn.Item().Text(factura.PaymentNoteElement).FontSize(9);
                            var fechaVencimiento = ConvertirFechaAleman(factura.DueDateValue);
                            var textoDescripcion = $"Wir bitten Sie, den Rechnungsbetrag innerhalb von 30 Tagen ab dem genannten Datum auf das angegebene Konto zu überweisen und dabei unsere Rechnungsnummer anzugeben. Zahlbar bis: {fechaVencimiento}";
                            remarksColumn.Item().Text(textoDescripcion).FontSize(9);
                        });// Sección de firma
                        column.Item().PaddingTop(40);
                        column.Item().Row(row =>
                        {
                            // Columna izquierda - Firma
                            row.RelativeItem(1).Column(leftColumn =>
                            {
                                // Espacio para firma
                                leftColumn.Item().Height(40).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);

                                // Nombre del contacto del vendedor
                                if (!string.IsNullOrWhiteSpace(factura.SellerPersonName))
                                {
                                    leftColumn.Item().PaddingTop(4).Text(factura.SellerPersonName).FontSize(11).Bold();
                                }
                            });

                            // Espacio derecho vacío
                            row.RelativeItem(1).Column(rightColumn => { });
                        });                        // Comentario final
                        column.Item().PaddingTop(8).Text("(Dieses Dokument ist automatisch erstellt worden und ohne Unterschrift gültig)").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });                    // Footer
                    page.Footer().Column(footerColumn =>
                    {
                        // Línea separadora superior
                        footerColumn.Item().Row(row =>
                        {
                            row.RelativeItem().BorderBottom(1).BorderColor(Colors.Black).Height(1);
                        });

                        // Espaciado
                        footerColumn.Item().PaddingVertical(5);

                        // Primera fila de información
                        footerColumn.Item().AlignCenter().Text("SIMICS TRADING GmbH | HRB 36000 | USt.-Id Nr.: DE400209649 | Ruhrallee 5, 45525 Hattingen, NRW, Deutschland | +49 1520 8572464").FontSize(7);

                        // Segunda fila de información
                        footerColumn.Item().AlignCenter().Text("www.simicstrading.com | contactenos@simicstrading.com").FontSize(7);

                        // Espaciado
                        footerColumn.Item().PaddingVertical(3);                        // Línea separadora inferior
                        footerColumn.Item().Row(row =>
                        {
                            row.RelativeItem().BorderBottom(1).BorderColor(Colors.Black).Height(1);
                        });

                        // Espaciado adicional para el contador de páginas (superpuesto con iText7)
                        footerColumn.Item().PaddingVertical(8);

                        // El número de página se agrega dinámicamente con iText7 después de la generación
                    });
                });
            });
        }
    }
}
