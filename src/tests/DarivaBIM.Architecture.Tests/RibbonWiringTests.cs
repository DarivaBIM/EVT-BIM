using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DarivaBIM.Architecture.Tests
{
    /// <summary>
    /// Guard contra o "botão fantasma": toda <c>*Feature.cs</c> em
    /// <c>Plugin.SharedSource/Features</c> que declara <c>static RibbonCommandId
    /// CommandId =&gt;</c> precisa estar registrada no
    /// <c>CommandRegistry</c>; caso contrário <c>RibbonBuilder</c> pula o
    /// botão silenciosamente (ver <c>RibbonBuilder.cs</c> linhas 45–49) e a
    /// feature não aparece na ribbon em runtime — defeito que só se mostra
    /// quando o Revit abre.
    /// </summary>
    public class RibbonWiringTests
    {
        private static readonly Regex CommandIdDeclaration = new(
            @"static\s+RibbonCommandId\s+CommandId\s*=>\s*RibbonCommandId\.\w+",
            RegexOptions.Compiled);

        // Captura cada entry do dictionary _commands no formato
        // "{ <Feature>.CommandId, <Feature>.CommandType }" — backreference \1
        // garante que ambos lados se referem à MESMA feature.
        private static readonly Regex RegistryEntry = new(
            @"(\w+Feature)\.CommandId\s*,\s*\1\.CommandType",
            RegexOptions.Compiled);

        [Fact]
        public void Every_feature_with_command_id_is_registered_in_command_registry()
        {
            string repoRoot = SourceTreeLocator.FindRepositoryRoot();
            string sharedSource = Path.Combine(repoRoot, "src", "Plugins", "DarivaBIM.Plugin.SharedSource");
            string featuresDir = Path.Combine(sharedSource, "Features");
            string registryPath = Path.Combine(sharedSource, "Ribbon", "CommandRegistry.cs");

            Assert.True(File.Exists(registryPath), $"CommandRegistry.cs não encontrado em {registryPath}");
            Assert.True(Directory.Exists(featuresDir), $"Pasta Features não encontrada: {featuresDir}");

            string registryContent = File.ReadAllText(registryPath);
            HashSet<string> registeredFeatures = new(
                RegistryEntry.Matches(registryContent).Select(m => m.Groups[1].Value),
                System.StringComparer.Ordinal);

            List<string> declaredFeatures = new();
            foreach (string featureFile in Directory.EnumerateFiles(featuresDir, "*Feature.cs", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(featureFile);
                if (!CommandIdDeclaration.IsMatch(content))
                    continue;

                string featureName = Path.GetFileNameWithoutExtension(featureFile);
                declaredFeatures.Add(featureName);
            }

            List<string> missing = declaredFeatures
                .Where(f => !registeredFeatures.Contains(f))
                .ToList();

            Assert.True(
                missing.Count == 0,
                "Features declarando RibbonCommandId.CommandId mas ausentes do CommandRegistry " +
                "(RibbonBuilder vai pular o botão silenciosamente):\n  " +
                string.Join("\n  ", missing));
        }
    }
}
