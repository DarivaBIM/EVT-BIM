using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Revit.Adapters.Common.SharedParameters;

namespace DarivaBIM.Revit.Adapters.Features.TigreCodes
{
    /// <summary>
    /// Implementação Revit-side de <see cref="ITigreParameterBindingService"/>.
    /// Garante que o shared parameter Tigre: Código exista no projeto e
    /// esteja vinculado às tubulações como instância. Operação idempotente:
    /// chamar com o binding já correto não tem efeito colateral.
    /// </summary>
    public sealed class TigreParameterBinder : ITigreParameterBindingService
    {
        private readonly Document _doc;

        public TigreParameterBinder(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public TigreEnsureParameterResult Ensure()
        {
            try
            {
                SharedParameterEnsureResult ensure = ExecuteInWriteTransaction(
                    _doc,
                    "Tigre — Criar parâmetro 'Tigre: Código' nos tubos",
                    () => SharedParameterService.Ensure(_doc, TigreCodesSharedParameters.Code));

                _doc.Regenerate();

                return new TigreEnsureParameterResult
                {
                    Action = ensure.Action,
                    Warnings = new List<string>(ensure.Warnings),
                };
            }
            catch (Exception ex)
            {
                return new TigreEnsureParameterResult
                {
                    Action = string.Empty,
                    ErrorMessage = ex.Message,
                };
            }
        }

        private static T ExecuteInWriteTransaction<T>(Document doc, string transactionName, Func<T> action)
        {
            if (doc.IsModifiable)
                return action();

            using Transaction tx = new(doc, transactionName);
            TransactionStatus status = tx.Start();
            if (status != TransactionStatus.Started)
                throw new InvalidOperationException(
                    $"Não foi possível abrir transação '{transactionName}'.");

            T result = action();
            tx.Commit();
            return result;
        }
    }
}
