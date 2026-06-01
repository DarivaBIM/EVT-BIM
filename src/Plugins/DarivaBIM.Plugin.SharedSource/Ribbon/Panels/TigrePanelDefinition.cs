using DarivaBIM.Plugin.Features.BatchParameterEditor;
using DarivaBIM.Plugin.Features.FamiliesImporter;
using DarivaBIM.Plugin.Features.FloorDrainExtension;
using DarivaBIM.Plugin.Features.PipeCadMapper;
using DarivaBIM.Plugin.Features.PipeCodes;
using DarivaBIM.Plugin.Features.TigreQuantifica;
using DarivaBIM.Plugin.Features.UtilizationPoints;
using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.Ribbon.Panels
{
    public static class TigrePanelDefinition
    {
        public const string Name = "EVT-BIM";

        public static RibbonPanelDefinition Build()
        {
            // Ordem por fluxo de uso: codificar tubos → quantificar é a
            // sequência natural pra preparar o relatório de compras. Mantém
            // PipeCodes e TigreQuantifica adjacentes pra coerência com o
            // banner "Codificar Tubos antes" (slice 1.6 F1).
            return new RibbonPanelDefinition(Name, new[]
            {
                FamiliesImporterFeature.Button,
                PipeCadMapperFeature.Button,
                PipeCodesFeature.Button,
                TigreQuantificaFeature.Button,
                FloorDrainExtensionFeature.Button,
                BatchParameterEditorFeature.Button,
                UtilizationPointInsertionFeature.Button
            });
        }
    }
}
