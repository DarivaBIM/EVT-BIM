using System;
using System.Collections.Generic;

namespace DarivaBIM.Application.DTOs.Tigre
{
    /// <summary>
    /// Linha agrupada exibida em uma das quatro caixinhas da janela
    /// "Codificar Tubos". Agrega tubos de mesmo Tipo + Diâmetro que
    /// compartilham o mesmo <see cref="Status"/> — cada combinação
    /// (TipoNome, Diâmetro, Status) vira uma linha distinta.
    /// </summary>
    public sealed class TigreScanGroup
    {
        public TigreScanGroup(
            string typeName,
            int? diameterMm,
            TigrePipeStatus status,
            IReadOnlyList<long> elementIds,
            int? matchedCode)
        {
            TypeName = typeName ?? string.Empty;
            DiameterMm = diameterMm;
            Status = status;
            ElementIds = elementIds ?? Array.Empty<long>();
            MatchedCode = matchedCode;
        }

        public string TypeName { get; }

        /// <summary>
        /// Diâmetro nominal em milímetros, arredondado. <c>null</c> quando o
        /// tubo não expõe diâmetro (vai para o estado <see cref="TigrePipeStatus.NoMatch"/>).
        /// </summary>
        public int? DiameterMm { get; }

        public TigrePipeStatus Status { get; }

        public IReadOnlyList<long> ElementIds { get; }

        /// <summary>
        /// Código do catálogo Tigre que casaria com este grupo. <c>null</c>
        /// quando <see cref="Status"/> é <see cref="TigrePipeStatus.NoMatch"/>.
        /// </summary>
        public int? MatchedCode { get; }

        public int Count => ElementIds.Count;
    }
}
