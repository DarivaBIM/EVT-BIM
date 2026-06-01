using System;
using System.Collections.Generic;
using System.Linq;

namespace DarivaBIM.Application.DTOs.Tigre
{
    /// <summary>
    /// Snapshot de leitura do projeto: quantos itens há no catálogo, quantos
    /// elementos Tigre existem nas 4 categorias relevantes (Pipes + Conexões
    /// + Acessórios + Aparelhos), em quantos o shared parameter Tigre:
    /// Código está acessível e a lista agrupada por (Categoria, Tipo,
    /// Diâmetro, Status). Não altera o documento — é só a base para a
    /// janela "Codificar Tigre" tomar decisões.
    /// </summary>
    public sealed class TigreScanResult
    {
        public int CatalogCount { get; init; }

        // Slice 3 — generalizado pra cobrir todas as 4 categorias Tigre.
        // Substituem os antigos PipesTotal/WithParameter/WithoutParameter.

        /// <summary>Elementos Tigre detectados (4 categorias).</summary>
        public int ElementsTotal { get; init; }

        /// <summary>Elementos cujo parâmetro Tigre: Código está acessível.</summary>
        public int ElementsWithParameter { get; init; }

        /// <summary>Elementos sem o parâmetro acessível.</summary>
        public int ElementsWithoutParameter { get; init; }

        /// <summary>
        /// Binding do parâmetro Tigre: Código por categoria. True quando
        /// TODOS os elementos Tigre da categoria têm o parâmetro
        /// acessível (instance binding global pra Pipes; type binding
        /// das famílias catálogo pra fittings/accessories/fixtures).
        /// </summary>
        public IReadOnlyDictionary<string, bool> BindingAvailable { get; init; }
            = new Dictionary<string, bool>();

        /// <summary>
        /// Estatísticas por categoria (Total/WithParameter/WithoutParameter/
        /// MatchedByCatalog). Útil pra UI mostrar header por subgrupo.
        /// </summary>
        public IReadOnlyDictionary<string, CategoryStats> ByCategoryStats { get; init; }
            = new Dictionary<string, CategoryStats>();

        public IReadOnlyList<TigreScanGroup> Groups { get; init; } = Array.Empty<TigreScanGroup>();

        /// <summary>
        /// Mensagem de erro fatal (ex.: catálogo vazio). Quando preenchida,
        /// a UI mostra o erro e ignora as listas.
        /// </summary>
        public string? ErrorMessage { get; init; }

        // ─────────────────────────────────────────────────────────────
        // Aliases legados (Slice 1.5 → Slice 3 transition).
        // Mantém ViewModels Pipe-only funcionais até o XAML migrar
        // pros nomes generalizados. Removidos quando todos consumers
        // migrarem ou no fim do Slice 3.
        // ─────────────────────────────────────────────────────────────

        /// <summary>Alias legado de <see cref="ElementsTotal"/>.</summary>
        public int PipesTotal => ElementsTotal;

        /// <summary>Alias legado de <see cref="ElementsWithParameter"/>.</summary>
        public int PipesWithParameter => ElementsWithParameter;

        /// <summary>Alias legado de <see cref="ElementsWithoutParameter"/>.</summary>
        public int PipesWithoutParameter => ElementsWithoutParameter;

        /// <summary>
        /// Alias legado de <see cref="BindingAvailable"/>. True se
        /// TODAS as categorias têm binding completo. Quando o
        /// dicionário está vazio (nenhum elemento Tigre no projeto),
        /// também retorna true — coerente com "nada a fazer".
        /// </summary>
        public bool ParameterIsBound =>
            BindingAvailable.Count == 0 ||
            BindingAvailable.Values.All(v => v);
    }
}
