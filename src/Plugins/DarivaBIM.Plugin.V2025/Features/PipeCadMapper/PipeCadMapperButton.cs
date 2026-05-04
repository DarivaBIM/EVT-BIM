using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.V2025.Features.PipeCadMapper
{
    public static class PipeCadMapperButton
    {
        public static RibbonButtonDefinition Definition => new RibbonButtonDefinition(
            internalName: "PipeConverter",
            text: "PipeCADMapper",
            commandId: RibbonCommandId.OpenPipeConverter,
            toolTip: "PipeCADMapper — converte linhas de vínculo CAD em tubos Revit com conexões automáticas.",
            longDescription:
                "Abre a janela PipeCADMapper para configurar sistema, tipo e diâmetro. " +
                "O modo de seleção converte linhas de vínculos CAD em tubos e conecta " +
                "automaticamente segmentos adjacentes e tubos existentes.",
            helpUrl: null,
            largeIconResource: "Ribbon/Resources/Icons/pipe_cad_mapper_32.png",
            smallIconResource: "Ribbon/Resources/Icons/pipe_cad_mapper_16.png",
            licenseRequirement: LicenseRequirement.Free);
    }
}
