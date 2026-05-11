using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.Features.BatchParameterEditor
{
    public static class BatchParameterEditorButton
    {
        public static RibbonButtonDefinition Definition => new RibbonButtonDefinition(
            internalName: "BatchParameterEditor",
            text: "Parâmetros\nem Lote",
            commandId: RibbonCommandId.OpenBatchParameterEditor,
            toolTip: "Atribui um valor a um parâmetro comum a vários elementos (incluindo famílias aninhadas).",
            longDescription:
                "Abre uma janela onde o usuário monta um lote de elementos com botões " +
                "Selecionar/Remover/Limpar (sem depender de Ctrl/Shift do Revit), " +
                "escolhe um parâmetro comum a todos em um dropdown e informa o valor a " +
                "ser atribuído. O valor é propagado para o elemento selecionado e para " +
                "as famílias aninhadas que tiverem o mesmo parâmetro. A janela mostra o " +
                "tipo do parâmetro (texto, número inteiro, decimal etc.) para evitar " +
                "valores incompatíveis e exibe um resumo de sucesso/falhas ao final.",
            helpUrl: null,
            largeIconResource: "Ribbon/Resources/Icons/batch_parameter_editor_32.png",
            smallIconResource: "Ribbon/Resources/Icons/batch_parameter_editor_16.png",
            licenseRequirement: LicenseRequirement.Free);
    }
}
