using DarivaBIM.Presentation.Wpf.Models;
using DarivaBIM.Presentation.Wpf.PipeConverter;
using DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Markers;

namespace DarivaBIM.Plugin.Features.PipeCadMapper.Tools
{
    /// <summary>
    /// Constrói um <see cref="PipeMarkerConfig"/> a partir do view-model
    /// neutro. A conversão dos IDs <c>long</c> para
    /// <see cref="Autodesk.Revit.DB.ElementId"/> acontece aqui, deixando o
    /// handler focado no ciclo de pick/transaction.
    /// </summary>
    internal static class PipeMarkerConfigFactory
    {
        public static bool TryCreate(
            PipeConverterViewModel vm,
            out PipeMarkerConfig? config,
            out string? error)
        {
            config = null;
            error = null;

            PipingSystemOptionViewModel? system = vm.SelectedSystem;
            PipeTypeOptionViewModel? pipeType = vm.SelectedPipeType;
            double? diameter = vm.SelectedDiameterMm;
            LevelOptionViewModel? level = vm.SelectedLevel;

            if (system == null || pipeType == null || !diameter.HasValue || level == null)
            {
                error = "Configuração incompleta — selecione sistema, tipo, diâmetro e nível.";
                return false;
            }

            config = new PipeMarkerConfig(
                RevitElementIdConversions.ToElementId(system.Id),
                RevitElementIdConversions.ToElementId(pipeType.Id),
                RevitElementIdConversions.ToElementId(level.Id),
                diameter.Value,
                level.ElevationFeet,
                vm.OffsetMm,
                vm.UseCadElevation);

            return true;
        }
    }
}
