using System.Globalization;
using System.Windows;
using FacturacionAlemana.Models;
using System.Collections.Generic;

namespace FacturacionAlemana
{
    public partial class ProductDetailsWindow : Window
    {
        public Producto? Producto { get; set; }

        public ProductDetailsWindow(Producto? existingProducto = null)
        {
            InitializeComponent();
            
            UnitComboBox.ItemsSource = new List<string> { "H87", "EA", "PCE", "KG", "LTR", "MTR", "BOX", "PAL" };
            
            if (existingProducto != null)
            {
                Producto = existingProducto;
                ProductNameTextBox.Text = existingProducto.Name;
                DescripcionTextBox.Text = existingProducto.Descripcion;
                SellerAssignedIDTextBox.Text = existingProducto.SellerAssignedID ?? "";
                BuyerAssignedIDTextBox.Text = existingProducto.BuyerAssignedID ?? "";
                BuyerOrderLineIDTextBox.Text = existingProducto.BuyerOrderLineID ?? "";
                UnitComboBox.SelectedItem = existingProducto.Unit ?? "H87";
                
                BillingStartDatePicker.SelectedDate = existingProducto.BillingStartDate;
                BillingEndDatePicker.SelectedDate = existingProducto.BillingEndDate;
            }
            else
            {
                UnitComboBox.SelectedItem = "H87";
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

            // Validar y actualizar el producto
            Producto.Descripcion = DescripcionTextBox.Text?.Trim() ?? "";
            Producto.SellerAssignedID = SellerAssignedIDTextBox.Text?.Trim();
            Producto.BuyerAssignedID = BuyerAssignedIDTextBox.Text?.Trim();
            Producto.BuyerOrderLineID = BuyerOrderLineIDTextBox.Text?.Trim();
            Producto.Unit = UnitComboBox.SelectedItem?.ToString() ?? "H87";

            // Asignar fechas directamente desde DatePicker
            Producto.BillingStartDate = BillingStartDatePicker.SelectedDate;
            Producto.BillingEndDate = BillingEndDatePicker.SelectedDate;

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
