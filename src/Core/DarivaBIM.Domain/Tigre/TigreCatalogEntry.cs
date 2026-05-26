using System.Collections.Generic;

namespace DarivaBIM.Domain.Tigre
{
    public sealed class TigreCatalogEntry
    {
        public TigreCatalogEntry(string description, int diameterMm, int code, ISet<string> ignoreTokens)
        {
            DescriptionRaw = description;
            Tokens = TigreTextUtils.Tokenize(description);
            CoreTokens = TigreTextUtils.CoreTokens(description, ignoreTokens);
            if (CoreTokens.Count == 0)
                CoreTokens = Tokens;

            // Lean: descrição sem marcadores dimensionais (DN, mm,
            // polegadas, PN, comprimento). Família Revit raramente carrega
            // esses no nome — então a comparação token-a-token do FindMatch
            // precisa de descrição enxuta no catálogo, ou nada casa.
            LeanDescription = TigreTextUtils.StripDimensions(description);
            LeanTokens = TigreTextUtils.Tokenize(LeanDescription);
            LeanCoreTokens = TigreTextUtils.CoreTokens(LeanDescription, ignoreTokens);
            if (LeanCoreTokens.Count == 0)
                LeanCoreTokens = LeanTokens;

            DiameterMm = diameterMm;
            Code = code;
        }

        public string DescriptionRaw { get; }
        public IReadOnlyList<string> Tokens { get; }
        public IReadOnlyList<string> CoreTokens { get; }

        /// <summary>
        /// Descrição sem marcadores dimensionais — preserva identificação
        /// do produto. Vide <see cref="TigreTextUtils.StripDimensions"/>.
        /// </summary>
        public string LeanDescription { get; }
        public IReadOnlyList<string> LeanTokens { get; }
        public IReadOnlyList<string> LeanCoreTokens { get; }

        public int DiameterMm { get; }
        public int Code { get; }
    }
}
