using System.Collections.Generic;
using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.V2026.Ribbon
{
    /// <summary>
    /// Declarative ribbon for the Revit 2026 plugin. Adding a new tool only
    /// requires registering its <see cref="RibbonCommandId"/> in
    /// <see cref="CommandRegistry"/> and listing it here.
    /// </summary>
    public static class DarivaBimRibbonDefinition
    {
        public const string TabName = "TigreBIM";
        public const string PanelName = "Tigre";

        public static RibbonDefinition Build()
        {
            var buttons = new List<RibbonButtonDefinition>
            {
                new RibbonButtonDefinition(
                    internalName: "FamiliesImporterHub",
                    text: "Families\nImporter Hub",
                    commandId: RibbonCommandId.ShowFamiliesPane,
                    toolTip: "Abre o painel de importação de famílias da Tigre.",
                    longDescription: "Abre um painel lateral no Revit para listar e importar famílias."),

                new RibbonButtonDefinition(
                    internalName: "PipeConverter",
                    text: "PipeCADMapper",
                    commandId: RibbonCommandId.OpenPipeConverter,
                    toolTip: "PipeCADMapper — converte linhas de vínculo CAD em tubos Revit com conexões automáticas.",
                    longDescription:
                        "Abre a janela PipeCADMapper para configurar sistema, tipo e diâmetro. " +
                        "O modo de seleção converte linhas de vínculos CAD em tubos e conecta " +
                        "automaticamente segmentos adjacentes e tubos existentes."),

                new RibbonButtonDefinition(
                    internalName: "TigreCodes",
                    text: "Códigos\nTigre",
                    commandId: RibbonCommandId.WriteTigreCodes,
                    toolTip: "Atribui o parâmetro 'Tigre: Código' a cada tubo conforme descrição/segmento e diâmetro.",
                    longDescription:
                        "Garante o shared parameter 'Tigre: Código' como instância na categoria " +
                        "Tubulações e percorre todos os tubos do projeto preenchendo o código " +
                        "Tigre correspondente, com base no catálogo embutido (descrição + diâmetro)."),

                new RibbonButtonDefinition(
                    internalName: "Prolongador",
                    text: "Prolongador\nem caixas",
                    commandId: RibbonCommandId.OpenProlongador,
                    toolTip: "Cria prolongadores (tubos verticais) acima de caixas sifonadas/secas.",
                    longDescription:
                        "Abre uma janela para informar o comprimento (em metros) e selecionar " +
                        "as caixas no projeto. Para cada caixa, busca o conector vertical e cria " +
                        "um tubo vertical com diâmetro herdado do conector e tipo coerente com o " +
                        "material (Redux/Reforçada/Série Normal)."),

                new RibbonButtonDefinition(
                    internalName: "ParameterEditor",
                    text: "Editor de\nParâmetros",
                    commandId: RibbonCommandId.OpenParameterEditor,
                    toolTip: "Atribui um valor a um parâmetro comum a vários elementos (incluindo famílias aninhadas).",
                    longDescription:
                        "Abre uma janela onde o usuário seleciona elementos do projeto (com " +
                        "seleção incremental — Ctrl/Shift+clique e múltiplas rodadas de seleção), " +
                        "escolhe um parâmetro comum a todos em um dropdown e informa o valor a " +
                        "ser atribuído. O valor é propagado para o elemento selecionado e para " +
                        "as famílias aninhadas que tiverem o mesmo parâmetro. A janela mostra o " +
                        "tipo do parâmetro (texto, número inteiro, decimal etc.) para evitar " +
                        "valores incompatíveis e exibe um resumo de sucesso/falhas ao final."),
            };

            var panel = new RibbonPanelDefinition(PanelName, buttons);
            return new RibbonDefinition(TabName, new[] { panel });
        }
    }
}
