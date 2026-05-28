namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Categoria de reducao quando ports de uma peca tem DNs diferentes. Domain
    /// pure: o classifier do Adapter Revit popula a partir da geometria; o
    /// matcher consome para resolver Reducer/Tee-Reducer. Vide secao 6 do
    /// rulebook canonico.
    /// </summary>
    public enum ReductionKind
    {
        None = 0,
        Concentric = 1,
        Eccentric = 2,
        BranchOnly = 3,
    }
}
