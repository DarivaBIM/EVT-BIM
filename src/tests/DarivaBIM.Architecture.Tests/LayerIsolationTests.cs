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
    }
}
