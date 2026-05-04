using DarivaBIM.Presentation.Wpf.Models;
using DarivaBIM.Presentation.Wpf.PipeConverter;
#if REVIT2026
using DarivaBIM.Revit.Adapters.V2026.Features.PipeCadMapper;
#elif REVIT2025
using DarivaBIM.Revit.Adapters.V2025.Features.PipeCadMapper;
#endif

namespace DarivaBIM.Plugin.Features.PipeCadMapper.Tools
{
    /// <summary>
    /// Constrói um <see cref="PipeConversionConfig"/> (lado Adapter,
    /// dependente da RevitAPI) a partir do <see cref="PipeConverterViewModel"/>
    /// neutro (Presentation.Wpf, sem RevitAPI). A conversão dos ids
    /// <c>long</c> de volta para <see cref="Autodesk.Revit.DB.ElementId"/>
    /// acontece aqui para o handler ficar focado no ciclo
    /// pick/transaction.
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
