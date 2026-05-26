using System.Collections.Generic;
using DarivaBIM.Application.DTOs.Tigre;
using Xunit;

namespace DarivaBIM.Core.Tests.Application.DTOs.Tigre
{
    /// <summary>
    /// Valida o contrato dos aliases legados de <see cref="TigreScanResult"/>
    /// (Slice 3.2): <c>PipesTotal</c>/<c>PipesWithParameter</c>/
    /// <c>PipesWithoutParameter</c> delegam pros campos generalizados
    /// (<c>ElementsTotal</c> etc.), e <c>ParameterIsBound</c> agrega o
    /// dicionário <see cref="TigreScanResult.BindingAvailable"/>.
    /// </summary>
    public class TigreScanResultTests
    {
        [Fact]
        public void Pipes_aliases_match_Elements_fields()
        {
            TigreScanResult result = new()
            {
                ElementsTotal = 100,
                ElementsWithParameter = 80,
                ElementsWithoutParameter = 20,
            };

            Assert.Equal(100, result.PipesTotal);
            Assert.Equal(80, result.PipesWithParameter);
            Assert.Equal(20, result.PipesWithoutParameter);
        }

        [Fact]
        public void ParameterIsBound_true_when_all_categories_bound()
        {
            TigreScanResult result = new()
            {
                BindingAvailable = new Dictionary<string, bool>
                {
                    ["Tubulações"] = true,
                    ["Conexões de tubo"] = true,
                    ["Acessórios de tubulação"] = true,
                },
            };

            Assert.True(result.ParameterIsBound);
        }

        [Fact]
        public void ParameterIsBound_false_when_any_category_not_bound()
        {
            TigreScanResult result = new()
            {
                BindingAvailable = new Dictionary<string, bool>
                {
                    ["Tubulações"] = true,
                    ["Conexões de tubo"] = false, // ← falha aqui
                    ["Acessórios de tubulação"] = true,
                },
            };

            Assert.False(result.ParameterIsBound);
        }

        [Fact]
        public void ParameterIsBound_true_when_no_elements_in_project()
        {
            // BindingAvailable vazio = "nada a fazer". UI não bloqueia o
            // botão Ensure por falta de binding nesse caso.
            TigreScanResult result = new()
            {
                ElementsTotal = 0,
                BindingAvailable = new Dictionary<string, bool>(),
            };

            Assert.True(result.ParameterIsBound);
        }

        [Fact]
        public void ByCategoryStats_default_is_empty_dictionary()
        {
            TigreScanResult result = new();

            Assert.NotNull(result.ByCategoryStats);
            Assert.Empty(result.ByCategoryStats);
        }
    }
}
