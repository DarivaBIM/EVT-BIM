using System;
using System.Collections.Generic;

namespace DarivaBIM.Application.DTOs.Tigre
{
    /// <summary>
    /// Linha agrupada exibida em uma das quatro caixinhas da janela
    /// "Codificar Tigre". Agrega elementos do mesmo (Categoria, Tipo,
    /// Diâmetro) que compartilham o mesmo <see cref="Status"/> — cada
    /// combinação vira uma linha distinta. Slice 3 ampliou a chave de
    /// (TipoNome, Diâmetro, Status) pra (CategoryName, Kind, TipoNome,
    /// Diâmetro, Status) cobrindo Pipes + Conexões + Acessórios +
    /// Aparelhos.
    /// </summary>
    public sealed class TigreScanGroup
    {
        public TigreScanGroup(
            string categoryName,
            string kind,
            string typeName,
            int? diameterMm,
            TigrePipeStatus status,
            IReadOnlyList<long> elementIds,
            int? matchedCode)
        {
            CategoryName = categoryName ?? string.Empty;
            Kind = kind ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            DiameterMm = diameterMm;
            Status = status;
            ElementIds = elementIds ?? Array.Empty<long>();
            MatchedCode = matchedCode;
        }

        /// <summary>
        /// Nome da categoria Revit ("Tubulações", "Conexões de tubo",
        /// "Acessórios de tubulação", "Aparelhos hidrossanitários" em
        /// pt-BR). Usado pelo XAML pra agrupar via CollectionViewSource.
        /// </summary>
        public string CategoryName { get; }

        /// <summary>
        /// Kind do match catálogo (pipe/fitting/accessory/fixture).
        /// Mesmo valor passado no kindFilter do FindMatch.
        /// </summary>
        public string Kind { get; }

        public string TypeName { get; }

        /// <summary>
        /// Diâmetro nominal em milímetros, arredondado. <c>null</c> quando
        /// o elemento não expõe diâmetro (vai para <see cref="TigrePipeStatus.NoMatch"/>).
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
