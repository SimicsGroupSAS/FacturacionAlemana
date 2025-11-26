using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using FacturacionAlemana.Models;
using System.IO;
using System.Globalization;

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

        private static bool IsValidDate(DateTime dt) => dt != default && dt.Year > 1900;

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
                    page.Margin(0.5f, Unit.Centimetre);

                    page.Content().Column(column =>
                    {
                        // Cabecera: logo a la izquierda + título centrado (con espacio simétrico a la derecha para centrar)
                        column.Item().Row(row =>
                        {
                            // Intentar cargar logo desde la carpeta Assets del ejecutable
                            try
                            {
                                var exeDir = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
                                var logoPath = Path.Combine(exeDir, "Assets", "Logo-v1_2024-SIMICS-TRADING-GmbH.png");
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
                            }                            // Título en mayúsculas centrado en el espacio restante
                            row.RelativeItem().AlignCenter().Column(titleCol =>
                            {
                                titleCol.Item().Text("RECHNUNG").FontSize(24).FontFamily("Century Gothic").Bold();
                            });

                            // Espacio derecho simétrico al logo para centrar el título en la página
                            row.ConstantItem(140);
                        });

                        // Línea horizontal
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().BorderBottom(1).BorderColor(Colors.Black).Height(1);
                        });
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().BorderBottom(1).BorderColor(Colors.Black).Height(1);
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
                            });                            
                            // Columna derecha - Zahlungsdetails + Kontakt
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
                                        rightColumn.Item().AlignRight().Text($"BIC: {factura.BICID}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(factura.BLZ))
                                        rightColumn.Item().AlignRight().Text($"BLZ: {factura.BLZ}").FontSize(9);
                                    rightColumn.Item().Text("").FontSize(8); // Salto
                                }

                                // Kontakt
                                if (HasAny(factura.SellerPersonName, factura.SellerEmail, factura.SellerCompleteNumber))
                                {
                                    rightColumn.Item().AlignRight().Text("Kontakt").FontSize(11).Bold();
                                    if (!string.IsNullOrWhiteSpace(factura.SellerPersonName))
                                        rightColumn.Item().AlignRight().Text($"Name: {factura.SellerPersonName}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(factura.SellerEmail))
                                        rightColumn.Item().AlignRight().Text($"E-Mail: {factura.SellerEmail}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(factura.SellerCompleteNumber))
                                        rightColumn.Item().AlignRight().Text($"Tel: {factura.SellerCompleteNumber}").FontSize(9);
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
                        {
                            // Columna izquierda - Empfänger
                            row.RelativeItem(1).Column(leftColumn =>
                            {
                                leftColumn.Item().Text("Empfänger").FontSize(11).Bold();
                                if (!string.IsNullOrWhiteSpace(factura.BuyerName))
                                    leftColumn.Item().Text(factura.BuyerName).FontSize(9);

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

                                if (HasAny(factura.BuyerPersonName, factura.BuyerEmailContact, factura.BuyerCompleteNumber))
                                {
                                    leftColumn.Item().Text("").FontSize(8); // Salto
                                    leftColumn.Item().Text("Kontakt").FontSize(11).Bold();
                                    if (!string.IsNullOrWhiteSpace(factura.BuyerPersonName))
                                        leftColumn.Item().Text($"Name: {factura.BuyerPersonName}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(factura.BuyerEmailContact))
                                        leftColumn.Item().Text($"E-Mail: {factura.BuyerEmailContact}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(factura.BuyerCompleteNumber))
                                        leftColumn.Item().Text($"Tel: {factura.BuyerCompleteNumber}").FontSize(9);
                                }
                            });

                            // Columna derecha - Información de Factura
                            row.RelativeItem(1).Column(rightColumn =>
                            {
                                if (!string.IsNullOrWhiteSpace(factura.InvoiceNumber))
                                {
                                    rightColumn.Item().Row(subRow =>
                                    {
                                        subRow.RelativeItem(1).Text("Rechnungs-Nr.").FontSize(9).Bold();
                                        subRow.RelativeItem(1).AlignRight().Text(factura.InvoiceNumber).FontSize(9);
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
                        // Sección de descripción de pago - Encima de la tabla de productos
                        column.Item().PaddingVertical(15).Column(infoColumn =>
                        {
                            var fechaVencimiento = ConvertirFechaAleman(factura.DueDateValue);
                            var textoDescripcion = $"Wir bitten Sie, den Rechnungsbetrag innerhalb von 30 Tagen ab dem oben genannten Datum auf das angegebene Konto\nzu überweisen und dabei unsere Rechnungsnummer anzugeben. Zahlbar bis: {fechaVencimiento}";
                            infoColumn.Item().Text(textoDescripcion).FontSize(9);
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
                                });
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(3).AlignCenter().Text($"{producto.PrecioUnitario:F2} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(9);
                                table.Cell().BorderBottom(1).BorderColor(borderColor).PaddingVertical(3).AlignCenter().Text($"{producto.PrecioTotal:F2} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(9);
                            }
                        });

                        // Totales
                        column.Item().Row(row =>
                        {
                            // Columna izquierda vacía
                            row.RelativeItem(1).Column(leftColumn => { });

                            // Columna derecha
                            row.RelativeItem(1).Column(rightColumn =>
                            {
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).Padding(3).AlignLeft().Text("Netto-Wert").FontSize(9).Bold();
                                    subRow.RelativeItem(1).Padding(3).AlignRight().Text($"{factura.BasisAmount} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(9);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).Padding(3).AlignLeft().Text($"Gesamtsteuer {factura.TaxRatePercent}% ({factura.TaxCategoryCode})").FontSize(9).Bold();
                                    subRow.RelativeItem(1).Padding(3).AlignRight().Text($"{factura.TaxAmount} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(9);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Height(1);
                                });
                                rightColumn.Item().Row(subRow =>
                                {
                                    subRow.RelativeItem(1).Padding(3).AlignLeft().Text("Gesamtbetrag").FontSize(9).Bold();
                                    subRow.RelativeItem(1).Padding(3).AlignRight().Text($"{factura.GrandTotalAmount} {ConvertirMonedaASimolo(factura.CurrencyID)}").FontSize(9);
                                });
                            });
                        });

                        // Sección de detalles de entrega y notas
                        column.Item().PaddingTop(15).Row(row =>
                        {
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

                                if (HasAny(factura.PaymentNoteElement, factura.PaymentDescription))
                                {
                                    leftColumn.Item().PaddingTop(8).Text("Hinweise und Bemerkungen").FontSize(9).Bold();
                                    if (!string.IsNullOrWhiteSpace(factura.PaymentNoteElement))
                                        leftColumn.Item().Text(factura.PaymentNoteElement).FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(factura.PaymentDescription))
                                        leftColumn.Item().Text(factura.PaymentDescription).FontSize(9);
                                }
                            });
                        });
                    });
                });
            });
        }
    }
}
