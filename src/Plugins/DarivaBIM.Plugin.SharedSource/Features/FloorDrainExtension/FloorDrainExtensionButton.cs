using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.Features.FloorDrainExtension
{
    public static class FloorDrainExtensionButton
    {
        public static RibbonButtonDefinition Definition => new RibbonButtonDefinition(
            internalName: "FloorDrainExtension",
            text: "Adicionar\nProlongadores",
            commandId: RibbonCommandId.OpenFloorDrainExtension,
            toolTip: "Cria prolongadores (tubos verticais) acima de caixas sifonadas/secas.",
            longDescription:
                "Abre uma janela com a lista de tipos de caixa sifonada/seca já inseridos " +
                "no projeto. Para cada tipo, escolha o tipo de tubo que deseja usar como " +
                "prolongador (o dropdown mostra apenas tubos compatíveis com o diâmetro " +
                "do conector vertical da caixa). Defina o comprimento e use um dos três " +
                "botões: selecionar caixas no projeto, todas as caixas do projeto ou " +
                "apenas as visíveis na vista ativa. As preferências escolhidas ficam " +
                "salvas para a próxima sessão.",
            helpUrl: null,
            largeIconResource: "Ribbon/Resources/Icons/floor_drain_extension_32.png",
            smallIconResource: "Ribbon/Resources/Icons/floor_drain_extension_16.png",
            licenseRequirement: LicenseRequirement.Free);
    }
}
