using System.Collections.Generic;
using Xunit;

namespace DarivaBIM.Architecture.Tests
{
    /// <summary>
    /// Enforces the dependency rules from ADR-0001 (clean architecture,
    /// core-agnostic) and ADR-0007 (Revit hosting layer): the Domain,
    /// Application and Presentation.Wpf assemblies are not allowed to depend
    /// on any RevitAPI namespace, and Application is not allowed to pull in
    /// WPF.
    /// </summary>
    public class LayerIsolationTests
    {
        private static readonly IReadOnlyList<string> RevitForbiddenPrefixes = new[]
        {
            "Autodesk.Revit",
        };

        private static readonly IReadOnlyList<string> WpfForbiddenPrefixes = new[]
        {
            "System.Windows",
            "System.Windows.Controls",
            "System.Windows.Media",
        };

        [Fact]
        public void Domain_does_not_reference_RevitAPI()
        {
            string projectRoot = SourceTreeLocator.FindProjectRoot("src/Core/DarivaBIM.Domain");
            var violations = ForbiddenUsingsScanner.Scan(projectRoot, RevitForbiddenPrefixes);
            Assert.True(
                violations.Count == 0,
                $"DarivaBIM.Domain não pode usar Autodesk.Revit.*:\n{ForbiddenUsingsScanner.Format(violations)}");
        }

        [Fact]
        public void Application_does_not_reference_RevitAPI()
        {
            string projectRoot = SourceTreeLocator.FindProjectRoot("src/Core/DarivaBIM.Application");
            var violations = ForbiddenUsingsScanner.Scan(projectRoot, RevitForbiddenPrefixes);
            Assert.True(
                violations.Count == 0,
                $"DarivaBIM.Application não pode usar Autodesk.Revit.*:\n{ForbiddenUsingsScanner.Format(violations)}");
        }

        [Fact]
        public void Application_does_not_reference_Wpf()
        {
            string projectRoot = SourceTreeLocator.FindProjectRoot("src/Core/DarivaBIM.Application");
            var violations = ForbiddenUsingsScanner.Scan(projectRoot, WpfForbiddenPrefixes);
            Assert.True(
                violations.Count == 0,
                $"DarivaBIM.Application não pode usar System.Windows / WPF:\n{ForbiddenUsingsScanner.Format(violations)}");
        }

        [Fact]
        public void PresentationWpf_does_not_reference_RevitAPI()
        {
            string projectRoot = SourceTreeLocator.FindProjectRoot("src/Presentation/DarivaBIM.Presentation.Wpf");
            var violations = ForbiddenUsingsScanner.Scan(projectRoot, RevitForbiddenPrefixes);
            Assert.True(
                violations.Count == 0,
                $"DarivaBIM.Presentation.Wpf não pode usar Autodesk.Revit.*:\n{ForbiddenUsingsScanner.Format(violations)}");
        }

        [Fact]
        public void Domain_does_not_reference_Wpf()
        {
            string projectRoot = SourceTreeLocator.FindProjectRoot("src/Core/DarivaBIM.Domain");
            var violations = ForbiddenUsingsScanner.Scan(projectRoot, WpfForbiddenPrefixes);
            Assert.True(
                violations.Count == 0,
                $"DarivaBIM.Domain não pode usar System.Windows / WPF:\n{ForbiddenUsingsScanner.Format(violations)}");
        }

        [Fact]
        public void PluginSharedSource_does_not_reference_versioned_namespaces()
        {
            // Plugin.SharedSource e compilado pelos plugins V2025 e V2026;
            // qualquer using direto a DarivaBIM.Plugin.V2025 ou V2026 quebra
            // a ideia de fonte unica neutra. Imports do adapter (V2025/V2026)
            // continuam permitidos porque ficam atras de #if REVIT2025/REVIT2026
            // e o ForbiddenUsingsScanner so olha "using " no inicio da linha.
            string projectRoot = SourceTreeLocator.FindProjectRoot("src/Plugins/DarivaBIM.Plugin.SharedSource");
            var violations = ForbiddenUsingsScanner.Scan(projectRoot, new[]
            {
                "DarivaBIM.Plugin.V2025",
                "DarivaBIM.Plugin.V2026",
            });
            Assert.True(
                violations.Count == 0,
                $"Plugin.SharedSource não pode referenciar namespaces versionados:\n{ForbiddenUsingsScanner.Format(violations)}");
        }
    }
}
