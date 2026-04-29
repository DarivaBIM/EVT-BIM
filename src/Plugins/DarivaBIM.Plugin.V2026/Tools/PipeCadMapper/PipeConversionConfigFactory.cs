using DarivaBIM.Presentation.Wpf.Models;
using DarivaBIM.Presentation.Wpf.PipeConverter;
using DarivaBIM.Revit.Adapters.V2026.Mapping;

namespace DarivaBIM.Plugin.V2026.Tools.PipeCadMapper
{
    /// <summary>
    /// Builds a <see cref="PipeConversionConfig"/> (Adapter-side, RevitAPI
    /// dependent) from the neutral <see cref="PipeConverterViewModel"/>
    /// (Presentation.Wpf, RevitAPI agnostic). Conversion of the long ids back
    /// to <c>ElementId</c> happens here so the handler stays focused on the
    /// pick/transaction lifecycle.
    /// </summary>
    internal static class PipeConversionConfigFactory
    {
        public static bool TryCreate(
            PipeConverterViewModel vm,
            out PipeConversionConfig? config,
            out string? error)
        {
            config = null;
            error = null;

            PipingSystemOptionViewModel? system = vm.SelectedSystem;
            PipeTypeOptionViewModel? pipeType = vm.SelectedPipeType;
            double? diameter = vm.SelectedDiameterMm;
            LevelOptionViewModel? level = vm.SelectedLevel;
            double offsetMm = vm.OffsetMm;

            if (system == null || pipeType == null || !diameter.HasValue || level == null)
            {
                error = "Configuração incompleta — selecione sistema, tipo, diâmetro e nível.";
                return false;
            }

            config = new PipeConversionConfig(
                RevitElementIdConversions.ToElementId(system.Id),
                RevitElementIdConversions.ToElementId(pipeType.Id),
                RevitElementIdConversions.ToElementId(level.Id),
                diameter.Value,
                level.ElevationFeet,
                offsetMm);

            return true;
        }
    }
}
