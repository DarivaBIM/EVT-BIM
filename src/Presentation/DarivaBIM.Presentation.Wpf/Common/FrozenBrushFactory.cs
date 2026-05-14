using System.Windows.Media;

namespace DarivaBIM.Presentation.Wpf.Common
{
    /// <summary>
    /// Constrói <see cref="SolidColorBrush"/>es congelados a partir de strings
    /// hexadecimais (<c>#RRGGBB</c> / <c>#AARRGGBB</c>). Freeze elimina o
    /// custo de dispatcher cross-thread — chamada certa para brushes que são
    /// reutilizados por centenas de cards/chips sem nunca mudarem de cor.
    /// </summary>
    public static class FrozenBrushFactory
    {
        public static SolidColorBrush FromHex(string hex)
        {
            SolidColorBrush brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            brush.Freeze();
            return brush;
        }
    }
}
