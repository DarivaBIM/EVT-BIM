namespace DarivaBIM.Application.DTOs.Tigre
{
    /// <summary>
    /// Estatísticas por categoria do scan Codificar Tigre. Substitui o
    /// counter flat <c>PipesTotal/WithParameter/WithoutParameter</c> que
    /// servia só pra Pipes — agora cada categoria das 4 (Pipes,
    /// Conexões, Acessórios, Aparelhos) tem o próprio dump.
    /// </summary>
    public sealed class CategoryStats
    {
        public int Total { get; init; }
        public int WithParameter { get; init; }
        public int WithoutParameter { get; init; }

        /// <summary>
        /// Subset de <see cref="Total"/> que casou contra o catálogo
        /// Tigre (descrição + diâmetro + kind). Útil pra mostrar "X de
        /// Y reconhecidos" no header de cada categoria na UI.
        /// </summary>
        public int MatchedByCatalog { get; init; }
    }
}
