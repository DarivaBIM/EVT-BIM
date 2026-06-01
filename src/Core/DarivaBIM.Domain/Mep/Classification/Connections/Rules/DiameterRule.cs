using System;
using System.Collections.Generic;

namespace DarivaBIM.Domain.Mep.Classification.Connections.Rules
{
    /// <summary>
    /// Regra dimensional de uma topologia (secao 12.2). Mode default "roles"
    /// (constraints port-based). No JSON aceita tambem o SHORTCUT string
    /// ("equal"/"different"/...) que o loader expande para esta forma canonica
    /// (um unico constraint com Ports vazio e a relacao informada).
    /// </summary>
    public sealed record DiameterRule
    {
        public string Mode { get; init; } = "roles";

        public IReadOnlyList<DiameterConstraint> Constraints { get; init; } = Array.Empty<DiameterConstraint>();
    }
}
