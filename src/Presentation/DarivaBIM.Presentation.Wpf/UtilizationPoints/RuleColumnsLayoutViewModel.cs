using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.UtilizationPoints
{
    /// <summary>
    /// Larguras compartilhadas das colunas da tabela de regras. Tanto o
    /// cabeçalho quanto cada linha de regra fazem binding aqui, então mover
    /// um thumb no cabeçalho atualiza simultaneamente todas as linhas. Mantém
    /// limites mínimos para que nenhuma coluna desapareça em janelas estreitas.
    /// </summary>
    public class RuleColumnsLayoutViewModel : ObservableObject
    {
        public const double DragHandleWidth = 28;
        public const double ActionsWidth = 64;

        public const double NameMin = 120;
        public const double NameDefault = 180;
        public const double NameMax = 360;

        public const double TypeMin = 220;
        public const double TypeDefault = 320;
        public const double TypeMax = 560;

        public const double NumberMin = 60;
        public const double NumberDefault = 84;
        public const double NumberMax = 140;

        public const double StatusMin = 80;
        public const double StatusDefault = 96;
        public const double StatusMax = 160;

        private double _nameWidth = NameDefault;
        public double NameWidth
        {
            get => _nameWidth;
            set => SetField(ref _nameWidth, Clamp(value, NameMin, NameMax));
        }

        private double _typeWidth = TypeDefault;
        public double TypeWidth
        {
            get => _typeWidth;
            set => SetField(ref _typeWidth, Clamp(value, TypeMin, TypeMax));
        }

        private double _minHeightWidth = NumberDefault;
        public double MinHeightWidth
        {
            get => _minHeightWidth;
            set => SetField(ref _minHeightWidth, Clamp(value, NumberMin, NumberMax));
        }

        private double _maxHeightWidth = NumberDefault;
        public double MaxHeightWidth
        {
            get => _maxHeightWidth;
            set => SetField(ref _maxHeightWidth, Clamp(value, NumberMin, NumberMax));
        }

        private double _statusWidth = StatusDefault;
        public double StatusWidth
        {
            get => _statusWidth;
            set => SetField(ref _statusWidth, Clamp(value, StatusMin, StatusMax));
        }

        private static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value)) return min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
