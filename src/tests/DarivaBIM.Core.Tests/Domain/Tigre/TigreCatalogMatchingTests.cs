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
            // Descrição com ç/ã ("Conexão Transição ClicPEX AQxPEX") deve
            // casar mesmo que o Normalize remova acentos antes do compare —
            // input e entry passam pela mesma normalização. Uso descrição
            // única (DN 16 só tem uma Conexão Transição) pra evitar empate
            // do AmbiguityGuard.
            // diameterMm da entry "Conexão Transição ClicPEX AQxPEX 15x16mm"
            // é 15 (dn1), não 16 — primeiro número da redução.
            TigreCatalogEntry? entry = Find("Conexão Transição ClicPEX AQxPEX", 15);

            Assert.NotNull(entry);
            Assert.Contains("Conex", entry!.DescriptionRaw);
            Assert.Contains("ClicPEX", entry.DescriptionRaw);
        }

        // ─────────────────────────────────────────────────────────────
        // 2A.4 — Strip de polegada robusto + AmbiguityGuard
        // ─────────────────────────────────────────────────────────────

        [Theory]
        // mm × polegada fracionária com aspa reta
        [InlineData("Bucha Soldável 25x3/4\"", "Bucha Soldável")]
        [InlineData("Conector 75x2.1/2\"", "Conector")]
        [InlineData("Adaptador 22x1/2\"", "Adaptador")]
        // polegada fracionária solo
        [InlineData("Tubo CPVC TIGREFire 3/4'", "Tubo CPVC TIGREFire")]
        [InlineData("Conector AQUATHERM 1.1/4\"", "Conector AQUATHERM")]
        [InlineData("Registro 1.1/2'", "Registro")]
        // polegada × polegada (aspa no meio)
        [InlineData("Tê Redução TIGREFire 2.1/2'x2'", "Tê Redução TIGREFire")]
        [InlineData("Bucha Redução 1.1/4\"x3/4\"", "Bucha Redução")]
        // aspas Unicode curvas (copy/paste de Word)
        [InlineData("Tubo CPVC TIGREFire 3/4’", "Tubo CPVC TIGREFire")]
        [InlineData("Adaptador 22x1/2”", "Adaptador")]
        // prime e double prime
        [InlineData("Tubo 1′", "Tubo")]
        [InlineData("Conector 2.1/2″", "Conector")]
        public void StripDimensions_handles_inch_variants(
            string input, string expectedLean)
        {
            string actual = TigreTextUtils.StripDimensions(input);
            Assert.Equal(expectedLean, actual);
        }

        [Fact]
        public void AmbiguousMatch_returns_null_when_lean_tokens_tied()
        {
            // PPR PN12.5/PN20/PN25 50mm tinham lean idêntica "Tubo PPR"
            // e diameter 50 — pre-fix, FindMatch retornava o primeiro
            // ordering, gravando SKU arbitrário. AmbiguityGuard agora
            // detecta empate e devolve null.
            List<TigreRawCatalogRow> rows = new()
            {
                new TigreRawCatalogRow
                {
                    Description = "Tubo PPR PN 12.5 50mm - 3m",
                    DiameterMm = 50, Code = 17010603,
                },
                new TigreRawCatalogRow
                {
                    Description = "Tubo PPR PN 20 50mm - 3m",
                    DiameterMm = 50, Code = 17010107,
                },
                new TigreRawCatalogRow
                {
                    Description = "Tubo PPR PN 25 50mm - 3m",
                    DiameterMm = 50, Code = 17010409,
                },
            };
            TigreCatalog cat = new TigreCatalog(rows);

            TigreCatalogEntry? entry = cat.FindMatch(
                descriptionText: "Tubo PPR",
                segmentText: string.Empty,
                typeNameText: string.Empty,
                combinedText: "Tubo PPR 50",
                diameterMmRound: 50);

            Assert.Null(entry);
        }

        [Fact]
        public void DisambiguationCase_prefers_more_specific_entry()
        {
            // "Tê" vs "Tê Redução" com query "Tê Redução": ambos passam
            // ContainsAllTokens (Tê é subset de "Tê Redução"), mas o
            // tie-break por LeanCoreTokens.Count escolhe a mais específica.
            List<TigreRawCatalogRow> rows = new()
            {
                new TigreRawCatalogRow
                {
                    Description = "Tê TIGREFire 2.1/2'",
                    DiameterMm = 64, Code = 22891332,
                },
                new TigreRawCatalogRow
                {
                    Description = "Tê Redução TIGREFire 2.1/2'x2'",
                    DiameterMm = 64, Code = 22891642,
                },
            };
            TigreCatalog cat = new TigreCatalog(rows);

            TigreCatalogEntry? specific = cat.FindMatch(
                descriptionText: "Tê Redução TIGREFire",
                segmentText: string.Empty,
                typeNameText: string.Empty,
                combinedText: "Tê Redução TIGREFire 64",
                diameterMmRound: 64);
            Assert.NotNull(specific);
            Assert.Equal(22891642, specific!.Code);

            // Query "Tê TIGREFire" SEM "Redução": "Tê Redução" entry tem
            // token "redução" que não está na query, então ela NÃO casa.
            // Só "Tê TIGREFire" casa → match único.
            TigreCatalogEntry? plain = cat.FindMatch(
                descriptionText: "Tê TIGREFire",
                segmentText: string.Empty,
                typeNameText: string.Empty,
                combinedText: "Tê TIGREFire 64",
                diameterMmRound: 64);
            Assert.NotNull(plain);
            Assert.Equal(22891332, plain!.Code);
        }
    }
}
