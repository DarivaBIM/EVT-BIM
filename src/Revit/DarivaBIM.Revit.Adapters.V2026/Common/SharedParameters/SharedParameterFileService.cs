using System;
using System.IO;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.V2026.Common.SharedParameters
{
    /// <summary>
    /// Abre/cria um arquivo de Shared Parameters acessível ao Revit. Quando o
    /// usuário não tem nenhum arquivo configurado (cenário comum em Revit
    /// fresco), gera um arquivo vazio em <c>%TEMP%</c>; o caller fica
    /// responsável por restaurar o caminho anterior chamando
    /// <see cref="RestorePreviousPath"/> no <c>finally</c>.
    /// </summary>
    internal static class SharedParameterFileService
    {
        public const string TemporaryFileName = "DarivaBIM_SharedParameters.txt";

        /// <summary>
        /// Garante que <c>app.SharedParametersFilename</c> aponte para um
        /// arquivo existente e retorna a <see cref="DefinitionFile"/> aberta.
        /// O caminho anterior é devolvido em <paramref name="previousPath"/>
        /// para que o caller restaure depois.
        /// </summary>
        public static DefinitionFile OpenOrCreate(Application app, out string? previousPath)
        {
            previousPath = app.SharedParametersFilename;
            string? sp = previousPath;

            if (string.IsNullOrEmpty(sp) || !File.Exists(sp))
            {
                sp = Path.Combine(Path.GetTempPath(), TemporaryFileName);
                if (!File.Exists(sp))
                {
                    File.WriteAllText(sp, string.Empty);
                }
                app.SharedParametersFilename = sp;
            }

            return app.OpenSharedParameterFile()
                ?? throw new InvalidOperationException(
                    "Não foi possível abrir/criar o arquivo de Shared Parameters.");
        }

        /// <summary>
        /// Best-effort: restaura o caminho de Shared Parameters do Revit.
        /// Usado em <c>finally</c> para não deixar o usuário com um caminho
        /// temporário "permanente".
        /// </summary>
        public static void RestorePreviousPath(Application app, string? previousPath)
        {
            if (previousPath == null)
                return;

            try
            {
                app.SharedParametersFilename = previousPath;
            }
            catch
            {
                // best-effort
            }
        }
    }
}
