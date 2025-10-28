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

            valor = valor.Replace(".", "").Replace(",", ".");
            
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

            var xmlDoc = XDocument.Load(filePath);

            // Detalles de la Factura
            var idElement = xmlDoc.Descendants(rsm + "ExchangedDocument").Elements(ram + "ID").FirstOrDefault()?.Value ?? "No encontrado";
            var typeCodeElement = xmlDoc.Descendants(rsm + "ExchangedDocument").Elements(ram + "TypeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var issueDateElement = xmlDoc.Descendants(rsm + "ExchangedDocument").Elements(ram + "IssueDateTime").Elements(udt + "DateTimeString").FirstOrDefault()?.Value ?? "No encontrado";
            
            var notas = ExtraerNotas(xmlDoc, rsm, ram);
            var paymentNoteElement = notas.Count > 0 ? notas[0] : "No encontrado";

            // Detalles del Vendedor
            var sellerName = xmlDoc.Descendants(ram + "SellerTradeParty").Elements(ram + "Name").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerPersonName = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "PersonName").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerDepartmentName = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "DepartmentName").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerCompleteNumber = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "CompleteNumber").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerEmail = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "URIID").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerPostcodeCode = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "PostcodeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerLineOne = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "LineOne").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerCityName = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "CityName").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerCountryIDRaw = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "CountryID").FirstOrDefault()?.Value ?? "XX";
            var sellerCountryID = NormalizarCodigoPais(sellerCountryIDRaw);
            var sellerVATID = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "ID").FirstOrDefault()?.Value ?? "No encontrado";

            // Detalles del Comprador
            var buyerID = xmlDoc.Descendants(ram + "BuyerTradeParty").Elements(ram + "ID").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerName = xmlDoc.Descendants(ram + "BuyerTradeParty").Elements(ram + "Name").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerPersonName = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "PersonName").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerCompleteNumber = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "CompleteNumber").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerEmail = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "URIID").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerPostcodeCode = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "PostcodeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerLineOne = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "LineOne").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerCityName = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "CityName").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerCountryIDRaw = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "CountryID").FirstOrDefault()?.Value ?? "XX";
            var buyerCountryID = NormalizarCodigoPais(buyerCountryIDRaw);

            // Detalles de los Ítems de Línea
            var lineID = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "LineID").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerAssignedID = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "SellerAssignedID").FirstOrDefault()?.Value ?? "No encontrado";
            var productName = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "Name").FirstOrDefault()?.Value ?? "No encontrado";
            var chargeAmount = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "ChargeAmount").FirstOrDefault()?.Value ?? "No encontrado";
            var billedQuantity = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "BilledQuantity").FirstOrDefault()?.Value ?? "No encontrado";
            var taxTypeCode = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "TypeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var taxCategoryCode = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "CategoryCode").FirstOrDefault()?.Value ?? "No encontrado";
            var taxRatePercent = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "RateApplicablePercent").FirstOrDefault()?.Value ?? "No encontrado";
            var lineTotalAmount = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "LineTotalAmount").FirstOrDefault()?.Value ?? "No encontrado";

            // Resumen de Pago
            var invoiceCurrencyCode = xmlDoc.Descendants(ram + "InvoiceCurrencyCode").FirstOrDefault()?.Value ?? "No encontrado";
            var paymentTypeCode = xmlDoc.Descendants(ram + "SpecifiedTradeSettlementPaymentMeans").Descendants(ram + "TypeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var paymentInformation = xmlDoc.Descendants(ram + "SpecifiedTradeSettlementPaymentMeans").Descendants(ram + "Information").FirstOrDefault()?.Value ?? "No encontrado";
            var ibanID = xmlDoc.Descendants(ram + "PayeePartyCreditorFinancialAccount").Descendants(ram + "IBANID").FirstOrDefault()?.Value ?? "No encontrado";
            var accountName = xmlDoc.Descendants(ram + "PayeePartyCreditorFinancialAccount").Descendants(ram + "AccountName").FirstOrDefault()?.Value ?? "No encontrado";
            var bicID = xmlDoc.Descendants(ram + "PayeeSpecifiedCreditorFinancialInstitution").Descendants(ram + "BICID").FirstOrDefault()?.Value ?? "No encontrado";
            
            var calculatedAmountRaw = xmlDoc.Descendants(ram + "ApplicableTradeTax").Descendants(ram + "CalculatedAmount").FirstOrDefault()?.Value ?? "0";
            var calculatedAmount = NormalizarDecimal(calculatedAmountRaw).ToString("F2");
            
            var basisAmountRaw = xmlDoc.Descendants(ram + "ApplicableTradeTax").Descendants(ram + "BasisAmount").FirstOrDefault()?.Value ?? "0";
            var basisAmount = NormalizarDecimal(basisAmountRaw).ToString("F2");
              var paymentDescription = xmlDoc.Descendants(ram + "SpecifiedTradePaymentTerms").Descendants(ram + "Description").FirstOrDefault()?.Value ?? "No encontrado";

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
            var duePayableAmount = NormalizarDecimal(duePayableAmountRaw).ToString("F2");

            var dateTimeFormat = xmlDoc.Descendants(udt + "DateTimeString").Attributes("format").FirstOrDefault()?.Value ?? "No encontrado";
            var unitCode = xmlDoc.Descendants(ram + "BilledQuantity").Attributes("unitCode").FirstOrDefault()?.Value ?? "No encontrado";
            var schemeID = xmlDoc.Descendants(ram + "SpecifiedTaxRegistration").Descendants(ram + "ID").Attributes("schemeID").FirstOrDefault()?.Value ?? "No encontrado";
            var currencyID = xmlDoc.Descendants(ram + "TaxTotalAmount").Attributes("currencyID").FirstOrDefault()?.Value ?? "No encontrado";

            var productos = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Select(item => new Producto
            {
                Id = item.Descendants(ram + "LineID").FirstOrDefault()?.Value ?? "No encontrado",
                Descripcion = item.Descendants(ram + "Name").FirstOrDefault()?.Value ?? "No encontrado",
                Cantidad = NormalizarDecimal(item.Descendants(ram + "BilledQuantity").FirstOrDefault()?.Value),
                PrecioUnitario = NormalizarDecimal(item.Descendants(ram + "ChargeAmount").FirstOrDefault()?.Value),
                PrecioTotal = NormalizarDecimal(item.Descendants(ram + "LineTotalAmount").FirstOrDefault()?.Value)
            }).ToList();

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
                SellerCityName = sellerCityName,
                SellerCountryID = sellerCountryID,
                SellerVATID = sellerVATID,
                BuyerID = buyerID,
                BuyerName = buyerName,
                BuyerPersonName = buyerPersonName,
                BuyerCompleteNumber = buyerCompleteNumber,
                BuyerEmail = buyerEmail,
                BuyerPostcodeCode = buyerPostcodeCode,
                BuyerLineOne = buyerLineOne,
                BuyerCityName = buyerCityName,
                BuyerCountryID = buyerCountryID,
                LineID = lineID,
                SellerAssignedID = sellerAssignedID,
                ProductName = productName,
                ChargeAmount = chargeAmount,
                BilledQuantity = billedQuantity,
                TaxTypeCode = taxTypeCode,
                TaxCategoryCode = taxCategoryCode,
                TaxRatePercent = taxRatePercent,
                LineTotalAmount = lineTotalAmount,
                InvoiceCurrencyCode = invoiceCurrencyCode,
                PaymentTypeCode = paymentTypeCode,
                PaymentInformation = paymentInformation,
                IBANID = ibanID,
                AccountName = accountName,
                BICID = bicID,
                CalculatedAmount = calculatedAmount,
                BasisAmount = basisAmount,
                PaymentDescription = paymentDescription,
                Productos = productos
            };
        }
    }
}
