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

            DiameterMm = diameterMm;
            Code = code;
        }

        public string DescriptionRaw { get; }
        public IReadOnlyList<string> Tokens { get; }
        public IReadOnlyList<string> CoreTokens { get; }
        public int DiameterMm { get; }
        public int Code { get; }
    }
}
