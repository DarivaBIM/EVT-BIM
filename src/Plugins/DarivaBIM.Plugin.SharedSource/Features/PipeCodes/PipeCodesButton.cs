using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.Features.PipeCodes
{
    public static class PipeCodesButton
    {
        public static RibbonButtonDefinition Definition => new RibbonButtonDefinition(
            internalName: "PipeCodes",
            text: "Códigos\nTigre",
            commandId: RibbonCommandId.WritePipeCodes,
            toolTip: "Identifica e aplica códigos Tigre em tubos, conexões, acessórios e peças hidrossanitárias (catálogo de 872 SKUs).",
            longDescription:
                "Códigos Tigre — varre o projeto procurando elementos das famílias Tigre " +
                "(Pipes, Conexões, Acessórios, Aparelhos hidrossanitários) e aplica o código " +
                "de catálogo no parâmetro 'Tigre: Código'. Famílias não-Tigre são ignoradas " +
                "pelo detector de marca. Trabalha sobre seleção do usuário ou projeto inteiro.",
            helpUrl: null,
            largeIconResource: "Ribbon/Resources/Icons/pipe_codes_32.png",
            smallIconResource: "Ribbon/Resources/Icons/pipe_codes_16.png",
            licenseRequirement: LicenseRequirement.Free);
    }
}
