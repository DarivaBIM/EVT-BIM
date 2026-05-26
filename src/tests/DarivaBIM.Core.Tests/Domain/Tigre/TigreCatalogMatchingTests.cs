using System.Linq;
using DarivaBIM.Domain.Tigre;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Tigre
{
    /// <summary>
    /// Fixtures de matching reais — uma por linha de produto Tigre +
    /// 3 edge cases. Garante que o JSON expandido + LeanDescription
    /// continuam casando descrições típicas que aparecem em famílias
    /// Revit (sem DN/mm/comprimento no segment).
    /// </summary>
    public class TigreCatalogMatchingTests
    {
        private static readonly TigreCatalog Catalog =
            new TigreCatalog(TigreFallbackCatalogRows.All());

        private static TigreCatalogEntry? Find(string description, int diameterMm)
        {
            string combined = description + " " + diameterMm;
            return Catalog.FindMatch(
                descriptionText: description,
                segmentText: string.Empty,
                typeNameText: string.Empty,
                combinedText: combined,
                diameterMmRound: diameterMm);
        }

        [Theory]
        [InlineData("Tubo Série Reforçada", 50, 11054420)]      // SR
        [InlineData("Tubo Série Normal", 50, 11030602)]         // SN
        [InlineData("Tubo REDUX", 100, 100002789)]              // REDUX
        [InlineData("Joelho 90 Soldável", 25, 22150251)]        // Soldável
        [InlineData("Registro Esfera VS Roscável", 25, 27958320)] // Registros
        [InlineData("Tubo ClicPEX Monocamada", 16, 300000774)]  // ClicPEX
        [InlineData("Tubo AQUATHERM", 22, 17000225)]            // AQUATHERM
        [InlineData("Tubo CPVC TIGREFire", 76, 17020250)]       // TIGREFire (3")
        [InlineData("Tê PPR", 50, 22322559)]                    // PPR
        public void Catalog_finds_match_per_product_line(
            string description, int diameterMm, int expectedCode)
        {
            TigreCatalogEntry? entry = Find(description, diameterMm);

            Assert.NotNull(entry);
            Assert.Equal(expectedCode, entry!.Code);
            Assert.Equal(diameterMm, entry.DiameterMm);
        }

        [Fact]
        public void Edge_entries_without_diameter_are_filtered_out()
        {
            // "Ralo Linear Invisível 50cm" entrou no JSON com diameterMm=0
            // (cm não é um sinal de DN). O ctor do TigreCatalog filtra
            // r.DiameterMm > 0, então a entry não vira candidata pra match.
            Assert.DoesNotContain(Catalog.Entries, e => e.Code == 100018896);
        }

        [Fact]
        public void Edge_duplicate_code_across_product_lines_is_preserved()
        {
            // Code 37051209 (Anel de Borracha) aparece em SR e REDUX no
            // payload. O catálogo NÃO deduplica por code — mantém ambas
            // entries pra que cada linha tenha cobertura completa.
            int occurrences = Catalog.Entries.Count(e => e.Code == 37051209);
            Assert.True(occurrences >= 2,
                $"Esperado >= 2 entries com code 37051209, achei {occurrences}");

            TigreCatalogEntry? entry = Find("Anel de Borracha", 40);
            Assert.NotNull(entry);
            Assert.Equal(37051209, entry!.Code);
        }

        [Fact]
        public void Edge_accents_are_normalized_for_matching()
        {
            // Descrição com ç/ã ("Conexão Macho ClicPEX") deve casar
            // mesmo que o Normalize remova acentos antes do compare —
            // input e entry passam pela mesma normalização.
            TigreCatalogEntry? entry = Find("Conexão Macho ClicPEX", 25);

            Assert.NotNull(entry);
            Assert.Contains("Conex", entry!.DescriptionRaw);
            Assert.Contains("ClicPEX", entry.DescriptionRaw);
        }
    }
}
