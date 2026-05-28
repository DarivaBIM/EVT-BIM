using System.Numerics;

namespace DarivaBIM.Domain.Mep.Classification.Ports
{
    /// <summary>
    /// POCO imutavel descrevendo uma porta de conexao de peca MEP. Usa
    /// <see cref="Vector3"/> de System.Numerics em vez de Autodesk.Revit.DB.XYZ
    /// porque Domain e core-agnostic (ADR-0001); o adapter Revit converte
    /// XYZ -> Vector3 na fronteira. Vide secao 6 do rulebook canonico.
    /// </summary>
    public sealed record MepPort
    {
        public required PortRole Role { get; init; }

        public required int DnMm { get; init; }

        public required Vector3 Direction { get; init; }

        public required Vector3 Origin { get; init; }

        public ConnectorShape Shape { get; init; } = ConnectorShape.Round;
    }
}
