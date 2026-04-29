using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DarivaBIM.Architecture.Tests
{
    /// <summary>
    /// Lightweight text scanner that walks every <c>*.cs</c> file under a
    /// project directory and reports lines whose trimmed prefix matches a
    /// forbidden <c>using</c> namespace. Pure text scan keeps the rule
    /// understandable and skips any need to load the offending assemblies
    /// (some of them depend on RevitAPI, which is not available on CI).
    /// </summary>
    internal static class ForbiddenUsingsScanner
    {
        public sealed record Violation(string FilePath, int LineNumber, string Line);

        public static IReadOnlyList<Violation> Scan(string projectRoot, IReadOnlyList<string> forbiddenPrefixes)
        {
            List<Violation> violations = new();

            foreach (string file in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories))
            {
                // Skip generated artefacts under bin/obj — the SDK drops
                // assembly attributes referencing arbitrary namespaces there.
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/bin/") || normalized.Contains("/obj/"))
                    continue;

                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].TrimStart();
                    if (!line.StartsWith("using "))
                        continue;

                    string usingTarget = line.Substring("using ".Length).TrimEnd(';', ' ', '\t');
                    foreach (string forbidden in forbiddenPrefixes)
                    {
                        if (usingTarget == forbidden || usingTarget.StartsWith(forbidden + "."))
                        {
                            violations.Add(new Violation(file, i + 1, line.TrimEnd()));
                            break;
                        }
                    }
                }
            }

            return violations;
        }

        public static string Format(IReadOnlyList<Violation> violations)
        {
            return string.Join(
                "\n",
                violations.Select(v => $"  {v.FilePath}:{v.LineNumber} → {v.Line}"));
        }
    }
}
