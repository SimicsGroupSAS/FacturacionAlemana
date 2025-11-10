using System.ComponentModel;

public class Producto : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int Pos { get; set; } // Cambiado de Id a Pos, contador auto incremental
        public string? Name { get; set; } // Nombre del producto (título)
        private string descripcion = "";
        public string Descripcion
        {
            get => descripcion;
            set
            {
                descripcion = value;
                OnPropertyChanged(nameof(Descripcion));
                OnPropertyChanged(nameof(DescripcionCompleta));
            }
        } // Descripción (subtítulo)
        private string? sellerAssignedID;
        public string? SellerAssignedID
        {
            get => sellerAssignedID;
            set
            {
                sellerAssignedID = value;
                OnPropertyChanged(nameof(SellerAssignedID));
                OnPropertyChanged(nameof(DescripcionCompleta));
            }
        } // Código del vendedor
        private string? buyerAssignedID;
        public string? BuyerAssignedID
        {
            get => buyerAssignedID;
            set
            {
                buyerAssignedID = value;
                OnPropertyChanged(nameof(BuyerAssignedID));
                OnPropertyChanged(nameof(DescripcionCompleta));
            }
        } // Código del comprador
        private string? buyerOrderLineID;
        public string? BuyerOrderLineID
        {
            get => buyerOrderLineID;
            set
            {
                buyerOrderLineID = value;
                OnPropertyChanged(nameof(BuyerOrderLineID));
                OnPropertyChanged(nameof(DescripcionCompleta));
            }
        } // Posición de orden del comprador
        private DateTime? billingStartDate;
        public DateTime? BillingStartDate
        {
            get => billingStartDate;
            set
            {
                billingStartDate = value;
                OnPropertyChanged(nameof(BillingStartDate));
                OnPropertyChanged(nameof(DescripcionCompleta));
            }
        } // Fecha de inicio del período
        private DateTime? billingEndDate;
        public DateTime? BillingEndDate
        {
            get => billingEndDate;
            set
            {
                billingEndDate = value;
                OnPropertyChanged(nameof(BillingEndDate));
                OnPropertyChanged(nameof(DescripcionCompleta));
            }
        } // Fecha de fin del período
        public decimal Cantidad { get; set; }
        public string? Unit { get; set; } // Unidad de medida
        public decimal PrecioUnitario { get; set; }
        public decimal PrecioTotal { get; set; }

        public string DescripcionCompleta => $"{Descripcion}\nPeríodo: {BillingStartDate?.ToString("dd.MM.yyyy")} - {BillingEndDate?.ToString("dd.MM.yyyy")}\nCódigo Vendedor: {SellerAssignedID}\nCódigo Comprador: {BuyerAssignedID}\nPosición de Orden: {BuyerOrderLineID}";
    }