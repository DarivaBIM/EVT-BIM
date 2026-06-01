using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.Features.TigreQuantifica
{
    /// <summary>
    /// Definição do botão "Tigre Quantifica" na ribbon. Ícone é
    /// <c>null</c> enquanto a arte não chega — passar caminho-placeholder
    /// que não existe quebraria
    /// <c>Architecture.Tests.IconReferencesTests.Every_button_icon_reference_resolves_to_a_real_file</c>.
    /// Quando os PNGs forem adicionados em <c>Resources/Icons/</c>, basta
    /// substituir os dois <c>null</c> aqui pelos caminhos
    /// <c>"Ribbon/Resources/Icons/tigre_quantifica_{16,32}.png"</c>.
    /// </summary>
    public static class TigreQuantificaButton
    {
        public static RibbonButtonDefinition Definition => new RibbonButtonDefinition(
            internalName: "TigreQuantifica",
            text: "Tigre\nQuantifica",
            commandId: RibbonCommandId.OpenTigreQuantifica,
            toolTip: "Lista os elementos do projeto com código Tigre, agrupados por família/tipo/diâmetro, e exporta o relatório de compras em CSV.",
            longDescription:
                "Lê todas as categorias relevantes (tubos, conexões, acessórios, paredes, pisos, " +
                "forros, coberturas, equipamentos), soma quantitativos (un, m, m²) e mostra cada " +
                "linha com Família, Tipo, Diâmetro, Código Tigre, Descrição, Fabricante e Sistema. " +
                "Não altera o projeto — somente leitura.",
            helpUrl: null,
            largeIconResource: null,
            smallIconResource: null,
            licenseRequirement: LicenseRequirement.Free);
    }
}
