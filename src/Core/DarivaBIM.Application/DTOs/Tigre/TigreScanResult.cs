using System;
using System.Collections.Generic;

namespace DarivaBIM.Application.DTOs.Tigre
{
    /// <summary>
    /// Snapshot de leitura do projeto: quantos itens há no catálogo, quantos
    /// tubos existem, em quantos o shared parameter Tigre: Código está
    /// acessível e a lista agrupada por (Tipo, Diâmetro, Status). Não altera
    /// o documento — é só a base para a janela "Codificar Tubos" tomar
    /// decisões.
    /// </summary>
    public sealed class TigreScanResult
    {
        public int CatalogCount { get; init; }

        public int PipesTotal { get; init; }

        /// <summary>
        /// Tubos cujo parâmetro Tigre: Código está acessível para escrita
        /// (binding existe no projeto e o tubo o expõe).
        /// </summary>
        public int PipesWithParameter { get; init; }

        /// <summary>
        /// Tubos sem o parâmetro acessível — o usuário precisa rodar
        /// "Criar parâmetro 'Tigre: Código' nos tubos" antes de qualquer
        /// inserção/atualização.
        /// </summary>
        public int PipesWithoutParameter { get; init; }

        /// <summary>
        /// <c>true</c> quando o shared parameter Tigre: Código já está
        /// vinculado às tubulações do projeto.
        /// </summary>
        public bool ParameterIsBound { get; init; }

        public IReadOnlyList<TigreScanGroup> Groups { get; init; } = Array.Empty<TigreScanGroup>();

        /// <summary>
        /// Mensagem de erro fatal (ex.: catálogo vazio). Quando preenchida, a
        /// UI mostra o erro e ignora as listas.
        /// </summary>
        public string? ErrorMessage { get; init; }
    }
}
