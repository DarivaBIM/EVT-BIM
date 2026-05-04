using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.UseCases.ApplyTigreCodes;
using Xunit;

namespace DarivaBIM.Core.Tests.Application.UseCases
{
    public class ApplyTigreCodesUseCaseTests
    {
        [Fact]
        public void Execute_returns_service_result()
        {
            var fakeService = new FakeTigreCodeApplyService();
            var useCase = new ApplyTigreCodesUseCase(fakeService);

            TigreCodeApplyResult result = useCase.Execute();

            Assert.Equal(7, result.PipesUpdated);
            Assert.Equal(1, fakeService.CallCount);
        }

        [Fact]
        public void FormatReport_produces_human_readable_summary()
        {
            var report = new TigreCodeApplyResult
            {
                CatalogCount = 42,
                PipesTotal = 10,
                PipesUpdated = 8,
            };

            string formatted = ApplyTigreCodesUseCase.FormatReport(report);
            Assert.Contains("Catálogo: 42", formatted);
            Assert.Contains("Tubos: 10", formatted);
        }

        private sealed class FakeTigreCodeApplyService : ITigreCodeApplyService
        {
            public int CallCount { get; private set; }

            public TigreCodeApplyResult Apply()
            {
                CallCount++;
                return new TigreCodeApplyResult
                {
                    PipesTotal = 10,
                    PipesUpdated = 7,
                };
            }
        }
    }
}
