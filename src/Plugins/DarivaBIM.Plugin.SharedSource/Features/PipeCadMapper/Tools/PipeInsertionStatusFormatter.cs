using DarivaBIM.Presentation.Wpf.PipeConverter;
#if REVIT2026
using DarivaBIM.Revit.Adapters.V2026.Features.PipeCadMapper;
#elif REVIT2025
using DarivaBIM.Revit.Adapters.V2025.Features.PipeCadMapper;
#endif

namespace DarivaBIM.Plugin.Features.PipeCadMapper.Tools
{
    /// <summary>
    /// Formata a linha de status mostrada ao usuário depois de uma rodada
    /// do PipeCADMapper. Extraído do <c>PipeInsertionHandler</c> para que o
    /// external event fique focado no ciclo (pick + transaction + rearm).
    /// </summary>
    internal static class PipeInsertionStatusFormatter
    {
        public static string Format(PipeConverterViewModel vm, PipeCreationResult result)
        {
            if (!result.Success)
                return $"Não foi possível criar o tubo: {result.ErrorMessage}";

            string skippedNote = result.SkippedCount > 0
                ? $" ({result.SkippedCount} segmento(s) curto(s) ignorado(s))"
                : string.Empty;

            string arcNote = result.ArcsAsChordCount > 0
                ? $" [{result.ArcsAsChordCount} arco(s) convertido(s) como corda reta]"
                : string.Empty;

            return $"Criado(s) {result.CreatedCount} tubo(s){skippedNote}{arcNote} | " +
                   $"{vm.SelectedPipeType?.Name} Ø{vm.SelectedDiameterMm}mm | " +
                   $"{vm.SelectedLevel?.Name} + {vm.OffsetMm}mm";
        }
    }
}
