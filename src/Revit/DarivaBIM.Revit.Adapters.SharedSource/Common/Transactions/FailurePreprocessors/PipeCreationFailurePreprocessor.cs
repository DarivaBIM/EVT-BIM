using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Common.Transactions.FailurePreprocessors
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

        // Configuração padrão usada por todos os fluxos do PipeCadMapper que
        // criam tubos/marcadores: registra o preprocessor, limpa avisos no
        // rollback e desabilita modais. Chamar antes de <c>tx.Start()</c>.
        public static void Attach(Transaction tx)
        {
            FailureHandlingOptions options = tx.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new PipeCreationFailurePreprocessor());
            options.SetClearAfterRollback(true);
            options.SetForcedModalHandling(false);
            tx.SetFailureHandlingOptions(options);
        }
    }
}
