using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DarivaBIM.Architecture.Tests
{
    /// <summary>
    /// Verifica que todo arquivo PNG referenciado por um *Button.cs em
    /// <c>Plugin.SharedSource/Features</c> realmente existe em
    /// <c>Plugin.SharedSource/Resources/Icons</c>. Cuidado classico em plugin
    /// Revit: alguem renomeia o icone em disco, esquece o button, e na
    /// proxima abertura o botao aparece sem imagem.
    /// </summary>
    public class IconReferencesTests
    {
        private static readonly Regex IconLiteral = new(
            "Ribbon/Resources/Icons/(?<file>[A-Za-z0-9_\\-]+\\.png)",
            RegexOptions.Compiled);

        [Fact]
        public void Every_button_icon_reference_resolves_to_a_real_file()
        {
            string repoRoot = SourceTreeLocator.FindRepositoryRoot();
            string sharedSource = Path.Combine(repoRoot, "src", "Plugins", "DarivaBIM.Plugin.SharedSource");
            string iconsDir = Path.Combine(sharedSource, "Resources", "Icons");

            Assert.True(
                Directory.Exists(iconsDir),
                $"Pasta de icones nao encontrada: {iconsDir}");

            HashSet<string> iconsOnDisk = new(
                Directory.EnumerateFiles(iconsDir, "*.png").Select(Path.GetFileName)!,
                System.StringComparer.OrdinalIgnoreCase);

            List<string> missing = new();

            foreach (string buttonFile in Directory.EnumerateFiles(
                Path.Combine(sharedSource, "Features"),
                "*Button.cs",
                SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(buttonFile);
                foreach (Match match in IconLiteral.Matches(content))
                {
                    string fileName = match.Groups["file"].Value;
                    if (!iconsOnDisk.Contains(fileName))
                    {
                        missing.Add($"{buttonFile} → Resources/Icons/{fileName}");
                    }
                }
            }

            Assert.True(
                missing.Count == 0,
                "Botoes referenciam icones que nao existem em Plugin.SharedSource/Resources/Icons:\n  " +
                string.Join("\n  ", missing));
        }
    }
}
