using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Plugin.Sidecar;
using DarivaBIM.Revit.Hosting.Commands;

namespace DarivaBIM.Plugin.Features.FamiliesImporter
{
    /// <summary>
    /// Abre (ou foca, se ja estiver aberta) a janela de Importar Familias.
    ///
    /// Arquitetura: o conteudo nao e mais um WPF UC in-process. Em vez disso,
    /// spawnamos o sidecar EXE <c>DarivaBIM.FamilyBrowser</c> que hospeda o
    /// AcervoBIM (Next.js) via WebView2 em processo separado, contornando o
    /// bug do Win11 + Revit 2025/2026 que congela qualquer UI WPF in-process
    /// apos placement de familia (REVIT-236376 / REVIT-237190).
    ///
    /// O sidecar conversa com este plugin via NamedPipe; quando o usuario
    /// clica "Inserir" no AcervoBIM, o JS invoca a bridge, a bridge envia
    /// IPC, e o <see cref="ImportFamilyDispatcher"/> deste lado dispara o
    /// mesmo <see cref="ImportFamilyExternalEvent"/> de sempre.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ShowFamiliesPaneCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            string outerMessage = message;
            Result result = RevitCommandExecutor.Current!.Execute(commandData, ref outerMessage, _ =>
            {
                try
                {
                    // Operacao sincrona do ponto de vista do comando: o
                    // EnsureRunningAsync apenas dispara o Process.Start +
                    // garante que o pipe server esta no ar; ambos sao
                    // rapidissimos. O await em si nao espera o sidecar
                    // terminar de carregar a UI.
                    SidecarHost.Instance.EnsureRunningAsync().GetAwaiter().GetResult();
                    return Result.Succeeded;
                }
                catch (System.Exception ex)
                {
                    TaskDialog.Show(
                        "Importar Famílias",
                        "Nao foi possivel iniciar o navegador de familias.\n\n" +
                        "Detalhes: " + ex.Message);
                    return Result.Failed;
                }
            });

            message = outerMessage;
            return result;
        }
    }
}
