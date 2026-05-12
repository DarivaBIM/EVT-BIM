using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DarivaBIM.Presentation.Wpf.Common
{
    /// <summary>
    /// Converte um <see cref="double"/> de view model em <see cref="GridLength"/>
    /// pixel-fixo, e ignora o convertBack (a manipulação é sempre code-behind
    /// disparando OnPropertyChanged no view model). Usado para que cabeçalho
    /// e linhas compartilhem larguras de coluna controladas por thumbs.
    /// </summary>
    public sealed class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && !double.IsNaN(d) && d > 0)
                return new GridLength(d);
            return new GridLength(80);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GridLength gl && gl.IsAbsolute) return gl.Value;
            return 0d;
        }
    }
}
