using System.Numerics;
using DarivaBIM.Domain.Mep.Classification.Ports;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Snapshot Revit-agnostic de um conector fisico, consumido pelo
    /// <see cref="TopologyInferenceEngine"/>. O Adapter (fase 1.B-2) converte
    /// cada Autodesk.Revit.DB.Connector nisto na fronteira de camada, para que o
    /// motor de inferencia geometrica viva no Domain puro (sobre Vector3) e seja
    /// testavel headless. Vide secoes 6 e 9 do rulebook canonico.
    /// </summary>
    public sealed record ConnectorReading
    {
        /// <summary>
        /// Normal apontando para FORA da peca (= Connector.BasisZ outward). NAO e
        /// a direcao de fluxo: dois conectores de uma peca reta tem OutwardNormal
        /// ANTI-paralelos (~180 graus entre si). O motor usa isto para a matriz de
        /// angulos. Espera-se unitario; o motor normaliza defensivamente.
        /// </summary>
        public required Vector3 OutwardNormal { get; init; }

        /// <summary>Origem do conector em milimetros (conversao feita no Adapter).</summary>
        public required Vector3 Origin { get; init; }

        /// <summary>Diametro nominal arredondado em milimetros.</summary>
        public required int DnMm { get; init; }

        /// <summary>
        /// Indice nativo estavel do conector no ConnectorManager. Existe para
        /// tornar a atribuicao de PortRole determinista (ordenacao por NativeIndex
        /// em empates), evitando que a ordem de iteracao do Revit vaze para o
        /// resultado. Pedido pelo Codex (C2).
        /// </summary>
        public required int NativeIndex { get; init; }

        /// <summary>Formato da secao; hidraulica e Round por padrao.</summary>
        public ConnectorShape Shape { get; init; } = ConnectorShape.Round;

        /// <summary>
        /// Se o conector tem algo conectado. Carregado para diagnostico (C2); o
        /// motor 1.B-1 nao filtra por isto (o filtro fisico e do Adapter 1.B-2).
        /// </summary>
        public bool IsConnected { get; init; }
    }
}
