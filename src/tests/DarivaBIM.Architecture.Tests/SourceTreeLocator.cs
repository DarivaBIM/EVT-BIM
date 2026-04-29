using System;
using System.IO;

namespace DarivaBIM.Architecture.Tests
{
    /// <summary>
    /// Walks up from the test binary location until the repository root is
    /// found (the folder containing <c>DarivaBIM.sln</c>). The architecture
    /// tests inspect the on-disk source tree, so they need a stable handle on
    /// the repo root regardless of the build configuration.
    /// </summary>
    internal static class SourceTreeLocator
    {
        public static string FindRepositoryRoot()
        {
            string current = AppContext.BaseDirectory;
            for (int i = 0; i < 12; i++)
            {
                if (File.Exists(Path.Combine(current, "DarivaBIM.sln")))
                    return current;

                DirectoryInfo? parent = Directory.GetParent(current);
                if (parent == null)
                    break;

                current = parent.FullName;
            }

            throw new InvalidOperationException(
                "Não foi possível localizar a raiz do repositório (DarivaBIM.sln).");
        }

        public static string FindProjectRoot(string projectRelativePath)
        {
            string repoRoot = FindRepositoryRoot();
            string projectRoot = Path.Combine(repoRoot, projectRelativePath);
            if (!Directory.Exists(projectRoot))
            {
                throw new DirectoryNotFoundException(
                    $"Diretório de projeto não encontrado: {projectRoot}");
            }

            return projectRoot;
        }
    }
}
