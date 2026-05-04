using System.Linq;
using DarivaBIM.Domain.Tigre;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Tigre
{
    public class TigreCatalogTests
    {
        [Fact]
        public void Catalog_built_from_fallback_rows_finds_serie_normal_50mm()
        {
            var catalog = new TigreCatalog(TigreFallbackCatalogRows.All());
            var entry = catalog.FindMatch(
                descriptionText: "Tubo Série Normal",
                segmentText: string.Empty,
                typeNameText: string.Empty,
                combinedText: "Tubo Série Normal 50",
                diameterMmRound: 50);

            Assert.NotNull(entry);
            Assert.Equal(50, entry!.DiameterMm);
            Assert.Equal(11030602, entry.Code);
        }

        [Fact]
        public void Catalog_returns_null_for_unknown_diameter()
        {
            var catalog = new TigreCatalog(TigreFallbackCatalogRows.All());
            var entry = catalog.FindMatch("anything", "", "", "anything", diameterMmRound: 9999);
            Assert.Null(entry);
        }

        [Fact]
        public void Entries_are_ordered_by_specificity()
        {
            var catalog = new TigreCatalog(TigreFallbackCatalogRows.All());
            // Entries with more core tokens come first.
            int firstCore = catalog.Entries.First().CoreTokens.Count;
            int lastCore = catalog.Entries.Last().CoreTokens.Count;
            Assert.True(firstCore >= lastCore);
        }
    }
}
