namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Contrato de um rulebook de conexoes por disciplina (secao 13): transforma uma
    /// leitura topologica + textos do elemento num <see cref="ConnectionIdentity"/>
    /// canonico (secao 5 / cam. 8 do secao 21). Implementacoes nunca lancam — leitura
    /// invalida ou ausencia de match degradam para uma identidade de fallback.
    /// </summary>
    public interface IConnectionRulebook
    {
        ConnectionIdentity Classify(TopologyReadResult topo, ElementTexts texts);
    }
}
