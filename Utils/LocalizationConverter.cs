using System;
using System.Globalization;
using System.Windows.Data;
using FacturacionAlemana.Services;

namespace FacturacionAlemana.Utils
{
    /// <summary>
    /// Conversor para obtener strings localizados desde XAML
    /// Uso: {Binding Converter={StaticResource LocalizationConverter}, ConverterParameter=HomePage.Title}
    /// </summary>
    public class LocalizationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not string key)
                return string.Empty;

            var localization = LocalizationService.Instance;
            return localization.Get(key);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
