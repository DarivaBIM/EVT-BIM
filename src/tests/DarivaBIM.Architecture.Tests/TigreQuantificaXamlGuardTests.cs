using System.IO;
using Xunit;

namespace DarivaBIM.Architecture.Tests
{
    /// <summary>
    /// Guards estruturais do <c>TigreQuantificaWindow.xaml</c> pra prevenir
    /// regressões do crash do hotfix 4.3.A.1: clique no botão "Corrigir
    /// agora" disparava bubble pro Button raiz do FindingRowTemplate,
    /// rodando 2 commands no mesmo click (SelectInRevit + CorrigirAgora)
    /// e crashando o Revit com race entre 2 ExternalEvents + 4
    /// ExternalEvent.Create simultâneos.
    ///
    /// Esses scans textuais batem chave-suficiente: se alguém recriar o
    /// padrão Button-dentro-de-Button no FindingRowTemplate, ou tirar o
    /// MouseLeftButtonUp handler que substituiu o Command binding, o
    /// teste quebra.
    /// </summary>
    public class TigreQuantificaXamlGuardTests
    {
        private static string LoadXaml()
        {
            string repoRoot = SourceTreeLocator.FindRepositoryRoot();
            string xamlPath = Path.Combine(
                repoRoot,
                "src",
                "Plugins",
                "DarivaBIM.Plugin.SharedSource",
                "Ui",
                "TigreQuantificaWindow.xaml");
            Assert.True(File.Exists(xamlPath), $"XAML não encontrado em {xamlPath}");
            return File.ReadAllText(xamlPath);
        }

        [Fact]
        public void FindingRowTemplate_root_is_Border_not_Button()
        {
            // Root do FindingRowTemplate deve consumir o style do Border
            // (FindingRowBorder); root como Button (FindingRowButton ou
            // qualquer Button TargetType="Button") reintroduz o crash.
            string xaml = LoadXaml();
            Assert.Contains("Style=\"{StaticResource FindingRowBorder}\"", xaml);
            Assert.DoesNotContain("Style=\"{StaticResource FindingRowButton}\"", xaml);
        }

        [Fact]
        public void FindingRowTemplate_uses_MouseLeftButtonUp_for_selection()
        {
            // A seleção é feita via MouseLeftButtonUp + handler no
            // code-behind, não mais via Command binding no Button raiz.
            string xaml = LoadXaml();
            Assert.Contains("MouseLeftButtonUp=\"OnFindingRowMouseUp\"", xaml);
        }

        [Fact]
        public void CorrigirAgora_button_uses_Click_handler_not_Command_binding()
        {
            // O botão interno "Corrigir agora" precisa usar Click handler
            // (não Command no XAML) pra poder marcar e.Handled=true e
            // cortar o bubble do MouseLeftButtonUp pro Border pai.
            string xaml = LoadXaml();
            Assert.Contains("Click=\"OnCorrigirAgoraClick\"", xaml);
            Assert.DoesNotContain(
                "Command=\"{Binding CorrigirAgoraCommand}\"",
                xaml);
        }
    }
}
