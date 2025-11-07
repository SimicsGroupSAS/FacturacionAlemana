public class Producto
    {
        public int Pos { get; set; } // Cambiado de Id a Pos, contador auto incremental
        public required string Name { get; set; } // Nombre del producto (título)
        public required string Descripcion { get; set; } // Descripción (subtítulo)
        public string? SellerAssignedID { get; set; } // Código del vendedor
        public string? BuyerAssignedID { get; set; } // Código del comprador
        public string? BuyerOrderLineID { get; set; } // Posición de orden del comprador
        public DateTime? BillingStartDate { get; set; } // Fecha de inicio del período
        public DateTime? BillingEndDate { get; set; } // Fecha de fin del período
        public decimal Cantidad { get; set; }
        public string? Unit { get; set; } // Unidad de medida
        public decimal PrecioUnitario { get; set; }
        public decimal PrecioTotal { get; set; }
    }