using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.Features.UtilizationPoints
{
    public static class UtilizationPointInsertionButton
    {
        public static RibbonButtonDefinition Definition => new RibbonButtonDefinition(
            internalName: "UtilizationPointInsertion",
            text: "Pontos de\nUtilização",
            commandId: RibbonCommandId.OpenUtilizationPointInsertion,
            toolTip: "Insere automaticamente peças hidrossanitárias em conectores livres com base em faixas de altura.",
            longDescription:
                "Configure grupos como Banheiro, Cozinha e Área de serviço, associe tipos de família e " +
                "faixas de altura, e insira os pontos automaticamente nos conectores hidráulicos livres.",
            helpUrl: null,
            largeIconResource: "Ribbon/Resources/Icons/utilization_point_insertion_32.png",
            smallIconResource: "Ribbon/Resources/Icons/utilization_point_insertion_16.png",
            licenseRequirement: LicenseRequirement.Free);
    }
}
