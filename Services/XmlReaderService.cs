using System.Xml.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using FacturacionAlemana.Models;

namespace FacturacionAlemana.Services
{
    public static class XmlReaderService
    {
        private static readonly Dictionary<string, string> PaisesMapping = new()
        {
            { "Netherlands", "NL" },
            { "Germany", "DE" },
            { "Spain", "ES" },
            { "France", "FR" },
            { "Italy", "IT" },
            { "Austria", "AT" },
            { "Belgium", "BE" },
            { "Switzerland", "CH" },
            { "United Kingdom", "UK" },
            { "Poland", "PL" }
        };

        private static decimal NormalizarDecimal(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return 0;

            valor = valor.Replace(",", ".");
            
            if (decimal.TryParse(valor, CultureInfo.InvariantCulture, out var resultado))
                return resultado;

            return 0;
        }        private static string NormalizarCodigoPais(string? pais)
        {
            if (string.IsNullOrWhiteSpace(pais))
                return "XX";

            pais = pais.Trim();

            if (pais.Length == 2 && Regex.IsMatch(pais, @"^[A-Z]{2}$"))
                return pais;

            if (PaisesMapping.TryGetValue(pais, out var codigo))
                return codigo;

            return pais.Length >= 2 ? pais.Substring(0, 2).ToUpper() : pais.ToUpper();
        }

        /// <summary>
        /// Convierte códigos de moneda ISO a símbolos
        /// </summary>
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

        private static List<string> ExtraerNotas(XDocument xmlDoc, XNamespace rsm, XNamespace ram)
        {
            return xmlDoc.Descendants(rsm + "ExchangedDocument")
                .Elements(ram + "IncludedNote")
                .Elements(ram + "Content")
                .Select(x => x.Value?.Trim() ?? "")
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }

        public static Factura LeerFacturaDesdeXml(string filePath)
        {
            XNamespace rsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
            XNamespace ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
            XNamespace udt = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";

            var xmlDoc = XDocument.Load(filePath);            // Detalles de la Factura
            var idElement = xmlDoc.Descendants(rsm + "ExchangedDocument").Elements(ram + "ID").FirstOrDefault()?.Value ?? "No encontrado";
            var typeCodeElement = xmlDoc.Descendants(rsm + "ExchangedDocument").Elements(ram + "TypeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var issueDateElement = xmlDoc.Descendants(rsm + "ExchangedDocument").Elements(udt + "DateTimeString").FirstOrDefault()?.Value ?? "No encontrado";
            
            var notas = ExtraerNotas(xmlDoc, rsm, ram);
            var paymentNoteElement = notas.Count > 0 ? notas[0] : "No encontrado";

            // Parsear fechas
            var issueDate = DateTime.TryParseExact(issueDateElement, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedIssueDate)
                ? parsedIssueDate
                : DateTime.Now;
            
            // Leer fecha de entrega
            var deliveryDateRaw = xmlDoc.Descendants(ram + "ActualDeliverySupplyChainEvent").Descendants(udt + "DateTimeString").FirstOrDefault()?.Value ?? issueDateElement;
            var deliveryDate = DateTime.TryParseExact(deliveryDateRaw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDeliveryDate)
                ? parsedDeliveryDate
                : issueDate;

            // Detalles del Vendedor
            var sellerName = xmlDoc.Descendants(ram + "SellerTradeParty").Elements(ram + "Name").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerPersonName = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "PersonName").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerDepartmentName = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "DepartmentName").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerCompleteNumber = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "CompleteNumber").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerEmail = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "URIID").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerPostcodeCode = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "PostcodeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerLineOne = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "LineOne").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerLineTwo = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "LineTwo").FirstOrDefault()?.Value ?? "";
            var sellerCityName = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "CityName").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerCountryIDRaw = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "CountryID").FirstOrDefault()?.Value ?? "XX";
            var sellerCountryID = NormalizarCodigoPais(sellerCountryIDRaw);            var sellerVATID = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "SpecifiedTaxRegistration").Where(x => x.Element(ram + "ID")?.Attribute("schemeID")?.Value == "VA").Select(x => x.Element(ram + "ID")?.Value).FirstOrDefault() ?? "No encontrado";
            var sellerTaxNumber = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "SpecifiedTaxRegistration").Where(x => x.Element(ram + "ID")?.Attribute("schemeID")?.Value == "FC").Select(x => x.Element(ram + "ID")?.Value).FirstOrDefault() ?? "";// Detalles del Comprador
            var buyerID = xmlDoc.Descendants(ram + "BuyerTradeParty").Elements(ram + "ID").FirstOrDefault()?.Value ?? "";
            var buyerName = xmlDoc.Descendants(ram + "BuyerTradeParty").Elements(ram + "Name").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerPersonName = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "PersonName").FirstOrDefault()?.Value ?? "";
            var buyerCompleteNumber = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "CompleteNumber").FirstOrDefault()?.Value ?? "";
            // Email del comprador - buscar en DefinedTradeContact primero, luego en URIUniversalCommunication
            var buyerEmailContact = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "DefinedTradeContact").Descendants(ram + "EmailURIUniversalCommunication").Elements(ram + "URIID").FirstOrDefault()?.Value ?? "";
            var buyerEmailUri = xmlDoc.Descendants(ram + "BuyerTradeParty").Elements(ram + "URIUniversalCommunication").Descendants(ram + "URIID").FirstOrDefault()?.Value ?? "";
            var buyerEmail = !string.IsNullOrWhiteSpace(buyerEmailContact) ? buyerEmailContact : buyerEmailUri;            // Código postal del comprador - buscar en PostalTradeAddress
            var buyerPostalAddress = xmlDoc.Descendants(ram + "BuyerTradeParty").Elements(ram + "PostalTradeAddress").FirstOrDefault();
            var buyerPostcodeCode = buyerPostalAddress?.Elements(ram + "PostcodeCode").FirstOrDefault()?.Value ?? "";
            var buyerLineOne = buyerPostalAddress?.Elements(ram + "LineOne").FirstOrDefault()?.Value ?? "";
            var buyerLineTwo = buyerPostalAddress?.Elements(ram + "LineTwo").FirstOrDefault()?.Value ?? "";
            var buyerCityName = buyerPostalAddress?.Elements(ram + "CityName").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerCountryIDRaw = buyerPostalAddress?.Elements(ram + "CountryID").FirstOrDefault()?.Value ?? "XX";
            var buyerCountryID = NormalizarCodigoPais(buyerCountryIDRaw);            var buyerVATID = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "SpecifiedTaxRegistration").Where(x => x.Element(ram + "ID")?.Attribute("schemeID")?.Value == "VA").Select(x => x.Element(ram + "ID")?.Value).FirstOrDefault() ?? "";

            // Detalles de entrega (ShipTo)
            var shipToID = xmlDoc.Descendants(ram + "ShipToTradeParty").Elements(ram + "ID").FirstOrDefault()?.Value ?? "";
            var shipToName = xmlDoc.Descendants(ram + "ShipToTradeParty").Elements(ram + "Name").FirstOrDefault()?.Value ?? "";
            var shipToPostcodeCode = xmlDoc.Descendants(ram + "ShipToTradeParty").Descendants(ram + "PostcodeCode").FirstOrDefault()?.Value ?? "";
            var shipToLineOne = xmlDoc.Descendants(ram + "ShipToTradeParty").Descendants(ram + "LineOne").FirstOrDefault()?.Value ?? "";
            var shipToLineTwo = xmlDoc.Descendants(ram + "ShipToTradeParty").Descendants(ram + "LineTwo").FirstOrDefault()?.Value ?? "";
            var shipToLineThree = xmlDoc.Descendants(ram + "ShipToTradeParty").Descendants(ram + "LineThree").FirstOrDefault()?.Value ?? "";
            var shipToCityName = xmlDoc.Descendants(ram + "ShipToTradeParty").Descendants(ram + "CityName").FirstOrDefault()?.Value ?? "";
            var shipToCountryID = xmlDoc.Descendants(ram + "ShipToTradeParty").Descendants(ram + "CountryID").FirstOrDefault()?.Value ?? "";
            var shipToCountrySubDivisionName = xmlDoc.Descendants(ram + "ShipToTradeParty").Descendants(ram + "CountrySubDivisionName").FirstOrDefault()?.Value ?? "";

            // Detalles de los Ítems de Línea
            var productos = new List<Producto>();
            string taxTypeCode = "VAT";
            string taxCategoryCode = "S";
            string taxRatePercent = "19";
            foreach (var item in xmlDoc.Descendants(rsm + "SupplyChainTradeTransaction").Elements(ram + "IncludedSupplyChainTradeLineItem"))
            {
                var itemLineID = item.Descendants(ram + "AssociatedDocumentLineDocument").Elements(ram + "LineID").FirstOrDefault()?.Value ?? "1";
                var itemSellerAssignedID = item.Descendants(ram + "SpecifiedTradeProduct").Elements(ram + "SellerAssignedID").FirstOrDefault()?.Value ?? "";
                var buyerAssignedID = item.Descendants(ram + "SpecifiedTradeProduct").Elements(ram + "BuyerAssignedID").FirstOrDefault()?.Value ?? "";
                var name = item.Descendants(ram + "SpecifiedTradeProduct").Elements(ram + "Name").FirstOrDefault()?.Value ?? "";
                var description = item.Descendants(ram + "SpecifiedTradeProduct").Elements(ram + "Description").FirstOrDefault()?.Value ?? "";
                var buyerOrderLineID = item.Descendants(ram + "SpecifiedLineTradeAgreement").Descendants(ram + "BuyerOrderReferencedDocument").Elements(ram + "LineID").FirstOrDefault()?.Value ?? "";
                var billingStartDateStr = item.Descendants(ram + "SpecifiedLineTradeSettlement").Descendants(ram + "BillingSpecifiedPeriod").Elements(ram + "StartDateTime").Elements(udt + "DateTimeString").FirstOrDefault()?.Value ?? "";
                var billingEndDateStr = item.Descendants(ram + "SpecifiedLineTradeSettlement").Descendants(ram + "BillingSpecifiedPeriod").Elements(ram + "EndDateTime").Elements(udt + "DateTimeString").FirstOrDefault()?.Value ?? "";
                var itemChargeAmount = item.Descendants(ram + "SpecifiedLineTradeAgreement").Descendants(ram + "NetPriceProductTradePrice").Elements(ram + "ChargeAmount").FirstOrDefault()?.Value ?? "0";
                var itemBilledQuantity = item.Descendants(ram + "SpecifiedLineTradeDelivery").Elements(ram + "BilledQuantity").FirstOrDefault()?.Value ?? "0";
                var itemUnitCode = item.Descendants(ram + "SpecifiedLineTradeDelivery").Elements(ram + "BilledQuantity").Attributes("unitCode").FirstOrDefault()?.Value ?? "H87";
                var itemLineTotalAmount = item.Descendants(ram + "SpecifiedLineTradeSettlement").Descendants(ram + "SpecifiedTradeSettlementLineMonetarySummation").Elements(ram + "LineTotalAmount").FirstOrDefault()?.Value ?? "0";

                // Leer tax del item
                var itemTaxTypeCode = item.Descendants(ram + "SpecifiedLineTradeSettlement").Descendants(ram + "ApplicableTradeTax").Elements(ram + "TypeCode").FirstOrDefault()?.Value ?? "VAT";
                var itemTaxCategoryCode = item.Descendants(ram + "SpecifiedLineTradeSettlement").Descendants(ram + "ApplicableTradeTax").Elements(ram + "CategoryCode").FirstOrDefault()?.Value ?? "S";
                var itemTaxRatePercent = item.Descendants(ram + "SpecifiedLineTradeSettlement").Descendants(ram + "ApplicableTradeTax").Elements(ram + "RateApplicablePercent").FirstOrDefault()?.Value ?? "19";

                DateTime.TryParseExact(billingStartDateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var billingStartDate);
                DateTime.TryParseExact(billingEndDateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var billingEndDate);

                productos.Add(new Producto
                {
                    Pos = int.Parse(itemLineID),
                    Name = name,
                    Descripcion = description,
                    SellerAssignedID = itemSellerAssignedID,
                    BuyerAssignedID = buyerAssignedID,
                    BuyerOrderLineID = buyerOrderLineID,
                    BillingStartDate = billingStartDate == DateTime.MinValue ? null : billingStartDate,
                    BillingEndDate = billingEndDate == DateTime.MinValue ? null : billingEndDate,
                    Cantidad = NormalizarDecimal(itemBilledQuantity),
                    Unit = itemUnitCode,
                    PrecioUnitario = NormalizarDecimal(itemChargeAmount),
                    PrecioTotal = NormalizarDecimal(itemLineTotalAmount)
                });

                // Usar el tax del primer item
                if (productos.Count == 1)
                {
                    taxTypeCode = itemTaxTypeCode;
                    taxCategoryCode = itemTaxCategoryCode;
                    taxRatePercent = itemTaxRatePercent;
                }
            }

            // Si no hay productos, agregar uno por defecto
            if (!productos.Any())
            {
                productos.Add(new Producto { Pos = 1, Name = "Producto por defecto", Descripcion = "Producto por defecto", Cantidad = 1, Unit = "H87", PrecioUnitario = 0, PrecioTotal = 0 });
            }

            // Usar el primer producto para los campos individuales
            var lineID = productos[0].Pos.ToString();
            var sellerAssignedID = productos[0].Pos.ToString();
            var productName = productos[0].Descripcion;
            var chargeAmount = productos[0].PrecioUnitario.ToString("F2", CultureInfo.InvariantCulture);
            var billedQuantity = productos[0].Cantidad.ToString("F2", CultureInfo.InvariantCulture);
            var lineTotalAmount = productos[0].PrecioTotal.ToString("F2", CultureInfo.InvariantCulture);

            // Resumen de Pago
            var invoiceCurrencyCode = xmlDoc.Descendants(ram + "InvoiceCurrencyCode").FirstOrDefault()?.Value ?? "No encontrado";
            var paymentTypeCode = xmlDoc.Descendants(ram + "SpecifiedTradeSettlementPaymentMeans").Descendants(ram + "TypeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var paymentInformation = xmlDoc.Descendants(ram + "SpecifiedTradeSettlementPaymentMeans").Descendants(ram + "Information").FirstOrDefault()?.Value ?? "No encontrado";
            var ibanID = xmlDoc.Descendants(ram + "PayeePartyCreditorFinancialAccount").Descendants(ram + "IBANID").FirstOrDefault()?.Value ?? "No encontrado";
            var accountName = xmlDoc.Descendants(ram + "PayeePartyCreditorFinancialAccount").Descendants(ram + "AccountName").FirstOrDefault()?.Value ?? "No encontrado";            var bicID = xmlDoc.Descendants(ram + "PayeeSpecifiedCreditorFinancialInstitution").Descendants(ram + "BICID").FirstOrDefault()?.Value ?? "";
            
            // Capturar información bancaria adicional
            var bankName = xmlDoc.Descendants(ram + "PayeeSpecifiedCreditorFinancialInstitution").Elements(ram + "Name").FirstOrDefault()?.Value ?? "";
            var blz = xmlDoc.Descendants(ram + "PayeeSpecifiedCreditorFinancialInstitution").Elements(ram + "GermanBankleitzahlIdentifier").FirstOrDefault()?.Value ?? "";
            
            var calculatedAmountRaw = xmlDoc.Descendants(ram + "ApplicableTradeTax").Descendants(ram + "CalculatedAmount").FirstOrDefault()?.Value ?? "0";
            var calculatedAmount = NormalizarDecimal(calculatedAmountRaw).ToString("F2");
            
            var basisAmountRaw = xmlDoc.Descendants(ram + "ApplicableTradeTax").Descendants(ram + "BasisAmount").FirstOrDefault()?.Value ?? "0";
            var basisAmount = NormalizarDecimal(basisAmountRaw).ToString("F2");            var paymentDescription = xmlDoc.Descendants(ram + "SpecifiedTradePaymentTerms").Descendants(ram + "Description").FirstOrDefault()?.Value ?? "";
            
            // GeneralNote: Primera nota capturada del documento
            var generalNote = notas.Count > 0 ? notas[0] : "";
            
            // Extraer fecha de vencimiento - primero intenta desde SpecifiedTradePaymentTerms, si no usa IssueDateTime
            var dueDateRaw = xmlDoc.Descendants(ram + "SpecifiedTradePaymentTerms").Descendants(udt + "DateTimeString").FirstOrDefault()?.Value;
            
            // Si no encuentra fecha de vencimiento, usa la fecha de emisión
            if (string.IsNullOrWhiteSpace(dueDateRaw))
            {
                dueDateRaw = issueDateElement;
            }

            var dueDate = DateTime.TryParseExact(dueDateRaw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
                ? parsedDate.ToString("dd-MM-yyyy")
                : dueDateRaw;

            var grandTotalAmountRaw = xmlDoc.Descendants(ram + "SpecifiedTradeSettlementHeaderMonetarySummation").Descendants(ram + "GrandTotalAmount").FirstOrDefault()?.Value ?? "0";
            var grandTotalAmount = NormalizarDecimal(grandTotalAmountRaw).ToString("F2");
            
            var duePayableAmountRaw = xmlDoc.Descendants(ram + "SpecifiedTradeSettlementHeaderMonetarySummation").Descendants(ram + "DuePayableAmount").FirstOrDefault()?.Value ?? "0";
            var duePayableAmount = NormalizarDecimal(duePayableAmountRaw).ToString("F2");            var dateTimeFormat = xmlDoc.Descendants(udt + "DateTimeString").Attributes("format").FirstOrDefault()?.Value ?? "No encontrado";
            var unitCode = xmlDoc.Descendants(ram + "BilledQuantity").Attributes("unitCode").FirstOrDefault()?.Value ?? "No encontrado";
            var schemeID = xmlDoc.Descendants(ram + "SpecifiedTaxRegistration").Descendants(ram + "ID").Attributes("schemeID").FirstOrDefault()?.Value ?? "No encontrado";
            var currencyID = xmlDoc.Descendants(ram + "TaxTotalAmount").Attributes("currencyID").FirstOrDefault()?.Value ?? "No encontrado";

            var taxTotalAmountRaw = xmlDoc.Descendants(ram + "SpecifiedTradeSettlementHeaderMonetarySummation").Descendants(ram + "TaxTotalAmount").FirstOrDefault()?.Value ?? "0";
            var taxTotalAmount = NormalizarDecimal(taxTotalAmountRaw).ToString("F2");

            // Extraer información de referencias de documentos
            var projectNumber = xmlDoc.Descendants(ram + "SpecifiedProcuringProject").Elements(ram + "ID").FirstOrDefault()?.Value ?? "";
            var contractNumber = xmlDoc.Descendants(ram + "ContractReferencedDocument").Elements(ram + "IssuerAssignedID").FirstOrDefault()?.Value ?? "";
            var purchaseOrderNumber = xmlDoc.Descendants(ram + "BuyerOrderReferencedDocument").Elements(ram + "IssuerAssignedID").FirstOrDefault()?.Value ?? "";
            var salesOrderNumber = xmlDoc.Descendants(ram + "SellerOrderReferencedDocument").Elements(ram + "IssuerAssignedID").FirstOrDefault()?.Value ?? "";
            var paymentReference = xmlDoc.Descendants(ram + "PaymentReference").FirstOrDefault()?.Value ?? "";
            
            // Parsear fecha de vencimiento como DateTime
            var dueDateRawForValue = xmlDoc.Descendants(ram + "SpecifiedTradePaymentTerms").Descendants(udt + "DateTimeString").FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(dueDateRawForValue))
            {
                dueDateRawForValue = issueDateElement;
            }
            var dueDateValue = DateTime.TryParseExact(dueDateRawForValue, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDueDate)
                ? parsedDueDate
                : issueDate;

            return new Factura
            {
                IdElement = idElement,
                Cliente = buyerName,
                Total = NormalizarDecimal(grandTotalAmount),
                DueDate = dueDate,
                GrandTotalAmount = grandTotalAmount,
                DuePayableAmount = duePayableAmount,
                DateTimeFormat = dateTimeFormat,
                UnitCode = unitCode,
                SchemeID = schemeID,
                CurrencyID = currencyID,
                TypeCodeElement = typeCodeElement,
                IssueDateElement = issueDateElement,
                PaymentNoteElement = paymentNoteElement,
                SellerName = sellerName,
                SellerPersonName = sellerPersonName,
                SellerDepartmentName = sellerDepartmentName,
                SellerCompleteNumber = sellerCompleteNumber,
                SellerEmail = sellerEmail,
                SellerPostcodeCode = sellerPostcodeCode,
                SellerLineOne = sellerLineOne,
                SellerLineTwo = sellerLineTwo,
                SellerCityName = sellerCityName,
                SellerCountryID = sellerCountryID,
                SellerVATID = sellerVATID,
                SellerTaxNumber = sellerTaxNumber,                BuyerID = buyerID,
                BuyerName = buyerName,
                BuyerPersonName = buyerPersonName,
                BuyerCompleteNumber = buyerCompleteNumber,
                BuyerEmail = !string.IsNullOrWhiteSpace(buyerEmailContact) ? buyerEmailContact : buyerEmail,
                BuyerPostcodeCode = buyerPostcodeCode,
                BuyerLineOne = buyerLineOne,
                BuyerLineTwo = buyerLineTwo,
                BuyerCityName = buyerCityName,
                BuyerCountryID = buyerCountryID,
                BuyerVATID = buyerVATID,
                BuyerEmailContact = buyerEmailContact,
                Productos = productos,
                LineID = lineID,
                SellerAssignedID = sellerAssignedID,
                ProductName = productName,
                ChargeAmount = chargeAmount,
                BilledQuantity = billedQuantity,
                TaxTypeCode = taxTypeCode,                TaxCategoryCode = taxCategoryCode,
                TaxRatePercent = taxRatePercent,
                LineTotalAmount = lineTotalAmount,
                InvoiceCurrencyCode = invoiceCurrencyCode,
                PaymentTypeCode = paymentTypeCode,
                PaymentInformation = paymentInformation,                IBANID = ibanID,
                AccountName = accountName,
                BICID = bicID,
                BankName = bankName,
                BLZ = blz,
                CalculatedAmount = calculatedAmount,
                BasisAmount = basisAmount,
                TaxAmount = taxTotalAmount,
                PaymentDescription = paymentDescription,
                InvoiceNumber = idElement,
                IssueDate = issueDate,
                DeliveryDate = deliveryDate,
                DueDateValue = dueDateValue,
                ProjectNumber = projectNumber,
                ContractNumber = contractNumber,
                PurchaseOrderNumber = purchaseOrderNumber,
                SalesOrderNumber = salesOrderNumber,
                PaymentReference = paymentReference,
                ShipToID = shipToID,
                ShipToName = shipToName,
                ShipToPostcodeCode = shipToPostcodeCode,
                ShipToLineOne = shipToLineOne,
                ShipToLineTwo = shipToLineTwo,
                ShipToLineThree = shipToLineThree,
                ShipToCityName = shipToCityName,                ShipToCountryID = shipToCountryID,
                ShipToCountrySubDivisionName = shipToCountrySubDivisionName,
                GeneralNote = generalNote,
                PaymentTermsDescription = paymentDescription
            };
        }
    }
}
