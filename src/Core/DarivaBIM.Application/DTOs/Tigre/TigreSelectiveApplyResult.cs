using System;
using System.Collections.Generic;

namespace DarivaBIM.Application.DTOs.Tigre
{
    /// <summary>
    /// Relatório da operação "Inserir/Atualizar Códigos" sobre os elementos
    /// Tigre selecionados pelo usuário no WPF. As contagens são sempre
    /// relativas à seleção, exceto <see cref="CatalogCount"/> e
    /// <see cref="ElementsTotalInProject"/>, que dão contexto.
    /// </summary>
    public sealed class TigreSelectiveApplyResult
    {
        public int CatalogCount { get; init; }

        /// <summary>
        /// Total de elementos Tigre no projeto (4 categorias). Slice 3 —
        /// substitui o antigo <see cref="PipesTotalInProject"/>.
        /// </summary>
        public int ElementsTotalInProject { get; init; }

        /// <summary>Total de elementos marcados pelo usuário no WPF.</summary>
        public int Selected { get; init; }

        /// <summary>Elementos que tiveram o parâmetro recém-preenchido.</summary>
        public int Inserted { get; init; }

        /// <summary>Elementos cujo código pré-existente foi sobrescrito.</summary>
        public int Overwritten { get; init; }

        /// <summary>Elementos que já estavam com o código correto e ficaram intactos.</summary>
        public int AlreadyOk { get; init; }

        /// <summary>Elementos sem correspondência no catálogo (foram ignorados).</summary>
        public int NoMatch { get; init; }

        /// <summary>
        /// Elementos sem parâmetro acessível para escrita (nem no instance
        /// nem no type da família). Veja <see cref="Issues"/> pra detalhes
        /// individuais.
        /// </summary>
        public int ParameterIssue { get; init; }

        /// <summary>
        /// Lista detalhada de elementos que caíram em ParameterIssue —
        /// permite UI mostrar quais famílias precisam ser preparadas com
        /// o parâmetro Tigre: Código. Slice 3.3.
        /// </summary>
        public IReadOnlyList<TigreApplyIssue> Issues { get; init; }
            = Array.Empty<TigreApplyIssue>();

        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Alias legado de <see cref="ElementsTotalInProject"/> — preserva
        /// ViewModels Pipe-only do Slice 1.5. Remove quando UI migrar.
        /// </summary>
        public int PipesTotalInProject => ElementsTotalInProject;
    }
}
