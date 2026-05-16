using System.Globalization;

namespace DarivaBIM.Sidecar.Ipc
{
    /// <summary>
    /// Constroi nomes de NamedPipe deterministicos a partir do PID do processo
    /// Revit. Atrelar ao PID permite duas instancias do Revit (ex.: 2025 e
    /// 2026) rodarem simultaneamente sem colisao de pipe.
    /// </summary>
    public static class PipeNames
    {
        // Prefixo curto pra log/debug; o nome completo fica
        // "DarivaBIM.Sidecar.<pid>" e e usado em
        // System.IO.Pipes.NamedPipeServerStream com seu proprio prefixo
        // "\\.\pipe\" automatico.
        private const string Prefix = "DarivaBIM.Sidecar";

        public static string ForRevit(int revitProcessId)
        {
            return Prefix + "." + revitProcessId.ToString(CultureInfo.InvariantCulture);
        }
    }
}
