namespace DarivaBIM.Domain.Mep.Classification.Connections.Rules
{
    /// <summary>
    /// Relacao dimensional entre os DNs de um conjunto de ports (secao 12.2 do
    /// rulebook). Unknown=0 sentinel-first (padrao do modulo): default seguro que
    /// NAO vira uma constraint real silenciosa se o JSON omitir/errar "relation".
    /// </summary>
    public enum DiameterRelation
    {
        Unknown = 0,
        Equal,
        Different,
        LessThan,
        LessOrEqualThan,
        GreaterThan,
        GreaterOrEqualThan,
        Single,
        Any,
    }
}
