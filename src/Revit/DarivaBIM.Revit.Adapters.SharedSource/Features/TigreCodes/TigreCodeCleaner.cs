using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Revit.Adapters.Common.SharedParameters;

namespace DarivaBIM.Revit.Adapters.Features.TigreCodes
{
    /// <summary>
    /// Implementação Revit-side de <see cref="ITigreCodeClearService"/>.
    /// Zera o valor do parâmetro Tigre: Código nos tubos selecionados —
    /// Integer → 0; String → vazio. Não remove o binding.
    /// </summary>
    public sealed class TigreCodeCleaner : ITigreCodeClearService
    {
        private readonly Document _doc;

        public TigreCodeCleaner(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public TigreClearResult Clear(IReadOnlyList<long> elementIds)
        {
            if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));

            int cleared = 0;
            int alreadyEmpty = 0;
            int paramIssue = 0;

            ExecuteInWriteTransaction(_doc, "Tigre — Apagar códigos", () =>
            {
                foreach (long rawId in elementIds)
                {
                    Element? el = _doc.GetElement(new ElementId(rawId));
                    if (el is not Pipe pipe)
                        continue;

                    Parameter? target = SharedParameterService.GetParameter(pipe, TigreCodesSharedParameters.Code);
                    if (target == null || target.IsReadOnly)
                    {
                        paramIssue++;
                        continue;
                    }

                    TryClear(target, ref cleared, ref alreadyEmpty, ref paramIssue);
                }
            });

            return new TigreClearResult
            {
                Selected = elementIds.Count,
                Cleared = cleared,
                AlreadyEmpty = alreadyEmpty,
                ParameterIssue = paramIssue,
            };
        }

        private static void TryClear(Parameter param, ref int cleared, ref int alreadyEmpty, ref int paramIssue)
        {
            try
            {
                switch (param.StorageType)
                {
                    case StorageType.Integer:
                    {
                        if (param.AsInteger() == 0)
                            alreadyEmpty++;
                        else
                        {
                            param.Set(0);
                            cleared++;
                        }
                        break;
                    }
                    case StorageType.String:
                    {
                        if (string.IsNullOrEmpty(param.AsString()))
                            alreadyEmpty++;
                        else
                        {
                            param.Set(string.Empty);
                            cleared++;
                        }
                        break;
                    }
                    default:
                        paramIssue++;
                        break;
                }
            }
            catch
            {
                paramIssue++;
            }
        }

        private static void ExecuteInWriteTransaction(Document doc, string transactionName, Action action)
        {
            if (doc.IsModifiable)
            {
                action();
                return;
            }

            using Transaction tx = new(doc, transactionName);
            TransactionStatus status = tx.Start();
            if (status != TransactionStatus.Started)
                throw new InvalidOperationException(
                    "Não foi possível abrir transação para apagar os códigos Tigre.");

            action();
            tx.Commit();
        }
    }
}
