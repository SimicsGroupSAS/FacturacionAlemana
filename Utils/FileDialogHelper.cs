using Microsoft.Win32;

namespace FacturacionAlemana.Utils
{
    public static class FileDialogHelper
    {
        public static string? AbrirArchivoXml()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Archivos XML (*.xml)|*.xml",
                Title = "Seleccionar archivo XML"
            };

            return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
        }
    }
}