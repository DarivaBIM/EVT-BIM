using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    public static class PipeCadMapperButton
    {
        public static RibbonButtonDefinition Definition => new RibbonButtonDefinition(
            internalName: "PipeCadMapper",
            text: "Converter\nTubos CAD",
            commandId: RibbonCommandId.OpenPipeConverter,
            toolTip: "PipeCADMapper — converte linhas de vínculo CAD em tubos Revit (unifilar e bifilar) via marcadores ajustáveis.",
            longDescription:
                "Abre a janela PipeCADMapper: selecione um vínculo CAD, escolha um layer, defina sistema/tipo/diâmetro/nível " +
                "e modo (unifilar ou bifilar). Crie marcadores (placeholders magenta) clicando linha-a-linha ou em lote — " +
                "no modo bifilar a ferramenta detecta automaticamente o eixo central entre as duas paredes do tubo. " +
                "Ajuste os marcadores na vista e, quando estiver pronto, converta todos em tubos Revit com conexões automáticas.",
            helpUrl: null,
            largeIconResource: "Ribbon/Resources/Icons/pipe_cad_mapper_32.png",
            smallIconResource: "Ribbon/Resources/Icons/pipe_cad_mapper_16.png",
            licenseRequirement: LicenseRequirement.Free);
    }
}
