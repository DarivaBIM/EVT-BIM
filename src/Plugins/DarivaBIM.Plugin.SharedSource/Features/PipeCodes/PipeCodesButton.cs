using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.Features.PipeCodes
{
    public static class PipeCodesButton
    {
        public static RibbonButtonDefinition Definition => new RibbonButtonDefinition(
            internalName: "PipeCodes",
            text: "Codificar\nTubos",
            commandId: RibbonCommandId.WritePipeCodes,
            toolTip: "Atribui o parâmetro 'Tigre: Código' a cada tubo conforme descrição/segmento e diâmetro.",
            longDescription:
                "Garante o shared parameter 'Tigre: Código' como instância na categoria " +
                "Tubulações e percorre todos os tubos do projeto preenchendo o código " +
                "Tigre correspondente, com base no catálogo embutido (descrição + diâmetro).",
            helpUrl: null,
            largeIconResource: "Ribbon/Resources/Icons/pipe_codes_32.png",
            smallIconResource: "Ribbon/Resources/Icons/pipe_codes_16.png",
            licenseRequirement: LicenseRequirement.Free);
    }
}
