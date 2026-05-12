using System;
using System.Globalization;
using System.Windows.Data;

namespace DarivaBIM.Presentation.Wpf.Common
{
    /// <summary>
    /// Converte um índice 0-baseado (tipicamente vindo de
    /// <c>ItemsControl.AlternationIndex</c>) em um rótulo numerado começando
    /// em 1 — útil para mostrar "1.", "2.", "3." nos itens de uma lista sem
    /// poluir o view model com campos derivados.
    /// </summary>
    public sealed class OneBasedIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i) return (i + 1).ToString(CultureInfo.InvariantCulture);
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
