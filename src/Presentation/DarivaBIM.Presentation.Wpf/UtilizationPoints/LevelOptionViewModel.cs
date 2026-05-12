using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.UtilizationPoints
{
    /// <summary>
    /// Item do dropdown "Nível de referência". Inclui um item especial
    /// "Usar nível do elemento" (ElementId nulo) sempre presente na lista,
    /// que reflete o fallback automático do algoritmo.
    /// </summary>
    public class LevelOptionViewModel : ObservableObject
    {
        public LevelOptionViewModel(LevelOptionDto? dto, string displayName)
        {
            Dto = dto;
            DisplayName = displayName;
        }

        public LevelOptionDto? Dto { get; }
        public long? ElementId => Dto?.ElementId;
        public string DisplayName { get; }
        public string ElevationLabel => Dto == null ? string.Empty : $"{Dto.ElevationMeters:0.000} m";
    }
}
