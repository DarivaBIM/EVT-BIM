using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Presentation.Wpf.PipeCodes;
using Xunit;

namespace DarivaBIM.Core.Tests.Presentation.Wpf.PipeCodes
{
    /// <summary>
    /// Slice 4.1 — valida os campos de display introduzidos pelo
    /// <see cref="PipeCodesGroupViewModel"/>: <c>ElementText</c> (coluna
    /// ELEMENTO da janela) e <c>MatchedCodeText</c> (coluna COD. SUGERIDO).
    /// </summary>
    public class PipeCodesGroupViewModelTests
    {
        [Fact]
        public void ElementText_combines_family_and_type_when_different()
        {
            PipeCodesGroupViewModel vm = new(
                categoryName: "Conexões de tubo",
                familyName: "Tigre - Joelho 90 Soldável",
                typeName: "JL90-25",
                diameterMm: 25,
                count: 2,
                status: TigrePipeStatus.Missing,
                elementIds: new long[] { 1, 2 },
                matchedCode: 22150251);

            Assert.Equal("Tigre - Joelho 90 Soldável · JL90-25", vm.ElementText);
        }

        [Fact]
        public void ElementText_collapses_to_type_when_family_equals_type()
        {
            // Caso típico de Pipes (system family): TigreElementDataReader
            // faz fallback FamilyName = TypeName. Evita renderizar
            // "Soldável 25 · Soldável 25" na UI.
            PipeCodesGroupViewModel vm = new(
                categoryName: "Tubulações",
                familyName: "Soldável 25",
                typeName: "Soldável 25",
                diameterMm: 25,
                count: 10,
                status: TigrePipeStatus.Ok,
                elementIds: new long[] { 1 },
                matchedCode: 10120250);

            Assert.Equal("Soldável 25", vm.ElementText);
        }

        [Fact]
        public void ElementText_falls_back_to_type_when_family_is_empty()
        {
            // Defesa em profundidade — se algum scan futuro deixar
            // FamilyName vazio, ElementText não quebra com " · " órfão.
            PipeCodesGroupViewModel vm = new(
                categoryName: "Tubulações",
                familyName: string.Empty,
                typeName: "Soldável 32",
                diameterMm: 32,
                count: 4,
                status: TigrePipeStatus.Missing,
                elementIds: new long[] { 1 },
                matchedCode: 10120320);

            Assert.Equal("Soldável 32", vm.ElementText);
        }

        [Fact]
        public void MatchedCodeText_renders_code_when_present()
        {
            PipeCodesGroupViewModel vm = new(
                categoryName: "Conexões de tubo",
                familyName: "Tigre - Tê 90",
                typeName: "T90-25",
                diameterMm: 25,
                count: 1,
                status: TigrePipeStatus.Missing,
                elementIds: new long[] { 1 },
                matchedCode: 22130251);

            Assert.Equal("22130251", vm.MatchedCodeText);
        }

        [Fact]
        public void MatchedCodeText_falls_back_to_em_dash_when_no_match()
        {
            // NoMatch é o único status onde MatchedCode é null por
            // construção (ver TigreCodeScanner.ComputeStatus). UI mostra
            // travessão pra alinhar com DiameterText "—".
            PipeCodesGroupViewModel vm = new(
                categoryName: "Tubulações",
                familyName: "Tubo Legado",
                typeName: "Tubo Legado",
                diameterMm: 25,
                count: 1,
                status: TigrePipeStatus.NoMatch,
                elementIds: new long[] { 1 },
                matchedCode: null);

            Assert.Equal("—", vm.MatchedCodeText);
        }

        [Fact]
        public void FamilyName_coerces_null_to_empty()
        {
            PipeCodesGroupViewModel vm = new(
                categoryName: "Tubulações",
                familyName: null!,
                typeName: "Soldável 25",
                diameterMm: 25,
                count: 1,
                status: TigrePipeStatus.Ok,
                elementIds: new long[] { 1 },
                matchedCode: 10120250);

            Assert.Equal(string.Empty, vm.FamilyName);
            Assert.Equal("Soldável 25", vm.ElementText);
        }
    }
}
