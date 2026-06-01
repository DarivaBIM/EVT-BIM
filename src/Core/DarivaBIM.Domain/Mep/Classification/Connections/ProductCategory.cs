namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Categoria do produto independente de disciplina. Mapeia BuiltInCategory
    /// Revit em conceito de Domain. Fixture e Consumable ficam fora do MVP 1
    /// (rulebooks proprios) mas o enum ja antecipa o vocabulario.
    /// </summary>
    public enum ProductCategory
    {
        Unknown = 0,
        PipeFitting = 1,
        PipeAccessory = 2,
        PlumbingFixture = 3,
        Support = 4,
        Consumable = 5,
    }
}
