namespace DarivaBIM.Application.DTOs.UtilizationPoints
{
    /// <summary>
    /// Tipo de família (FamilySymbol) disponível para inserção como ponto de
    /// utilização hidrossanitário. Projetado para alimentar o dropdown da WPF
    /// sem que a Presentation conheça Autodesk.Revit.*.
    /// </summary>
    public sealed class FamilyTypeOptionDto
    {
        public FamilyTypeOptionDto(
            long elementId,
            string uniqueId,
            string familyName,
            string typeName,
            string? categoryName)
        {
            ElementId = elementId;
            UniqueId = uniqueId ?? string.Empty;
            FamilyName = familyName ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            CategoryName = categoryName;
        }

        public long ElementId { get; }
        public string UniqueId { get; }
        public string FamilyName { get; }
        public string TypeName { get; }
        public string? CategoryName { get; }

        public string DisplayName => $"{FamilyName} : {TypeName}";
    }
}
