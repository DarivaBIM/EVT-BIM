using System.Collections.Generic;
using Xunit;

namespace DarivaBIM.Architecture.Tests
{
    /// <summary>
    /// Antes da consolidacao (ADR-0016) Infrastructure era 4 csprojs separados:
    /// Api, Persistence, Licensing, Telemetry. A separacao por csproj impedia
    /// que Persistence usasse HttpClient ou que Licensing dependesse de Api,
    /// porque o build quebrava por falta de ProjectReference. Apos fundir
    /// tudo em um unico DarivaBIM.Infrastructure, essa fronteira passa a ser
    /// preservada apenas pela convencao de pastas + estes testes de
    /// arquitetura.
    /// </summary>
    public class InfrastructureBoundariesTests
    {
        private const string InfrastructureRoot = "src/Infrastructure/DarivaBIM.Infrastructure";

        [Fact]
        public void Persistence_does_not_reference_HttpClient()
        {
            string root = SourceTreeLocator.FindProjectRoot(InfrastructureRoot + "/Persistence");
            var violations = ForbiddenUsingsScanner.Scan(root, new[]
            {
                "System.Net.Http",
            });
            Assert.True(
                violations.Count == 0,
                $"Persistence/ não pode usar System.Net.Http (use Api/):\n{ForbiddenUsingsScanner.Format(violations)}");
        }

        [Fact]
        public void Persistence_does_not_reference_Api()
        {
            string root = SourceTreeLocator.FindProjectRoot(InfrastructureRoot + "/Persistence");
            var violations = ForbiddenUsingsScanner.Scan(root, new[]
            {
                "DarivaBIM.Infrastructure.Api",
            });
            Assert.True(
                violations.Count == 0,
                $"Persistence/ não pode depender de Api/ (a direção é inversa):\n{ForbiddenUsingsScanner.Format(violations)}");
        }

        [Fact]
        public void Licensing_does_not_reference_HttpClient_or_Api()
        {
            string root = SourceTreeLocator.FindProjectRoot(InfrastructureRoot + "/Licensing");
            var forbidden = new[]
            {
                "System.Net.Http",
                "DarivaBIM.Infrastructure.Api",
                "DarivaBIM.Infrastructure.Persistence",
                "DarivaBIM.Infrastructure.Telemetry",
            };
            var violations = ForbiddenUsingsScanner.Scan(root, forbidden);
            Assert.True(
                violations.Count == 0,
                $"Licensing/ deve permanecer isolado das outras pastas Infra:\n{ForbiddenUsingsScanner.Format(violations)}");
        }

        [Fact]
        public void Telemetry_does_not_reference_HttpClient_or_other_infra()
        {
            string root = SourceTreeLocator.FindProjectRoot(InfrastructureRoot + "/Telemetry");
            var forbidden = new[]
            {
                "System.Net.Http",
                "DarivaBIM.Infrastructure.Api",
                "DarivaBIM.Infrastructure.Persistence",
                "DarivaBIM.Infrastructure.Licensing",
            };
            var violations = ForbiddenUsingsScanner.Scan(root, forbidden);
            Assert.True(
                violations.Count == 0,
                $"Telemetry/ deve permanecer isolado das outras pastas Infra:\n{ForbiddenUsingsScanner.Format(violations)}");
        }
    }
}
