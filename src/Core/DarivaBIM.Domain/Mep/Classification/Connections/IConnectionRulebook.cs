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

        /// <summary>
        /// Classifica SO por texto (sem geometria) — usado pelo migrador de catalogo (3.A) e
        /// como fallback do <see cref="MepClassifier"/> quando a leitura topologica falha.
        /// </summary>
        ConnectionIdentity ClassifyTextOnly(ElementTexts texts);
    }
}
