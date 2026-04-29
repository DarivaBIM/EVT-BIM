using Autodesk.Revit.DB;
using DarivaBIM.Revit.Adapters.V2026.Filters;
using DarivaBIM.Revit.Adapters.V2026.Mapping;
using DarivaBIM.Revit.Adapters.V2026.Parameters;
using DarivaBIM.Revit.Adapters.V2026.Transactions;
using DarivaBIM.Revit.Adapters.V2026.Writers;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.Contracts;

namespace DarivaBIM.Revit.Adapters.V2026.Transactions
{
    /// <summary>
    /// Suprime os avisos esperados durante a criação dos placeholders e a
    /// conversão em tubos: "conexão aberta" e "curva do marcador de posição
    /// não é longa o suficiente". O Dynamo descarta esses avisos via seu
    /// preprocessor padrão; o Revit puro só faz isso se registrarmos o
    /// nosso. Erros (severidade <c>DocumentCorruption</c>/<c>Error</c>) são
    /// preservados.
    /// </summary>
    public class PipeCreationFailurePreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            foreach (FailureMessageAccessor failure in failuresAccessor.GetFailureMessages())
            {
                if (failure.GetSeverity() == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(failure);
                }
            }

            return FailureProcessingResult.Continue;
        }
    }
}
