using System;
using System.Collections.Generic;
using System.Linq;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Domain.Tigre;

namespace DarivaBIM.Plugin.Ui.Models
{
    /// <summary>
    /// Catálogo estático dos 14 sistemas hidráulicos prediais que a janela de
    /// Importar Famílias filtra. A ordem aqui é a ordem em que os chips
    /// aparecem na barra de filtros — fixa e independente da existência de
    /// famílias na categoria. Vasco (água fria) sempre vem antes de bombas,
    /// mesmo quando 0 famílias do catálogo casam com bombas.
    ///
    /// Hex colors espelham os tokens 2.2 da SPEC-WPF.md e os mesmos hex que
    /// <c>TagFilterOption.ResolvePalette</c> e <c>FamilyItem.CreateTagBadge</c>
    /// usavam previamente — manter sincronia evita "chip azul + badge cinza"
    /// para a mesma família.
    /// </summary>
    public static class SistemaCatalog
    {
        public static IReadOnlyList<Sistema> All { get; } = BuildCatalog();

        private static readonly Dictionary<string, Sistema> ById =
            All.ToDictionary(s => s.Id, StringComparer.Ordinal);

        public static Sistema? FindById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return ById.TryGetValue(id, out Sistema? sistema) ? sistema : null;
        }

        /// <summary>
        /// Resolve as tags de uma família para a lista de sistemas a que ela
        /// pertence. Uma família pode pertencer a múltiplos sistemas (ex.: um
        /// joelho pode aparecer tanto em "Água Fria" quanto em "Água Quente"
        /// se os dois sinônimos casarem com tags da família). Retorna uma
        /// lista distinta preservando a ordem do catálogo.
        /// </summary>
        public static IReadOnlyList<string> ResolveSistemaIds(IEnumerable<FamilyTag>? tags)
        {
            if (tags == null)
            {
                return Array.Empty<string>();
            }

            // Snapshot normalizado das tags da família, calculado uma vez para
            // não pagar normalização N×M (N tags × M sistemas × K sinônimos).
            List<string> normalizedTags = tags
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Description))
                .Select(t => Normalize(t.Description))
                .Where(s => s.Length > 0)
                .ToList();

            if (normalizedTags.Count == 0)
            {
                return Array.Empty<string>();
            }

            List<string> matched = new List<string>(2);

            foreach (Sistema sistema in All)
            {
                if (MatchesAnySynonym(sistema, normalizedTags))
                {
                    matched.Add(sistema.Id);
                }
            }

            return matched;
        }

        // Match bidirecional substring: "agua fria" casa com tag "agua fria
        // - tigre", e tag "agua" casa com sinônimo "agua fria"? — não, só
        // o primeiro caso. Para evitar matches falsos amplos (ex.: tag
        // "agua" casando com "agua quente" também), o critério é apenas
        // tag.Contains(synonym) — sinônimo dentro da tag.
        private static bool MatchesAnySynonym(Sistema sistema, IReadOnlyList<string> normalizedTags)
        {
            for (int s = 0; s < sistema.Synonyms.Count; s++)
            {
                string synonym = sistema.Synonyms[s];
                if (synonym.Length == 0)
                {
                    continue;
                }

                for (int t = 0; t < normalizedTags.Count; t++)
                {
                    if (normalizedTags[t].Contains(synonym, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IReadOnlyList<Sistema> BuildCatalog()
        {
            return new[]
            {
                new Sistema
                {
                    Id = "agua-fria",
                    Label = "Água Fria",
                    IconFileName = "agua_fria.png",
                    ColorHex = "#1565C0",
                    BgHex = "#E8F0FA",
                    Synonyms = new[] { "agua fria" },
                },
                new Sistema
                {
                    Id = "agua-quente",
                    Label = "Água Quente",
                    IconFileName = "agua_quente.png",
                    ColorHex = "#D84343",
                    BgHex = "#FBE8E8",
                    Synonyms = new[] { "agua quente" },
                },
                new Sistema
                {
                    Id = "pluvial",
                    Label = "Pluvial",
                    IconFileName = "pluvial.png",
                    ColorHex = "#5E60CE",
                    BgHex = "#ECEDFA",
                    Synonyms = new[] { "pluvial" },
                },
                new Sistema
                {
                    Id = "esgoto",
                    Label = "Esgoto",
                    IconFileName = "esgoto.png",
                    ColorHex = "#2E7D32",
                    BgHex = "#E6F1E7",
                    // "esgoto" sozinho casa com "esgoto sanitario", "tratamento de esgoto",
                    // "ramal de esgoto" etc. — comportamento desejado, todos são esgoto.
                    Synonyms = new[] { "esgoto" },
                },
                new Sistema
                {
                    Id = "incendio",
                    Label = "Combate a Incêndio",
                    IconFileName = "combate_a_incendio.png",
                    ColorHex = "#B71C1C",
                    BgHex = "#FAE3E3",
                    Synonyms = new[] { "incendio", "hidrante", "sprinkler" },
                },
                new Sistema
                {
                    Id = "piscina",
                    Label = "Piscina",
                    IconFileName = "piscina.png",
                    ColorHex = "#039BE5",
                    BgHex = "#E3F3FB",
                    Synonyms = new[] { "piscina" },
                },
                new Sistema
                {
                    Id = "irrigacao",
                    Label = "Irrigação",
                    IconFileName = "irrigacao.png",
                    ColorHex = "#6B8E23",
                    BgHex = "#EEF2E3",
                    Synonyms = new[] { "irrigacao" },
                },
                new Sistema
                {
                    Id = "reservatorio",
                    Label = "Reservatório",
                    IconFileName = "reservatorio.png",
                    ColorHex = "#0E7490",
                    BgHex = "#E0F0F3",
                    Synonyms = new[] { "reservatorio", "caixa de agua", "caixa d agua", "caixa dagua" },
                },
                new Sistema
                {
                    Id = "bombas",
                    Label = "Bombas",
                    IconFileName = "bombas.png",
                    ColorHex = "#EF6C00",
                    BgHex = "#FBEEDD",
                    Synonyms = new[] { "bomba" },
                },
                new Sistema
                {
                    Id = "valvula",
                    Label = "Válvula",
                    IconFileName = "valvula.png",
                    ColorHex = "#00796B",
                    BgHex = "#DFF0EE",
                    Synonyms = new[] { "valvula", "registro" },
                },
                new Sistema
                {
                    Id = "caixas-ralos",
                    Label = "Caixas e Ralos",
                    IconFileName = "caixas_e_ralos.png",
                    ColorHex = "#546E7A",
                    BgHex = "#E7ECEF",
                    Synonyms = new[] { "caixa", "ralo" },
                },
                new Sistema
                {
                    Id = "tratamento",
                    Label = "Tratamento de Esgoto",
                    IconFileName = "tratamento_de_esgoto.png",
                    ColorHex = "#6D4C41",
                    BgHex = "#EFE8E3",
                    Synonyms = new[] { "tratamento de esgoto", "fossa", "filtro anaerobio" },
                },
                new Sistema
                {
                    Id = "poco",
                    Label = "Poço",
                    IconFileName = "poco.png",
                    ColorHex = "#C88719",
                    BgHex = "#F8EFDC",
                    Synonyms = new[] { "poco" },
                },
                new Sistema
                {
                    Id = "ponto-util",
                    Label = "Ponto de Utilização",
                    IconFileName = "ponto_de_utilizacao.png",
                    ColorHex = "#616161",
                    BgHex = "#ECECEC",
                    Synonyms = new[] { "ponto de utilizacao", "utilitario" },
                },
            };
        }

        private static string Normalize(string value) => TigreTextUtils.NormalizeForSearch(value);
    }
}
