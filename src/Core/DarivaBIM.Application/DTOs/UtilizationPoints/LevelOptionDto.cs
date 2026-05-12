namespace DarivaBIM.Application.DTOs.UtilizationPoints
{
    /// <summary>
    /// Nível Revit em formato neutro para alimentar o dropdown "Nível de
    /// referência" da janela WPF. <c>ElementId == null</c> representa a opção
    /// "Usar nível do elemento" (default), e <c>ElementId == 0</c> representa
    /// "Zero absoluto do projeto", honrando a tríade de fallback do
    /// algoritmo Python.
    /// </summary>
    public sealed class LevelOptionDto
    {
        public LevelOptionDto(long? elementId, string name, double elevationMeters)
        {
            ElementId = elementId;
            Name = name ?? string.Empty;
            ElevationMeters = elevationMeters;
        }

        public long? ElementId { get; }
        public string Name { get; }
        public double ElevationMeters { get; }
    }
}
