using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.UtilizationPoints
{
    /// <summary>
    /// Linha do dropdown de tipos de família. Encapsula o
    /// <see cref="FamilyTypeOptionDto"/> e expõe propriedades amigáveis para
    /// data templates (nome composto, categoria visível).
    /// </summary>
    public class FamilyTypeOptionViewModel : ObservableObject
    {
        public FamilyTypeOptionViewModel(FamilyTypeOptionDto dto)
        {
            Dto = dto;
        }

        public FamilyTypeOptionDto Dto { get; }

        public long ElementId => Dto.ElementId;
        public string UniqueId => Dto.UniqueId;
        public string FamilyName => Dto.FamilyName;
        public string TypeName => Dto.TypeName;
        public string? CategoryName => Dto.CategoryName;
        public string DisplayName => Dto.DisplayName;

        public string SearchKey => $"{FamilyName} {TypeName} {CategoryName}".ToLowerInvariant();
    }
}
