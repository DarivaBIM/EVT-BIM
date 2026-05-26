using System.Collections.Generic;

namespace DarivaBIM.Domain.Tigre
{
    public sealed class TigreCatalogEntry
    {
        /// <summary>
        /// Ctor legado — preserva PipeCodes e tests existentes que
        /// criavam entries só com (description, diameterMm, code).
        /// Internamente delega pro ctor novo com row sintética sem
        /// ProductLine/Kind/Dn1/Dn2/Pn.
        /// </summary>
        public TigreCatalogEntry(string description, int diameterMm, int code, ISet<string> ignoreTokens)
            : this(
                new TigreRawCatalogRow
                {
                    Description = description,
                    DiameterMm = diameterMm,
                    Code = code,
                },
                ignoreTokens)
        {
        }

        public TigreCatalogEntry(TigreRawCatalogRow row, ISet<string> ignoreTokens)
        {
            DescriptionRaw = row.Description;
            Tokens = TigreTextUtils.Tokenize(row.Description);
            CoreTokens = TigreTextUtils.CoreTokens(row.Description, ignoreTokens);
            if (CoreTokens.Count == 0)
                CoreTokens = Tokens;

            // Lean: descrição sem marcadores dimensionais (DN, mm,
            // polegadas, PN, comprimento). Família Revit raramente carrega
            // esses no nome — então a comparação token-a-token do FindMatch
            // precisa de descrição enxuta no catálogo, ou nada casa.
            LeanDescription = TigreTextUtils.StripDimensions(row.Description);
            LeanTokens = TigreTextUtils.Tokenize(LeanDescription);
            LeanCoreTokens = TigreTextUtils.CoreTokens(LeanDescription, ignoreTokens);
            if (LeanCoreTokens.Count == 0)
                LeanCoreTokens = LeanTokens;

            DiameterMm = row.DiameterMm;
            Code = row.Code;
            ProductLine = row.ProductLine;
            Kind = row.Kind;
            Dn1 = row.Dn1;
            Dn2 = row.Dn2;
            Pn = row.Pn;
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

        /// <summary>Linha de produto (SR, SN, REDUX, Soldável, ...).</summary>
        public string? ProductLine { get; }

        /// <summary>pipe/cap/elbow/tee/reducer/fitting/valve/accessory/fixture.</summary>
        public string? Kind { get; }

        /// <summary>Diâmetro nominal principal em mm.</summary>
        public int? Dn1 { get; }

        /// <summary>Diâmetro nominal secundário em mm (reduções e tês de redução).</summary>
        public int? Dn2 { get; }

        /// <summary>Classe de pressão PPR ("12.5", "20", "25").</summary>
        public string? Pn { get; }
    }
}
