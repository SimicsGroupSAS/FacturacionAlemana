public class Producto
    {
        public required string Id { get; set; } // Agregar 'required' para evitar valores NULL
        public required string Descripcion { get; set; } // Agregar 'required' para evitar valores NULL
        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal PrecioTotal { get; set; }
    }