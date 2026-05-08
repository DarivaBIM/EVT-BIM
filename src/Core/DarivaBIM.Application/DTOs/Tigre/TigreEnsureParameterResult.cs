using System;
using System.Collections.Generic;

namespace DarivaBIM.Application.DTOs.Tigre
{
    /// <summary>
    /// Relatório da criação/garantia do shared parameter Tigre: Código nas
    /// tubulações do projeto. <see cref="Action"/> descreve o que foi feito
    /// (criado, mantido, convertido) e <see cref="Warnings"/> traz os avisos
    /// de reaproveitamento — nome igual com GUID diferente, etc.
    /// </summary>
    public sealed class TigreEnsureParameterResult
    {
        public string Action { get; init; } = string.Empty;

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        public string? ErrorMessage { get; init; }
    }
}
