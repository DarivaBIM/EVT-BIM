using System.Collections.Generic;

namespace DarivaBIM.Plugin.Ui.Models
{
    /// <summary>
    /// Catálogo canônico de um sistema hidráulico (água fria, esgoto, pluvial,
    /// bombas...). É a unidade de filtragem e classificação visual da aba de
    /// importação de famílias: cada chip da barra de filtros corresponde a
    /// exatamente um <see cref="Sistema"/>, e cada família carregada da API é
    /// resolvida para zero, um ou mais sistemas via correspondência de tags.
    ///
    /// Sem WPF aqui de propósito — mantém o catálogo testável e reaproveitável
    /// fora do plugin. A view-model do chip (TagFilterOption) é quem materializa
    /// brush e BitmapImage a partir destes dados.
    /// </summary>
    public sealed class Sistema
    {
        public required string Id { get; init; }
        public required string Label { get; init; }

        /// <summary>
        /// Nome do arquivo PNG do ícone, dentro de
        /// <c>Resources/FilterIcons/</c>. Carregado em runtime via
        /// <c>Assembly.Location</c>; ausente no disco vira fallback Segoe MDL2.
        /// </summary>
        public required string IconFileName { get; init; }

        /// <summary>Cor de acento do sistema (#RRGGBB). Borda do chip checked, badge.</summary>
        public required string ColorHex { get; init; }

        /// <summary>Pastel de fundo do chip e do badge mini no card (#RRGGBB).</summary>
        public required string BgHex { get; init; }

        /// <summary>
        /// Sinônimos normalizados (lowercase, sem acentos, sem hífens) usados
        /// para casar tags da API ao sistema. A correspondência é feita
        /// substring-aware: uma tag "incêndio - sprinkler" casa com qualquer
        /// sinônimo cuja forma normalizada esteja contida na tag, e vice-versa.
        /// </summary>
        public required IReadOnlyList<string> Synonyms { get; init; }
    }
}
