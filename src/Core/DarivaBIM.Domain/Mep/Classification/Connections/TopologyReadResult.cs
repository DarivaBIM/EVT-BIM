using System;
using System.Collections.Generic;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Wrapper retornado pelo Adapter Revit em vez de jogar excecao: erros de
    /// leitura viram diagnostics estruturados e Topology vira null em vez de
    /// crashar o pipeline de classificacao. Vide secao 8 do rulebook canonico.
    /// </summary>
    public sealed record TopologyReadResult
    {
        public bool Success { get; init; }

        public ConnectionTopology? Topology { get; init; }

        public IReadOnlyList<TopologyDiagnostic> Diagnostics { get; init; }
            = Array.Empty<TopologyDiagnostic>();
    }
}
