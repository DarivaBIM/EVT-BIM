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
                "Abre uma janela para informar o comprimento (em metros) e selecionar " +
                "as caixas no projeto. Para cada caixa, busca o conector vertical e cria " +
                "um tubo vertical com diâmetro herdado do conector e tipo coerente com o " +
                "material (Redux/Reforçada/Série Normal).",
            helpUrl: null,
            largeIconResource: "Ribbon/Resources/Icons/floor_drain_extension_32.png",
            smallIconResource: "Ribbon/Resources/Icons/floor_drain_extension_16.png",
            licenseRequirement: LicenseRequirement.Free);
    }
}
