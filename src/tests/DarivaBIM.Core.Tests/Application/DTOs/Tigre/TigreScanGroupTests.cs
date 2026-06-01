using DarivaBIM.Application.DTOs.Tigre;
using Xunit;

namespace DarivaBIM.Core.Tests.Application.DTOs.Tigre
{
    /// <summary>
    /// Slice 4.1 — valida o contrato de <see cref="TigreScanGroup"/> após o
    /// ganho da propriedade <c>FamilyName</c>. FamilyName entra na chave de
    /// agrupamento do scanner (ver TigreCodeScanner.GroupKey) e é exibido
    /// pela UI no formato "familia · tipo".
    /// </summary>
    public class TigreScanGroupTests
    {
        [Fact]
        public void Ctor_assigns_all_fields_including_FamilyName()
        {
            TigreScanGroup group = new(
                categoryName: "Conexões de tubo",
                kind: "fitting",
                familyName: "Tigre - Joelho 90 Soldável",
                typeName: "JL90-25",
                diameterMm: 25,
                status: TigrePipeStatus.Missing,
                elementIds: new long[] { 1001, 1002 },
                matchedCode: 22150251);

            Assert.Equal("Conexões de tubo", group.CategoryName);
            Assert.Equal("fitting", group.Kind);
            Assert.Equal("Tigre - Joelho 90 Soldável", group.FamilyName);
            Assert.Equal("JL90-25", group.TypeName);
            Assert.Equal(25, group.DiameterMm);
            Assert.Equal(TigrePipeStatus.Missing, group.Status);
            Assert.Equal(2, group.Count);
            Assert.Equal(22150251, group.MatchedCode);
        }

        [Fact]
        public void Ctor_coerces_null_FamilyName_to_empty_string()
        {
            // FamilyName ausente nunca é null em código real (scanner
            // garante fallback pra TypeName em Pipes), mas o DTO precisa
            // ser defensivo igual aos outros campos string.
            TigreScanGroup group = new(
                categoryName: "Tubulações",
                kind: "pipe",
                familyName: null!,
                typeName: "Soldável 25",
                diameterMm: 25,
                status: TigrePipeStatus.Ok,
                elementIds: new long[] { 1 },
                matchedCode: 11000001);

            Assert.Equal(string.Empty, group.FamilyName);
        }
    }
}
