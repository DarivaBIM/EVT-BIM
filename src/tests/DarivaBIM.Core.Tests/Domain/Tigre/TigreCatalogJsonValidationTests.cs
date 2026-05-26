using System.Collections.Generic;
using System.Linq;
using DarivaBIM.Domain.Tigre;
using Xunit;
using Xunit.Abstractions;

namespace DarivaBIM.Core.Tests.Domain.Tigre
{
    /// <summary>
    /// Validações estruturais do tigre_codes.json após expansão do Slice 2A
    /// (~872 SKUs em 9 linhas). Garante que o arquivo no disco / embedded
    /// resource respeita o schema combinado entre janelas de código/revisão.
    /// </summary>
    public class TigreCatalogJsonValidationTests
    {
        private static readonly IReadOnlyCollection<string> AllowedProductLines = new[]
        {
            "SR", "SN", "REDUX", "Soldável", "Registros",
            "ClicPEX", "AQUATHERM", "TIGREFire", "PPR",
        };

        private static readonly IReadOnlyCollection<string> AllowedKinds = new[]
        {
            "pipe", "cap", "elbow", "tee", "reducer",
            "fitting", "valve", "accessory", "fixture",
        };

        private readonly ITestOutputHelper _output;

        public TigreCatalogJsonValidationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static List<TigreRawCatalogRow> LoadRows()
        {
            return TigreFallbackCatalogRows.All().ToList();
        }

        [Fact]
        public void Catalog_has_expected_total_entries()
        {
            List<TigreRawCatalogRow> rows = LoadRows();

            // 9 linhas × distribuição esperada totaliza 872. Banda ±5 absorve
            // pequenas correções futuras sem quebrar o test a cada SKU novo.
            Assert.InRange(rows.Count, 867, 877);
        }

        [Fact]
        public void Every_entry_has_nonempty_description_and_positive_code()
        {
            List<TigreRawCatalogRow> rows = LoadRows();

            foreach (TigreRawCatalogRow r in rows)
            {
                Assert.False(string.IsNullOrWhiteSpace(r.Description),
                    $"Description vazia em code {r.Code}");
                Assert.True(r.Code > 0,
                    $"Code inválido (<= 0) em '{r.Description}'");
            }
        }

        [Fact]
        public void Every_entry_has_valid_product_line()
        {
            List<TigreRawCatalogRow> rows = LoadRows();

            foreach (TigreRawCatalogRow r in rows)
            {
                Assert.False(string.IsNullOrEmpty(r.ProductLine),
                    $"ProductLine ausente em code {r.Code} ({r.Description})");
                Assert.Contains(r.ProductLine!, AllowedProductLines);
            }
        }

        [Fact]
        public void Every_entry_has_valid_kind()
        {
            List<TigreRawCatalogRow> rows = LoadRows();

            foreach (TigreRawCatalogRow r in rows)
            {
                Assert.False(string.IsNullOrEmpty(r.Kind),
                    $"Kind ausente em code {r.Code} ({r.Description})");
                Assert.Contains(r.Kind!, AllowedKinds);
            }
        }

        [Fact]
        public void Diameters_are_positive_when_present()
        {
            List<TigreRawCatalogRow> rows = LoadRows();

            foreach (TigreRawCatalogRow r in rows)
            {
                if (r.DiameterMm != 0)
                    Assert.True(r.DiameterMm > 0,
                        $"DiameterMm <= 0 em '{r.Description}' (code {r.Code})");
                if (r.Dn1.HasValue)
                    Assert.True(r.Dn1.Value > 0,
                        $"Dn1 <= 0 em '{r.Description}' (code {r.Code})");
                if (r.Dn2.HasValue)
                    Assert.True(r.Dn2.Value > 0,
                        $"Dn2 <= 0 em '{r.Description}' (code {r.Code})");
            }
        }

        [Fact]
        public void Code_is_not_duplicated_within_same_product_line()
        {
            // Code repetido ENTRE productLines é OK (ex.: Anel de Borracha
            // 37051209 aparece em SR e REDUX). Repetido DENTRO da mesma
            // linha é sinal de payload duplicado pelo parser.
            List<TigreRawCatalogRow> rows = LoadRows();

            IEnumerable<IGrouping<(string?, int), TigreRawCatalogRow>> dups = rows
                .GroupBy(r => (r.ProductLine, r.Code))
                .Where(g => g.Count() > 1);

            List<string> messages = dups
                .Select(g =>
                    $"{g.Key.Item1} code {g.Key.Item2}: " +
                    string.Join(" / ", g.Select(r => r.Description)))
                .ToList();

            Assert.True(messages.Count == 0,
                "Codes duplicados dentro da mesma productLine:\n" +
                string.Join("\n", messages));
        }

        [Fact]
        public void Every_entry_has_nonempty_lean_tokens()
        {
            // R3 — guard de qualidade do StripDimensions. Se alguma entry
            // tiver descrição reduzida a zero tokens após o strip, o
            // matcher não acha NADA pra ela em PipeCodes/Quantifica.
            List<TigreRawCatalogRow> rows = LoadRows();
            ISet<string> ignored = TigreCatalog.DefaultIgnoreTokens;

            foreach (TigreRawCatalogRow r in rows)
            {
                TigreCatalogEntry entry = new TigreCatalogEntry(
                    r.Description, r.DiameterMm, r.Code, ignored);
                Assert.True(entry.LeanTokens.Count > 0,
                    $"LeanTokens vazios em code {r.Code}: " +
                    $"'{r.Description}' → lean='{entry.LeanDescription}'");
            }
        }

        [Fact]
        public void Warns_when_more_than_fifteen_percent_lack_diameter()
        {
            // R1 — warning de console, NÃO falha o test. Existem entradas
            // legítimas sem DN (ralos lineares em cm, conectores sem aspa
            // no payload), mas se passar de 15% provavelmente regrediu
            // alguma regex de parsing.
            List<TigreRawCatalogRow> rows = LoadRows();
            int withoutDiameter = rows.Count(r =>
                r.DiameterMm == 0 && r.Dn1 is null && r.Dn2 is null);
            double pct = 100.0 * withoutDiameter / rows.Count;

            _output.WriteLine(
                $"Entradas sem diâmetro: {withoutDiameter}/{rows.Count} " +
                $"({pct:F1}%)");

            if (pct > 15.0)
            {
                _output.WriteLine(
                    "WARN: mais de 15% sem diâmetro — revisar regex de " +
                    "extract_dims em tools/parse_tigre_payload.py.");
            }
        }
    }
}
