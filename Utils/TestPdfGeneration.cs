using System;
using System.Collections.Generic;
using FacturacionAlemana.Models;
using FacturacionAlemana.Services;

namespace FacturacionAlemana.Utils
{
    public static class TestPdfGeneration
    {
        public static void Test()
        {
            // Crear una factura de prueba con productos que tengan los nuevos campos
            var factura = new Factura
            {
                Cliente = "Cliente Comprador GmbH",
                Total = 238.00m,
                DueDate = "30.11.2024",
                DuePayableAmount = "1190.00",
                DateTimeFormat = "102",
                UnitCode = "C62",
                SchemeID = "VA",
                IdElement = "TEST-001",
                TypeCodeElement = "380",
                IssueDateElement = "20241107",
                PaymentNoteElement = "Pago en 30 días",
                SellerDepartmentName = "",
                BuyerID = "BUYER-001",
                LineID = "1",
                SellerAssignedID = "CONS-001",
                ProductName = "Servicio de Consultoría",
                ChargeAmount = "1000.00",
                BilledQuantity = "1.00",
                TaxTypeCode = "VAT",
                TaxCategoryCode = "S",
                TaxRatePercent = "19",
                LineTotalAmount = "1000.00",
                InvoiceCurrencyCode = "EUR",
                PaymentTypeCode = "31",
                PaymentInformation = "SEPA Credit Transfer",
                CalculatedAmount = "190.00",
                InvoiceNumber = "TEST-001",
                IssueDate = DateTime.Now,
                DeliveryDate = DateTime.Now.AddDays(1),
                DueDateValue = DateTime.Now.AddDays(30),
                SellerName = "Empresa Vendedora S.A.",
                SellerLineOne = "Calle Principal 123",
                SellerLineTwo = "",
                SellerPostcodeCode = "28001",
                SellerCityName = "Madrid",
                SellerCountryID = "ES",
                SellerVATID = "ES12345678",
                SellerTaxNumber = "123456789",
                SellerEmail = "vendedor@empresa.com",
                SellerPersonName = "Juan Pérez",
                SellerCompleteNumber = "+34 123 456 789",
                BuyerName = "Cliente Comprador GmbH",
                BuyerLineOne = "Hauptstraße 456",
                BuyerLineTwo = "",
                BuyerPostcodeCode = "10115",
                BuyerCityName = "Berlin",
                BuyerCountryID = "DE",
                BuyerVATID = "DE87654321",
                BuyerEmail = "comprador@cliente.de",
                BuyerPersonName = "Anna Müller",
                BuyerCompleteNumber = "+49 987 654 321",
                BuyerEmailContact = "anna@cliente.de",
                CurrencyID = "EUR",                AccountName = "Empresa Vendedora S.A.",
                IBANID = "ES12345678901234567890",
                BICID = "BANKESMM",
                PaymentDescription = "Wir bitten Sie, den Rechnungsbetrag innerhalb von 30 Tagen ab dem oben genannten Datum auf das angegebene Konto\nzu überweisen und dabei unsere Rechnungsnummer anzugeben. Zahlbar bis: 04. Dezember 2025",
                ProjectNumber = "PROJ-2024-001",
                ContractNumber = "CONTR-2024-001",
                PurchaseOrderNumber = "PO-2024-001",
                SalesOrderNumber = "SO-2024-001",
                PaymentReference = "REF-TEST-001",
                ShipToID = "LUGARENTREGA",
                ShipToName = "LUGARENTREGANOMBREDESTINATARIORECEPTOR",
                ShipToPostcodeCode = "77777",
                ShipToLineOne = "LUGARENTREGADIRECCION1",
                ShipToLineTwo = "LUGARENTREGADIRECCION2",
                ShipToLineThree = "LUGARENTREGADIRECCIONCOMPLEMENTO",
                ShipToCityName = "CIUDADENTREGA",
                ShipToCountryID = "DE",
                ShipToCountrySubDivisionName = "LUGARENTREGAREGION",
                BasisAmount = "200.00",
                TaxAmount = "38.00",
                GrandTotalAmount = "238.00",
                Productos = new List<Producto>
                {
                    new Producto
                    {
                        Pos = 1,
                        Name = "Servicio de Consultoría",
                        Descripcion = "Consultoría especializada en desarrollo de software",
                        SellerAssignedID = "CONS-001",
                        BuyerAssignedID = "CUST-CONS-001",
                        BuyerOrderLineID = "OL-001",
                        BillingStartDate = new DateTime(2024, 1, 1),
                        BillingEndDate = new DateTime(2024, 1, 31),
                        Cantidad = 1,
                        Unit = "H87",
                        PrecioUnitario = 100.00m,
                        PrecioTotal = 100.00m
                    },
                    new Producto
                    {
                        Pos = 2,
                        Name = "Licencia de Software",
                        Descripcion = "Licencia anual para herramienta de gestión",
                        SellerAssignedID = "LIC-002",
                        BuyerAssignedID = "CUST-LIC-002",
                        BuyerOrderLineID = "OL-002",
                        BillingStartDate = new DateTime(2024, 2, 1),
                        BillingEndDate = new DateTime(2024, 2, 28),
                        Cantidad = 2,
                        Unit = "H87",
                        PrecioUnitario = 50.00m,
                        PrecioTotal = 100.00m
                    }
                }
            };

            // Generar PDF en memoria
            var pdfBytes = PdfGeneratorService.GenerarFacturaPdfEnMemoria(factura);

            // Guardar el PDF en el directorio de salida
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"TestFactura_{timestamp}.pdf");
            System.IO.File.WriteAllBytes(outputPath, pdfBytes);

            Console.WriteLine($"PDF generado exitosamente en: {outputPath}");
        }
    }
}
