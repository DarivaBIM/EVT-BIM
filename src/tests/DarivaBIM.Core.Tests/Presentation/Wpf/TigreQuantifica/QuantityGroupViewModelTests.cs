using DarivaBIM.Application.DTOs.Quantifica;
using DarivaBIM.Presentation.Wpf.TigreQuantifica;
using Xunit;

namespace DarivaBIM.Core.Tests.Presentation.Wpf.TigreQuantifica
{
    /// <summary>
    /// Slice 4.5 — protege o contrato de <see cref="QuantityGroupViewModel"/>
    /// depois da consolidação FAMÍLIA+TIPO em coluna única ELEMENTO. O
    /// fallback respeita TigreDescription → "familia · tipo" → tipo isolado.
    /// </summary>
    public class QuantityGroupViewModelTests
    {
        [Fact]
        public void ElementText_prefers_tigre_description_when_present()
        {
            QuantityGroup g = new()
            {
                Family = "Tigre - Joelho 90 Soldável",
                Type = "JL90-25",
                TigreDescription = "Joelho Sold. 25mm Tigre",
                MeasurementKind = MeasurementKind.Count,
                ElementCount = 4,
                Quantity = 4m,
            };
            QuantityGroupViewModel vm = new(g);

            // TigreDescription trumpa "familia · tipo" — é a descrição
            // canônica que o cliente quer ler no relatório.
            Assert.Equal("Joelho Sold. 25mm Tigre", vm.ElementText);
        }

        [Fact]
        public void ElementText_falls_back_to_family_dot_type_when_tigre_description_empty()
        {
            QuantityGroup g = new()
            {
                Family = "Tigre - Joelho 90 Soldável",
                Type = "JL90-25",
                TigreDescription = null,
                MeasurementKind = MeasurementKind.Count,
                ElementCount = 1,
                Quantity = 1m,
            };
            QuantityGroupViewModel vm = new(g);

            Assert.Equal("Tigre - Joelho 90 Soldável · JL90-25", vm.ElementText);
        }

        [Fact]
        public void ElementText_collapses_to_type_when_family_equals_type()
        {
            // Caso típico de Pipes (system family): FamilyName == TypeName.
            // Evita "Soldável 25 · Soldável 25" no relatório.
            QuantityGroup g = new()
            {
                Family = "Soldável 25",
                Type = "Soldável 25",
                TigreDescription = "",
                MeasurementKind = MeasurementKind.LengthMeters,
                ElementCount = 2,
                Quantity = 5m,
            };
            QuantityGroupViewModel vm = new(g);

            Assert.Equal("Soldável 25", vm.ElementText);
        }

        [Fact]
        public void ElementText_falls_back_to_type_when_family_is_empty()
        {
            QuantityGroup g = new()
            {
                Family = "",
                Type = "Soldável 32",
                TigreDescription = null,
                MeasurementKind = MeasurementKind.LengthMeters,
                ElementCount = 1,
                Quantity = 3m,
            };
            QuantityGroupViewModel vm = new(g);

            Assert.Equal("Soldável 32", vm.ElementText);
        }

        [Fact]
        public void ElementText_whitespace_only_tigre_description_falls_back_to_family_type()
        {
            // Modeler pode preencher " " no parametro por engano —
            // IsNullOrWhiteSpace pega esse caso e usa o fallback.
            QuantityGroup g = new()
            {
                Family = "Tigre - Reducao",
                Type = "RED-25-20",
                TigreDescription = "   ",
                MeasurementKind = MeasurementKind.Count,
                ElementCount = 1,
                Quantity = 1m,
            };
            QuantityGroupViewModel vm = new(g);

            Assert.Equal("Tigre - Reducao · RED-25-20", vm.ElementText);
        }
    }
}
