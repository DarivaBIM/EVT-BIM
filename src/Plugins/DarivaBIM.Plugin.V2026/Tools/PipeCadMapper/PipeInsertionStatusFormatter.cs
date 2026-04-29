using DarivaBIM.Presentation.Wpf.PipeConverter;
using DarivaBIM.Revit.Adapters.V2026.Writers;

namespace DarivaBIM.Plugin.V2026.Tools.PipeCadMapper
{
    /// <summary>
    /// Formats the user-facing status line shown after a PipeCADMapper run.
    /// Pulled out of <c>PipeInsertionHandler</c> to keep the external event
    /// focused on lifecycle (pick + transaction + rearm) rather than copy.
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
