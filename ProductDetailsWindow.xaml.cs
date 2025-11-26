using System.Globalization;
using System.Windows;
using System.Windows.Input;
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
                BillingEndDatePicker.SelectedDate = existingProducto.BillingEndDate;                // Cargar precio y cantidad usando cultura invariante para evitar problemas con separadores decimales
                PrecioUnitarioTextBox.Text = existingProducto.PrecioUnitario.ToString("F2", CultureInfo.InvariantCulture);
                CantidadTextBox.Text = existingProducto.Cantidad.ToString("G", CultureInfo.InvariantCulture);
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
            }            // Obtener controles mediante FindName para evitar dependencias de campos generados
            var productNameCtrl = this.FindName("ProductNameTextBox") as System.Windows.Controls.TextBox;
            var descripcionCtrl = this.FindName("DescripcionTextBox") as System.Windows.Controls.TextBox;
            var sellerIdCtrl = this.FindName("SellerAssignedIDTextBox") as System.Windows.Controls.TextBox;
            var buyerIdCtrl = this.FindName("BuyerAssignedIDTextBox") as System.Windows.Controls.TextBox;
            var buyerLineCtrl = this.FindName("BuyerOrderLineIDTextBox") as System.Windows.Controls.TextBox;
            var unitCombo = this.FindName("UnitComboBox") as System.Windows.Controls.ComboBox;
            var startDatePicker = this.FindName("BillingStartDatePicker") as System.Windows.Controls.DatePicker;
            var endDatePicker = this.FindName("BillingEndDatePicker") as System.Windows.Controls.DatePicker;
            var precioUnitarioCtrl = this.FindName("PrecioUnitarioTextBox") as System.Windows.Controls.TextBox;
            var cantidadCtrl = this.FindName("CantidadTextBox") as System.Windows.Controls.TextBox;

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
            Producto.Unit = !string.IsNullOrEmpty(unidad) ? unidad : "EA";            // Asignar fechas directamente desde DatePicker
            Producto.BillingStartDate = startDatePicker?.SelectedDate;
            Producto.BillingEndDate = endDatePicker?.SelectedDate;            // Validar y actualizar precio unitario
            var precioStr = precioUnitarioCtrl?.Text?.Trim() ?? "0";
            // Normalizar el separador decimal: convertir coma a punto para InvariantCulture
            precioStr = precioStr.Replace(",", ".");
            if (decimal.TryParse(precioStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var precioVal))
            {
                Producto.PrecioUnitario = precioVal;
            }
            else
            {
                MessageBox.Show("El precio unitario debe ser un número válido.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }            // Validar y actualizar cantidad
            var cantidadStr = cantidadCtrl?.Text?.Trim() ?? "1";
            // Normalizar el separador decimal: convertir coma a punto para InvariantCulture
            cantidadStr = cantidadStr.Replace(",", ".");
            if (decimal.TryParse(cantidadStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var cantidadVal))
            {
                if (cantidadVal <= 0)
                {
                    MessageBox.Show("La cantidad debe ser mayor a 0.", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                Producto.Cantidad = cantidadVal;
            }
            else
            {
                MessageBox.Show("La cantidad debe ser un número válido.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Recalcular PrecioTotal basado en cantidad y precio unitario
            Producto.PrecioTotal = Producto.Cantidad * Producto.PrecioUnitario;

            DialogResult = true;
            Close();
        }

        private void OnlyNumeric_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextNumeric(e.Text);
        }

        private void OnlyNumeric_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!IsTextNumeric(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }        private static bool IsTextNumeric(string text)
        {
            // Permitir dígitos, coma, punto y backspace
            // Solo un separador decimal por entrada (la validación completa ocurre en TryParse)
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"^[0-9,.\b]+$");
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
