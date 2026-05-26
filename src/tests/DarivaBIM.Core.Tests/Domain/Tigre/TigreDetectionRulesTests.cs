using System.Collections.Generic;
using DarivaBIM.Domain.Tigre;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Tigre
{
    /// <summary>
    /// Heurística pura — testa todos os 6 sinais isoladamente + os casos
    /// críticos de trumping (ExistingCodeMatch trumpa veto, veto trumpa
    /// Family/Description).
    /// </summary>
    public class TigreDetectionRulesTests
    {
        // Catalog mínimo com 1 SKU pra Sinal 0 ter o que validar.
        private static readonly TigreCatalog Catalog = new TigreCatalog(
            new[]
            {
                new TigreRawCatalogRow
                {
                    Description = "Joelho 90 Soldável 25mm",
                    DiameterMm = 25,
                    Code = 22150251,
                    Kind = "elbow",
                    ProductLine = "Soldável",
                },
            });

        [Theory]
        // Sinal 4 — DistinctiveBrandToken via family (Soldável)
        [InlineData("AF_Soldavel_Joelho90", null, null, null,
            true, TigreDetectionSignal.DistinctiveBrandToken)]
        // Sinal 3 — FamilyNameContainsTigre
        [InlineData("Pipe_Tigre_PPR_DN25", null, null, null,
            true, TigreDetectionSignal.FamilyNameContainsTigre)]
        // Default — sem nenhum sinal
        [InlineData("Generic_PVC", null, null, null,
            false, TigreDetectionSignal.None)]
        // Default — Knauf no NOME (sem manufacturer) não é veto (veto só
        // dispara com Manufacturer preenchido); Family.Name "knauf" não
        // bate nenhum distinctive nem "tigre" → None.
        [InlineData("Knauf_Drywall", null, null, null,
            false, TigreDetectionSignal.None)]
        // Sinal 1 — veto Manufacturer (sem token tigre)
        [InlineData("Pipe_Generic", "Amanco", null, null,
            false, TigreDetectionSignal.ManufacturerVeto)]
        // Sinal 1 — veto trumpa family com "tigre"
        [InlineData("Pipe_Tigre_PPR", "Amanco", null, null,
            false, TigreDetectionSignal.ManufacturerVeto)]
        // Sinal 2 — Manufacturer "Tigre S/A"
        [InlineData("Pipe_Generic", "Tigre S/A", null, null,
            true, TigreDetectionSignal.ManufacturerTigre)]
        // Sinal 2 — © Tigre S/A (copyright vira separator-like)
        [InlineData("Pipe_Generic", "© Tigre S/A", null, null,
            true, TigreDetectionSignal.ManufacturerTigre)]
        // Sinal 2 — Tigre MAIÚSCULO
        [InlineData("Pipe_Generic", "TIGRE", null, null,
            true, TigreDetectionSignal.ManufacturerTigre)]
        // Sinal 4 — DistinctiveBrandToken via description (Soldável)
        [InlineData("Pipe_Generic", null, "Joelho Soldável", null,
            true, TigreDetectionSignal.DistinctiveBrandToken)]
        // Sinal 5 — Description menciona "tigre" sem outro token distintivo
        [InlineData("Pipe_Generic", null, "Joelho TIGRE marrom", null,
            true, TigreDetectionSignal.DescriptionMentionsTigre)]
        // Sinal 1 — veto Knauf trumpa PPR token na description
        [InlineData("Pipe_Knauf", "Knauf", "Joelho PPR", null,
            false, TigreDetectionSignal.ManufacturerVeto)]
        public void Detect_returns_expected_signal(
            string? familyName,
            string? manufacturer,
            string? description,
            int? existingCode,
            bool expectedIsTigre,
            TigreDetectionSignal expectedSignal)
        {
            TigreDetectionResult result = TigreDetectionRules.Detect(
                familyName, manufacturer, description, existingCode, Catalog);

            Assert.Equal(expectedIsTigre, result.IsTigre);
            Assert.Equal(expectedSignal, result.Signal);
        }

        [Fact]
        public void ExistingCodeMatch_trumpa_ManufacturerVeto()
        {
            // Sinal 0 trumpa Sinal 1: mesmo com Manufacturer=Amanco, se o
            // elemento já tem Tigre: Código preenchido com SKU válido,
            // é Tigre.
            TigreDetectionResult result = TigreDetectionRules.Detect(
                familyName: "Pipe_Generic",
                manufacturer: "Amanco",
                description: null,
                existingCode: 22150251,
                Catalog);

            Assert.True(result.IsTigre);
            Assert.Equal(TigreDetectionSignal.ExistingCodeMatch, result.Signal);
        }

        [Fact]
        public void ExistingCodeMatch_trumpa_ManufacturerTigre_signal()
        {
            // Quando code válido E manufacturer=Tigre, ambos disparariam
            // sinal positivo. Sinal 0 vem antes → reporta ExistingCodeMatch.
            TigreDetectionResult result = TigreDetectionRules.Detect(
                familyName: "Pipe_Generic",
                manufacturer: "Tigre",
                description: null,
                existingCode: 22150251,
                Catalog);

            Assert.True(result.IsTigre);
            Assert.Equal(TigreDetectionSignal.ExistingCodeMatch, result.Signal);
        }

        [Fact]
        public void Empty_inputs_return_None()
        {
            TigreDetectionResult result = TigreDetectionRules.Detect(
                familyName: null,
                manufacturer: null,
                description: null,
                existingCode: null,
                Catalog);

            Assert.False(result.IsTigre);
            Assert.Equal(TigreDetectionSignal.None, result.Signal);
        }

        [Fact]
        public void Existing_code_zero_is_not_a_signal()
        {
            // Sinal 0 exige code > 0 (HasCode garante isso). Code=0 não
            // dispara, segue pros sinais seguintes — sem outros, None.
            TigreDetectionResult result = TigreDetectionRules.Detect(
                familyName: "Generic",
                manufacturer: null,
                description: null,
                existingCode: 0,
                Catalog);

            Assert.False(result.IsTigre);
            Assert.Equal(TigreDetectionSignal.None, result.Signal);
        }

        [Fact]
        public void Existing_code_unknown_falls_through_to_other_signals()
        {
            // Code preenchido mas não-catalógico (legado errado, mistype):
            // Sinal 0 falha em HasCode, mas Family ou Description podem
            // ainda casar.
            TigreDetectionResult result = TigreDetectionRules.Detect(
                familyName: "Tigre_Soldavel_Joelho",
                manufacturer: null,
                description: null,
                existingCode: 99999999,
                Catalog);

            Assert.True(result.IsTigre);
            Assert.Equal(TigreDetectionSignal.FamilyNameContainsTigre, result.Signal);
        }
    }
}
