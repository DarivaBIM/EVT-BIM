using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DarivaBIM.Presentation.Wpf.Common
{
    /// <summary>
    /// Converte <c>true</c> em <see cref="Visibility.Collapsed"/> e qualquer
    /// outro valor em <see cref="Visibility.Visible"/>. Útil para esconder
    /// regiões da UI quando uma flag (ex.: <c>IsCollapsed</c> de um toggle)
    /// está ativa.
    /// </summary>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Collapsed;
    }
}
