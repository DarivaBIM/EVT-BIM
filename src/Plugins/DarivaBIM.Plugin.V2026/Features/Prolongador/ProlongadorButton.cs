using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.V2026.Features.Prolongador
{
    public static class ProlongadorButton
    {
        public static RibbonButtonDefinition Definition => new RibbonButtonDefinition(
            internalName: "Prolongador",
            text: "Prolongador\nem caixas",
            commandId: RibbonCommandId.OpenProlongador,
            toolTip: "Cria prolongadores (tubos verticais) acima de caixas sifonadas/secas.",
            longDescription:
                "Abre uma janela para informar o comprimento (em metros) e selecionar " +
                "as caixas no projeto. Para cada caixa, busca o conector vertical e cria " +
                "um tubo vertical com diâmetro herdado do conector e tipo coerente com o " +
                "material (Redux/Reforçada/Série Normal).",
            helpUrl: null,
            largeIconResource: "Ribbon/Resources/Icons/prolongador_32.png",
            smallIconResource: "Ribbon/Resources/Icons/prolongador_16.png",
            licenseRequirement: LicenseRequirement.Free);
    }
}
