using System.Globalization;
using System.Windows;
using FacturacionAlemana.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FacturacionAlemana
{
    public partial class ProductDetailsWindow : Window
    {
        public Producto? Producto { get; set; }
        // Colección mutable para poder agregar unidades introducidas por el usuario
        private ObservableCollection<string> UnitOptions;
        public ProductDetailsWindow(Producto? existingProducto = null)
        {            
            InitializeComponent();
            
            UnitOptions = new ObservableCollection<string> { "EA", "KG", "H87", "HUR","KGM", "LTR", "MTR", "PAL", "C62", "BOX" };
            UnitComboBox.ItemsSource = UnitOptions;
            
            if (existingProducto != null)
            {
                Producto = existingProducto;
                ProductNameTextBox.Text = existingProducto.Name;
                DescripcionTextBox.Text = existingProducto.Descripcion;
                SellerAssignedIDTextBox.Text = existingProducto.SellerAssignedID ?? "";
                BuyerAssignedIDTextBox.Text = existingProducto.BuyerAssignedID ?? "";
                BuyerOrderLineIDTextBox.Text = existingProducto.BuyerOrderLineID ?? "";
                // Mostrar la unidad existente (si el usuario la edita quedará en UnitComboBox.Text)
                UnitComboBox.Text = existingProducto.Unit ?? "EA";
                
                BillingStartDatePicker.SelectedDate = existingProducto.BillingStartDate;
                BillingEndDatePicker.SelectedDate = existingProducto.BillingEndDate;
            }
            else
            {
                UnitComboBox.Text = "EA";
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (Producto == null)
            {
                MessageBox.Show("Error: No hay producto seleccionado.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Obtener controles mediante FindName para evitar dependencias de campos generados
            var productNameCtrl = this.FindName("ProductNameTextBox") as System.Windows.Controls.TextBox;
            var descripcionCtrl = this.FindName("DescripcionTextBox") as System.Windows.Controls.TextBox;
            var sellerIdCtrl = this.FindName("SellerAssignedIDTextBox") as System.Windows.Controls.TextBox;
            var buyerIdCtrl = this.FindName("BuyerAssignedIDTextBox") as System.Windows.Controls.TextBox;
            var buyerLineCtrl = this.FindName("BuyerOrderLineIDTextBox") as System.Windows.Controls.TextBox;
            var unitCombo = this.FindName("UnitComboBox") as System.Windows.Controls.ComboBox;
            var startDatePicker = this.FindName("BillingStartDatePicker") as System.Windows.Controls.DatePicker;
            var endDatePicker = this.FindName("BillingEndDatePicker") as System.Windows.Controls.DatePicker;

            // Guardar nombre editable
            var nombre = productNameCtrl?.Text?.Trim();
            if (string.IsNullOrEmpty(nombre))
            {
                MessageBox.Show("El nombre del producto no puede estar vacío.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            Producto.Name = nombre;

            // Validar y actualizar el producto
            Producto.Descripcion = descripcionCtrl?.Text?.Trim() ?? "";
            Producto.SellerAssignedID = sellerIdCtrl?.Text?.Trim();
            Producto.BuyerAssignedID = buyerIdCtrl?.Text?.Trim();
            Producto.BuyerOrderLineID = buyerLineCtrl?.Text?.Trim();
            // Leer el texto escrito por el usuario (funciona aunque no exista como ítem)
            var unidad = unitCombo?.Text?.Trim();
            if (!string.IsNullOrEmpty(unidad) && !UnitOptions.Contains(unidad))
            {
                UnitOptions.Add(unidad);
            }
            Producto.Unit = !string.IsNullOrEmpty(unidad) ? unidad : "EA";

            // Asignar fechas directamente desde DatePicker
            Producto.BillingStartDate = startDatePicker?.SelectedDate;
            Producto.BillingEndDate = endDatePicker?.SelectedDate;

            // Validar coherencia de fechas
            if (Producto.BillingStartDate.HasValue && Producto.BillingEndDate.HasValue 
                && Producto.BillingStartDate > Producto.BillingEndDate)
            {
                MessageBox.Show("La fecha de inicio no puede ser mayor que la fecha de fin.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
