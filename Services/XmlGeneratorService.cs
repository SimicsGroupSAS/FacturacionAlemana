using System;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using FacturacionAlemana.Models;

namespace FacturacionAlemana.Services
{
    public static class XmlGeneratorService
    {        /// <summary>
        /// Convierte un valor decimal a string con formato de punto (.), no coma (,)
        /// Requerido por EN 16931 que especifica punto como separador decimal
        /// </summary>
        private static string NormalizeDecimal(decimal value)
        {
            // Convertir a string con 2 decimales usando InvariantCulture (punto decimal)
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Sobrecarga que acepta string para compatibilidad
        /// </summary>
        private static string NormalizeDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "0.00";
            
            // Reemplazar comas por puntos para asegurar formato universal
            return value.Replace(",", ".");
        }

        public static void GenerarFacturaXml(Factura factura, string outputPath)
        {
            // IMPORTANTE: El SellerVATID debe ser un número de IVA válido según el país
            // Ejemplos válidos de prueba:
            // - Alemania (DE): DE123456789
            // - España (ES): ESA12345678
            // - Francia (FR): FRXX999999999
            // Un VAT-ID inválido causará advertencias de validación en PortInvoice (Warnung [VD-Valitool-160])
            
            // IMPORTANTE: El IBANID debe tener exactamente 22 caracteres sin espacios
            // Formato: Código país (2 chars) + Dígitos de control (2) + Código banco + Número cuenta
            // Ejemplo válido de prueba: DE89400900505012345678
            // IBANs con espacios o longitud incorrecta causarán errores (Error [VD-Valitool-22])
            
            // Definir namespaces según EN 16931 / XRechnung 3.0
            var rsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
            var ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
            var udt = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";
            var xs = "http://www.w3.org/2001/XMLSchema-instance";
            var ccts = "urn:un:unece:uncefact:documentation:standard:CoreComponentsTechnicalSpecification:2";

            var xmlDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(
                    XName.Get("CrossIndustryInvoice", rsm),
                    new XAttribute(XNamespace.Xmlns + "a", "urn:un:unece:uncefact:data:standard:QualifiedDataType:100"),
                    new XAttribute(XNamespace.Xmlns + "qdt", "urn:un:unece:uncefact:data:standard:QualifiedDataType:10"),
                    new XAttribute(XNamespace.Xmlns + "ram", ram),
                    new XAttribute(XNamespace.Xmlns + "rsm", rsm),
                    new XAttribute(XNamespace.Xmlns + "xs", xs),
                    new XAttribute(XNamespace.Xmlns + "udt", udt),
                    new XAttribute(XNamespace.Xmlns + "ccts", ccts),
                    
                    // ExchangedDocumentContext - REQUERIDO por EN 16931
                    new XElement(XName.Get("ExchangedDocumentContext", rsm),
                        new XElement(XName.Get("BusinessProcessSpecifiedDocumentContextParameter", ram),
                            new XElement(XName.Get("ID", ram), "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0")
                        ),
                        new XElement(XName.Get("GuidelineSpecifiedDocumentContextParameter", ram),
                            new XElement(XName.Get("ID", ram), "urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0")
                        )
                    ),
                    
                    // ExchangedDocument
                    new XElement(XName.Get("ExchangedDocument", rsm),
                        new XElement(XName.Get("ID", ram), factura.IdElement),
                        new XElement(XName.Get("TypeCode", ram), factura.TypeCodeElement),
                        new XElement(XName.Get("IssueDateTime", ram),
                            new XElement(XName.Get("DateTimeString", udt), 
                                new XAttribute("format", "102"), 
                                factura.IssueDateElement
                            )
                        ),
                        new XElement(XName.Get("IncludedNote", ram),
                            new XElement(XName.Get("Content", ram), factura.PaymentNoteElement)
                        )                    ),
                      // SupplyChainTradeTransaction - Debe usar namespace rsm: (Root Schema Module)
                    new XElement(XName.Get("SupplyChainTradeTransaction", rsm),
                        // Líneas de productos
                        factura.Productos.Select((prod, index) =>
                            new XElement(XName.Get("IncludedSupplyChainTradeLineItem", ram),
                                new XElement(XName.Get("AssociatedDocumentLineDocument", ram),
                                    new XElement(XName.Get("LineID", ram), (index + 1).ToString())
                                ),
                                new XElement(XName.Get("SpecifiedTradeProduct", ram),
                                    new XElement(XName.Get("SellerAssignedID", ram), prod.SellerAssignedID ?? ""),
                                    new XElement(XName.Get("BuyerAssignedID", ram), prod.BuyerAssignedID ?? ""),
                                    new XElement(XName.Get("Name", ram), prod.Name),
                                    new XElement(XName.Get("Description", ram), prod.Descripcion)
                                ),
                                new XElement(XName.Get("SpecifiedLineTradeAgreement", ram),
                                    new XElement(XName.Get("BuyerOrderReferencedDocument", ram),
                                        new XElement(XName.Get("LineID", ram), prod.BuyerOrderLineID ?? "")
                                    ),
                                    new XElement(XName.Get("NetPriceProductTradePrice", ram),
                                        new XElement(XName.Get("ChargeAmount", ram), NormalizeDecimal(prod.PrecioUnitario)),
                                        new XElement(XName.Get("BasisQuantity", ram), 
                                            new XAttribute("unitCode", prod.Unit ?? "H87"), 1)
                                    )
                                ),
                                new XElement(XName.Get("SpecifiedLineTradeDelivery", ram),
                                    new XElement(XName.Get("BilledQuantity", ram), 
                                        new XAttribute("unitCode", prod.Unit ?? "H87"), prod.Cantidad)
                                ),
                                new XElement(XName.Get("SpecifiedLineTradeSettlement", ram),
                                    new XElement(XName.Get("ApplicableTradeTax", ram),
                                        new XElement(XName.Get("TypeCode", ram), factura.TaxTypeCode),
                                        new XElement(XName.Get("CategoryCode", ram), factura.TaxCategoryCode),
                                        new XElement(XName.Get("RateApplicablePercent", ram), factura.TaxRatePercent)
                                    ),
                                    prod.BillingStartDate.HasValue && prod.BillingEndDate.HasValue ?
                                    new XElement(XName.Get("BillingSpecifiedPeriod", ram),
                                        new XElement(XName.Get("StartDateTime", ram),
                                            new XElement(XName.Get("DateTimeString", udt), prod.BillingStartDate.Value.ToString("yyyyMMdd"), new XAttribute("format", "102"))
                                        ),
                                        new XElement(XName.Get("EndDateTime", ram),
                                            new XElement(XName.Get("DateTimeString", udt), prod.BillingEndDate.Value.ToString("yyyyMMdd"), new XAttribute("format", "102"))
                                        )
                                    ) : null,
                                    new XElement(XName.Get("SpecifiedTradeSettlementLineMonetarySummation", ram),
                                        new XElement(XName.Get("LineTotalAmount", ram), NormalizeDecimal(prod.PrecioTotal))
                                    )
                                )
                            )
                        ).ToArray(),                        // Información de Aplicación del Acuerdo Comercial
                        new XElement(XName.Get("ApplicableHeaderTradeAgreement", ram),
                            new XElement(XName.Get("BuyerReference", ram), 
                                string.IsNullOrWhiteSpace(factura.BuyerReference) 
                                    ? $"REF-{factura.IdElement}" // Generar referencia automática si está vacía
                                    : factura.BuyerReference
                            ),
                            new XElement(XName.Get("SellerOrderReferencedDocument", ram),
                                new XElement(XName.Get("IssuerAssignedID", ram), factura.SalesOrderNumber)
                            ),
                            new XElement(XName.Get("BuyerOrderReferencedDocument", ram),
                                new XElement(XName.Get("IssuerAssignedID", ram), factura.PurchaseOrderNumber)
                            ),
                            new XElement(XName.Get("ContractReferencedDocument", ram),
                                new XElement(XName.Get("IssuerAssignedID", ram), factura.ContractNumber)
                            ),
                            new XElement(XName.Get("SpecifiedProcuringProject", ram),
                                new XElement(XName.Get("ID", ram), factura.ProjectNumber),
                                new XElement(XName.Get("Name", ram), factura.ProjectNumber)
                            ),
                            new XElement(XName.Get("SellerTradeParty", ram),
                                new XElement(XName.Get("Name", ram), factura.SellerName),
                                new XElement(XName.Get("DefinedTradeContact", ram),
                                    new XElement(XName.Get("PersonName", ram), factura.SellerPersonName),
                                    new XElement(XName.Get("DepartmentName", ram), factura.SellerDepartmentName),
                                    new XElement(XName.Get("TelephoneUniversalCommunication", ram),
                                        new XElement(XName.Get("CompleteNumber", ram), factura.SellerCompleteNumber)
                                    ),
                                    new XElement(XName.Get("EmailURIUniversalCommunication", ram),
                                        new XElement(XName.Get("URIID", ram), factura.SellerEmail)
                                    )
                                ),
                                new XElement(XName.Get("PostalTradeAddress", ram),
                                    new XElement(XName.Get("PostcodeCode", ram), factura.SellerPostcodeCode),
                                    new XElement(XName.Get("LineOne", ram), factura.SellerLineOne),
                                    new XElement(XName.Get("LineTwo", ram), factura.SellerLineTwo),
                                    new XElement(XName.Get("CityName", ram), factura.SellerCityName),
                                    new XElement(XName.Get("CountryID", ram), factura.SellerCountryID)
                                ),
                                new XElement(XName.Get("URIUniversalCommunication", ram),
                                    new XElement(XName.Get("URIID", ram), 
                                        new XAttribute("schemeID", "EM"), 
                                        factura.SellerEmail
                                    )
                                ),
                                new XElement(XName.Get("SpecifiedTaxRegistration", ram),
                                    new XElement(XName.Get("ID", ram), 
                                        new XAttribute("schemeID", "VA"), 
                                        factura.SellerVATID
                                    )
                                )
                            ),
                            new XElement(XName.Get("BuyerTradeParty", ram),
                                new XElement(XName.Get("Name", ram), factura.BuyerName),
                                new XElement(XName.Get("DefinedTradeContact", ram),
                                    new XElement(XName.Get("PersonName", ram), factura.BuyerPersonName),
                                    new XElement(XName.Get("TelephoneUniversalCommunication", ram),
                                        new XElement(XName.Get("CompleteNumber", ram), factura.BuyerCompleteNumber)
                                    ),
                                    new XElement(XName.Get("EmailURIUniversalCommunication", ram),
                                        new XElement(XName.Get("URIID", ram), factura.BuyerEmail)
                                    )
                                ),
                                new XElement(XName.Get("PostalTradeAddress", ram),
                                    new XElement(XName.Get("PostcodeCode", ram), factura.BuyerPostcodeCode),
                                    new XElement(XName.Get("LineOne", ram), factura.BuyerLineOne),
                                    new XElement(XName.Get("LineTwo", ram), factura.BuyerLineTwo),
                                    new XElement(XName.Get("CityName", ram), factura.BuyerCityName),
                                    new XElement(XName.Get("CountryID", ram), factura.BuyerCountryID)
                                ),
                                new XElement(XName.Get("URIUniversalCommunication", ram),
                                    new XElement(XName.Get("URIID", ram), 
                                        new XAttribute("schemeID", "EM"), 
                                        factura.BuyerEmail
                                    )
                                ),
                                new XElement(XName.Get("SpecifiedTaxRegistration", ram),
                                    new XElement(XName.Get("ID", ram), 
                                        new XAttribute("schemeID", "VA"), 
                                        factura.BuyerVATID
                                    )
                                )
                            )
                        ),
                          // Información de Entrega
                        new XElement(XName.Get("ApplicableHeaderTradeDelivery", ram),
                            new XElement(XName.Get("ActualDeliverySupplyChainEvent", ram),
                                new XElement(XName.Get("OccurrenceDateTime", ram),
                                    new XElement(XName.Get("DateTimeString", udt), 
                                        new XAttribute("format", "102"), 
                                        factura.DeliveryDate.ToString("yyyyMMdd")
                                    )
                                )
                            )
                        ),                          // Información de Liquidación (Settlement)
                        new XElement(XName.Get("ApplicableHeaderTradeSettlement", ram),
                            new XElement(XName.Get("PaymentReference", ram), factura.PaymentReference),
                            new XElement(XName.Get("InvoiceCurrencyCode", ram), factura.CurrencyID),
                            new XElement(XName.Get("SpecifiedTradeSettlementPaymentMeans", ram),
                                new XElement(XName.Get("TypeCode", ram), factura.PaymentTypeCode),
                                new XElement(XName.Get("Information", ram), factura.PaymentInformation),
                                new XElement(XName.Get("PayeePartyCreditorFinancialAccount", ram),
                                    new XElement(XName.Get("IBANID", ram), factura.IBANID),
                                    new XElement(XName.Get("AccountName", ram), factura.AccountName)
                                ),
                                new XElement(XName.Get("PayeeSpecifiedCreditorFinancialInstitution", ram),
                                    new XElement(XName.Get("BICID", ram), factura.BICID)
                                )
                            ),                            new XElement(XName.Get("ApplicableTradeTax", ram),
                                new XElement(XName.Get("CalculatedAmount", ram), NormalizeDecimal(factura.CalculatedAmount)),
                                new XElement(XName.Get("TypeCode", ram), factura.TaxTypeCode),
                                new XElement(XName.Get("BasisAmount", ram), NormalizeDecimal(factura.BasisAmount)),
                                new XElement(XName.Get("CategoryCode", ram), factura.TaxCategoryCode),
                                new XElement(XName.Get("RateApplicablePercent", ram), factura.TaxRatePercent)
                            ),
                            new XElement(XName.Get("SpecifiedTradePaymentTerms", ram),
                                new XElement(XName.Get("Description", ram), factura.PaymentDescription)
                            ),
                            new XElement(XName.Get("SpecifiedTradeSettlementHeaderMonetarySummation", ram),
                                new XElement(XName.Get("LineTotalAmount", ram), NormalizeDecimal(factura.BasisAmount)),
                                new XElement(XName.Get("ChargeTotalAmount", ram), NormalizeDecimal(0m)),
                                new XElement(XName.Get("AllowanceTotalAmount", ram), NormalizeDecimal(0m)),
                                new XElement(XName.Get("TaxBasisTotalAmount", ram), NormalizeDecimal(factura.BasisAmount)),
                                new XElement(XName.Get("TaxTotalAmount", ram), 
                                    new XAttribute("currencyID", factura.CurrencyID), 
                                    NormalizeDecimal(factura.CalculatedAmount)
                                ),
                                new XElement(XName.Get("GrandTotalAmount", ram), NormalizeDecimal(factura.GrandTotalAmount)),
                                new XElement(XName.Get("TotalPrepaidAmount", ram), NormalizeDecimal(0m)),
                                new XElement(XName.Get("DuePayableAmount", ram), NormalizeDecimal(factura.DuePayableAmount))
                            )
                        )
                    )
                )
            );            // Guardar XML con UTF-8 sin BOM (requerido por EN 16931/XRechnung)
            var settings = new XmlWriterSettings
            {
                Encoding = new System.Text.UTF8Encoding(false), // UTF-8 sin BOM
                Indent = true,
                IndentChars = "  "
            };
            
            using (var writer = System.Xml.XmlWriter.Create(outputPath, settings))
            {
                xmlDoc.WriteTo(writer);
            }
        }
    }
}
