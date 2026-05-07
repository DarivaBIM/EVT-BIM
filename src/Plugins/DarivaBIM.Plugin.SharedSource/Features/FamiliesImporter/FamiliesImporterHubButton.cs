using DarivaBIM.Application.Common;
using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.Features.FamiliesImporter
{
    public static class FamiliesImporterHubButton
    {
        public static RibbonButtonDefinition Definition => new RibbonButtonDefinition(
            internalName: FeatureNames.FamiliesImporter,
            text: "Biblioteca\nTigre",
            commandId: RibbonCommandId.ShowFamiliesPane,
            toolTip: "Abre o painel de importação de famílias da Tigre.",
            longDescription: "Abre um painel lateral no Revit para listar e importar famílias.",
            helpUrl: null,
            largeIconResource: "Ribbon/Resources/Icons/families_importer_hub_32.png",
            smallIconResource: "Ribbon/Resources/Icons/families_importer_hub_16.png",
            licenseRequirement: LicenseRequirement.Free);
    }
}
