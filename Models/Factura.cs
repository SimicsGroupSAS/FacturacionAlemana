namespace FacturacionAlemana.Models
{
    public class Factura
    {
        public required string Cliente { get; set; }
        public required decimal Total { get; set; }

        // Propiedades adicionales
        public required string DueDate { get; set; }
        public required string GrandTotalAmount { get; set; }
        public required string DuePayableAmount { get; set; }
        public required string DateTimeFormat { get; set; }
        public required string UnitCode { get; set; }
        public required string SchemeID { get; set; }
        public required string CurrencyID { get; set; }
        public required string IdElement { get; set; }

        // Propiedades extraídas del XML
        public required string TypeCodeElement { get; set; }
        public required string IssueDateElement { get; set; }
        public required string PaymentNoteElement { get; set; }
        public required string SellerName { get; set; }
        public required string SellerPersonName { get; set; }
        public required string SellerDepartmentName { get; set; }
        public required string SellerCompleteNumber { get; set; }
        public required string SellerEmail { get; set; }
        public required string SellerPostcodeCode { get; set; }
        public required string SellerLineOne { get; set; }
        public required string SellerCityName { get; set; }
        public required string SellerCountryID { get; set; }
        public required string SellerVATID { get; set; }
        public required string BuyerID { get; set; }
        public required string BuyerName { get; set; }
        public required string BuyerPersonName { get; set; }
        public required string BuyerCompleteNumber { get; set; }
        public required string BuyerEmail { get; set; }
        public required string BuyerPostcodeCode { get; set; }
        public required string BuyerLineOne { get; set; }
        public required string BuyerCityName { get; set; }        public required string BuyerCountryID { get; set; }
        public string BuyerReference { get; set; } = string.Empty; // Referencia del comprador (puede estar vacía)
        public required string LineID { get; set; }
        public required string SellerAssignedID { get; set; }
        public required string ProductName { get; set; }
        public required string ChargeAmount { get; set; }
        public required string BilledQuantity { get; set; }
        public required string TaxTypeCode { get; set; }
        public required string TaxCategoryCode { get; set; }
        public required string TaxRatePercent { get; set; }
        public required string LineTotalAmount { get; set; }
        public required string InvoiceCurrencyCode { get; set; }
        public required string PaymentTypeCode { get; set; }
        public required string PaymentInformation { get; set; }
        public required string IBANID { get; set; }
        public required string AccountName { get; set; }
        public required string BICID { get; set; }
        public required string CalculatedAmount { get; set; }        public required string BasisAmount { get; set; }
        public required string PaymentDescription { get; set; }
        public string TaxAmount { get; set; } = "0"; // Monto total de impuestos

        public List<Producto> Productos { get; set; } = new List<Producto>(); // Agregar lista de productos
    }
}