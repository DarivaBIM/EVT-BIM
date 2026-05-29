using System;
using System.Collections.Generic;
using DarivaBIM.Domain.Mep.Classification.Ports;

namespace DarivaBIM.Domain.Mep.Classification.Connections.Rules
{
    /// <summary>
    /// Restricao dimensional port-based (secao 12.2): os ports em <see cref="Ports"/>
    /// devem satisfazer <see cref="Relation"/>, opcionalmente contra <see cref="Target"/>
    /// (um PortRole ou valor numerico fixo). Ports vazio = aplica a todos — usado
    /// pela expansao do shortcut string (ex.: "equal").
    /// </summary>
    public sealed record DiameterConstraint
    {
        public IReadOnlyList<PortRole> Ports { get; init; } = Array.Empty<PortRole>();

        public DiameterRelation Relation { get; init; }

        public string? Target { get; init; }
    }
}
