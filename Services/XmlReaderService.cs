using System.Xml.Linq;
using System.Globalization;
using FacturacionAlemana.Models;

namespace FacturacionAlemana.Services
{
    public static class XmlReaderService
    {
        public static Factura LeerFacturaDesdeXml(string filePath)
        {
            XNamespace rsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
            XNamespace ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
            XNamespace udt = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";

            var xmlDoc = XDocument.Load(filePath);

            // Extract Invoice Details
            var idElement = xmlDoc.Descendants(rsm + "ExchangedDocument").Elements(ram + "ID").FirstOrDefault()?.Value ?? "No encontrado";
            var typeCodeElement = xmlDoc.Descendants(rsm + "ExchangedDocument").Elements(ram + "TypeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var issueDateElement = xmlDoc.Descendants(rsm + "ExchangedDocument").Elements(ram + "IssueDateTime").Elements(udt + "DateTimeString").FirstOrDefault()?.Value ?? "No encontrado";
            var paymentNoteElement = xmlDoc.Descendants(rsm + "ExchangedDocument").Elements(ram + "IncludedNote").Elements(ram + "Content").FirstOrDefault()?.Value ?? "No encontrado";

            // Log extracted data
            Console.WriteLine("Detalles de la Factura:");
            Console.WriteLine($"ID: {idElement}");
            Console.WriteLine($"TypeCode: {typeCodeElement}");
            Console.WriteLine($"IssueDate: {issueDateElement}");
            Console.WriteLine($"PaymentNote: {paymentNoteElement}");

            // Extract Seller Details
            var sellerName = xmlDoc.Descendants(ram + "SellerTradeParty").Elements(ram + "Name").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerPersonName = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "PersonName").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerDepartmentName = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "DepartmentName").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerCompleteNumber = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "CompleteNumber").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerEmail = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "URIID").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerPostcodeCode = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "PostcodeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerLineOne = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "LineOne").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerCityName = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "CityName").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerCountryID = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "CountryID").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerVATID = xmlDoc.Descendants(ram + "SellerTradeParty").Descendants(ram + "ID").FirstOrDefault()?.Value ?? "No encontrado";

            // Log seller details
            Console.WriteLine("Detalles del Vendedor:");
            Console.WriteLine($"Nombre: {sellerName}");
            Console.WriteLine($"Persona de Contacto: {sellerPersonName}");
            Console.WriteLine($"Departamento: {sellerDepartmentName}");
            Console.WriteLine($"Número Completo: {sellerCompleteNumber}");
            Console.WriteLine($"Email: {sellerEmail}");
            Console.WriteLine($"Código Postal: {sellerPostcodeCode}");
            Console.WriteLine($"Dirección Línea Uno: {sellerLineOne}");
            Console.WriteLine($"Ciudad: {sellerCityName}");
            Console.WriteLine($"País ID: {sellerCountryID}");
            Console.WriteLine($"VAT ID: {sellerVATID}");

            // Extract Buyer Details
            var buyerID = xmlDoc.Descendants(ram + "BuyerTradeParty").Elements(ram + "ID").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerName = xmlDoc.Descendants(ram + "BuyerTradeParty").Elements(ram + "Name").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerPersonName = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "PersonName").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerCompleteNumber = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "CompleteNumber").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerEmail = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "URIID").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerPostcodeCode = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "PostcodeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerLineOne = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "LineOne").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerCityName = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "CityName").FirstOrDefault()?.Value ?? "No encontrado";
            var buyerCountryID = xmlDoc.Descendants(ram + "BuyerTradeParty").Descendants(ram + "CountryID").FirstOrDefault()?.Value ?? "No encontrado";

            // Log buyer details
            Console.WriteLine("Detalles del Comprador:");
            Console.WriteLine($"ID: {buyerID}");
            Console.WriteLine($"Nombre: {buyerName}");
            Console.WriteLine($"Persona de Contacto: {buyerPersonName}");
            Console.WriteLine($"Número Completo: {buyerCompleteNumber}");
            Console.WriteLine($"Email: {buyerEmail}");
            Console.WriteLine($"Código Postal: {buyerPostcodeCode}");
            Console.WriteLine($"Dirección Línea Uno: {buyerLineOne}");
            Console.WriteLine($"Ciudad: {buyerCityName}");
            Console.WriteLine($"País ID: {buyerCountryID}");

            // Extract Line Item Details
            var lineID = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "LineID").FirstOrDefault()?.Value ?? "No encontrado";
            var sellerAssignedID = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "SellerAssignedID").FirstOrDefault()?.Value ?? "No encontrado";
            var productName = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "Name").FirstOrDefault()?.Value ?? "No encontrado";
            var chargeAmount = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "ChargeAmount").FirstOrDefault()?.Value ?? "No encontrado";
            var billedQuantity = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "BilledQuantity").FirstOrDefault()?.Value ?? "No encontrado";
            var taxTypeCode = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "TypeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var taxCategoryCode = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "CategoryCode").FirstOrDefault()?.Value ?? "No encontrado";
            var taxRatePercent = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "RateApplicablePercent").FirstOrDefault()?.Value ?? "No encontrado";
            var lineTotalAmount = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Descendants(ram + "LineTotalAmount").FirstOrDefault()?.Value ?? "No encontrado";

            // Log line item details
            Console.WriteLine("Detalles de los Ítems de Línea:");
            Console.WriteLine($"Line ID: {lineID}");
            Console.WriteLine($"Seller Assigned ID: {sellerAssignedID}");
            Console.WriteLine($"Nombre del Producto: {productName}");
            Console.WriteLine($"Monto de Cargo: {chargeAmount}");
            Console.WriteLine($"Cantidad Facturada: {billedQuantity}");
            Console.WriteLine($"Código de Tipo de Impuesto: {taxTypeCode}");
            Console.WriteLine($"Código de Categoría de Impuesto: {taxCategoryCode}");
            Console.WriteLine($"Porcentaje de Tasa de Impuesto: {taxRatePercent}");
            Console.WriteLine($"Monto Total de Línea: {lineTotalAmount}");

            // Extract Payment Summary
            var invoiceCurrencyCode = xmlDoc.Descendants(ram + "InvoiceCurrencyCode").FirstOrDefault()?.Value ?? "No encontrado";
            var paymentTypeCode = xmlDoc.Descendants(ram + "SpecifiedTradeSettlementPaymentMeans").Descendants(ram + "TypeCode").FirstOrDefault()?.Value ?? "No encontrado";
            var paymentInformation = xmlDoc.Descendants(ram + "SpecifiedTradeSettlementPaymentMeans").Descendants(ram + "Information").FirstOrDefault()?.Value ?? "No encontrado";
            var ibanID = xmlDoc.Descendants(ram + "PayeePartyCreditorFinancialAccount").Descendants(ram + "IBANID").FirstOrDefault()?.Value ?? "No encontrado";
            var accountName = xmlDoc.Descendants(ram + "PayeePartyCreditorFinancialAccount").Descendants(ram + "AccountName").FirstOrDefault()?.Value ?? "No encontrado";
            var bicID = xmlDoc.Descendants(ram + "PayeeSpecifiedCreditorFinancialInstitution").Descendants(ram + "BICID").FirstOrDefault()?.Value ?? "No encontrado";
            var calculatedAmount = xmlDoc.Descendants(ram + "ApplicableTradeTax").Descendants(ram + "CalculatedAmount").FirstOrDefault()?.Value ?? "No encontrado";
            var basisAmount = xmlDoc.Descendants(ram + "ApplicableTradeTax").Descendants(ram + "BasisAmount").FirstOrDefault()?.Value ?? "No encontrado";
            var paymentDescription = xmlDoc.Descendants(ram + "SpecifiedTradePaymentTerms").Descendants(ram + "Description").FirstOrDefault()?.Value ?? "No encontrado";

            // Formatear la fecha al estilo dd-mm-aaaa
            var dueDateRaw = xmlDoc.Descendants(ram + "SpecifiedTradePaymentTerms").Descendants(udt + "DateTimeString").FirstOrDefault()?.Value ?? "No encontrado";
            var dueDate = DateTime.TryParseExact(dueDateRaw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
                ? parsedDate.ToString("dd-MM-yyyy")
                : "No encontrado";

            var grandTotalAmount = xmlDoc.Descendants(ram + "SpecifiedTradeSettlementHeaderMonetarySummation").Descendants(ram + "GrandTotalAmount").FirstOrDefault()?.Value ?? "No encontrado";
            var duePayableAmount = xmlDoc.Descendants(ram + "SpecifiedTradeSettlementHeaderMonetarySummation").Descendants(ram + "DuePayableAmount").FirstOrDefault()?.Value ?? "No encontrado";

            // Log payment summary
            Console.WriteLine("Resumen de Pago:");
            Console.WriteLine($"Código de Moneda de Factura: {invoiceCurrencyCode}");
            Console.WriteLine($"Código de Tipo de Pago: {paymentTypeCode}");
            Console.WriteLine($"Información de Pago: {paymentInformation}");
            Console.WriteLine($"IBAN ID: {ibanID}");
            Console.WriteLine($"Nombre de la Cuenta: {accountName}");
            Console.WriteLine($"BIC ID: {bicID}");
            Console.WriteLine($"Monto Calculado: {calculatedAmount}");
            Console.WriteLine($"Monto Base: {basisAmount}");
            Console.WriteLine($"Descripción de Pago: {paymentDescription}");
            Console.WriteLine($"Fecha de Vencimiento: {dueDate}");
            Console.WriteLine($"Monto Total: {grandTotalAmount}");
            Console.WriteLine($"Monto Pagadero: {duePayableAmount}");

            // Extract attributes
            var dateTimeFormat = xmlDoc.Descendants(udt + "DateTimeString").Attributes("format").FirstOrDefault()?.Value ?? "No encontrado";
            var unitCode = xmlDoc.Descendants(ram + "BilledQuantity").Attributes("unitCode").FirstOrDefault()?.Value ?? "No encontrado";
            var schemeID = xmlDoc.Descendants(ram + "SpecifiedTaxRegistration").Descendants(ram + "ID").Attributes("schemeID").FirstOrDefault()?.Value ?? "No encontrado";
            var currencyID = xmlDoc.Descendants(ram + "TaxTotalAmount").Attributes("currencyID").FirstOrDefault()?.Value ?? "No encontrado";

            // Log attributes
            Console.WriteLine("Atributos:");
            Console.WriteLine($"Formato de Fecha y Hora: {dateTimeFormat}");
            Console.WriteLine($"Código de Unidad: {unitCode}");
            Console.WriteLine($"ID de Esquema: {schemeID}");
            Console.WriteLine($"ID de Moneda: {currencyID}");

            var productos = xmlDoc.Descendants(ram + "IncludedSupplyChainTradeLineItem").Select(item => new Producto
            {
                Id = item.Descendants(ram + "LineID").FirstOrDefault()?.Value ?? "No encontrado",
                Descripcion = item.Descendants(ram + "Name").FirstOrDefault()?.Value ?? "No encontrado",
                Cantidad = decimal.TryParse(item.Descendants(ram + "BilledQuantity").FirstOrDefault()?.Value, out var cantidad) ? cantidad : 0,
                PrecioUnitario = decimal.TryParse(item.Descendants(ram + "ChargeAmount").FirstOrDefault()?.Value, out var precioUnitario) ? precioUnitario : 0,
                PrecioTotal = decimal.TryParse(item.Descendants(ram + "LineTotalAmount").FirstOrDefault()?.Value, out var precioTotal) ? precioTotal : 0
            }).ToList();

            return new Factura
            {
                IdElement = idElement, // Asignar el ID correctamente usando IdElement
                Cliente = buyerName,
                Total = decimal.TryParse(grandTotalAmount, out var total) ? total : 0,
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