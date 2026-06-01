namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Tipo do filtro (Y, inline ou cesta). Filtro pode aparecer como
    /// <see cref="BaseKind.Valve"/> generico, mas exige refinamento para
    /// perda de carga e classificacao no catalogo. Vide secao 14 do
    /// rulebook canonico.
    /// </summary>
    public enum FilterKind
    {
        Unknown = 0,
        YStrainer = 1,
        InlineFilter = 2,
        BasketFilter = 3,
    }
}
