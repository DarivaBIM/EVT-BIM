using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Revit.Adapters.Common.SharedParameters;
using DarivaBIM.Revit.Adapters.Common.Transactions;

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
                SharedParameterEnsureResult ensure = RevitTransactionRunner.Run(
                    _doc,
                    "Tigre — Criar parâmetro 'Tigre: Código' nos tubos",
                    () => SharedParameterService.Ensure(_doc, TigreCodesSharedParameters.Code));

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
    }
}
