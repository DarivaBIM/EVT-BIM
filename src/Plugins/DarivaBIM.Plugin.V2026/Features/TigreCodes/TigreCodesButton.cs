using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.V2026.Features.TigreCodes
{
    public static class TigreCodesButton
    {
        public static RibbonButtonDefinition Definition => new RibbonButtonDefinition(
            internalName: "TigreCodes",
            text: "Códigos\nTigre",
            commandId: RibbonCommandId.WriteTigreCodes,
            toolTip: "Atribui o parâmetro 'Tigre: Código' a cada tubo conforme descrição/segmento e diâmetro.",
            longDescription:
                "Garante o shared parameter 'Tigre: Código' como instância na categoria " +
                "Tubulações e percorre todos os tubos do projeto preenchendo o código " +
                "Tigre correspondente, com base no catálogo embutido (descrição + diâmetro).",
            helpUrl: null,
            largeIconResource: "Ribbon/Resources/Icons/tigre_codes_32.png",
            smallIconResource: "Ribbon/Resources/Icons/tigre_codes_16.png",
            licenseRequirement: LicenseRequirement.Free);
    }
}
