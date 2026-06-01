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
    /// Implementação Revit-side de <see cref="ITigreCodeClearService"/>.
    /// Zera o valor do parâmetro Tigre: Código nos elementos selecionados —
    /// Integer → 0; String → vazio. Não remove o binding.
    ///
    /// Slice 3.6 (Codex blocker) — Cleaner amplia escopo de Pipes-only
    /// pras 4 categorias Tigre, espelhando o Applier do Slice 3.3.
    /// Estratégia dual-path: instance → type → skip. Não usa detector
    /// porque o usuário já selecionou os IDs no scan anterior — o Cleaner
    /// só apaga o que foi marcado.
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

            RevitTransactionRunner.Run(_doc, "Tigre — Apagar códigos", () =>
            {
                foreach (long rawId in elementIds)
                {
                    Element? el = _doc.GetElement(new ElementId(rawId));
                    if (el == null) continue;

                    ProcessElement(el,
                        ref cleared, ref alreadyEmpty, ref paramIssue);
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

        private void ProcessElement(
            Element element,
            ref int cleared,
            ref int alreadyEmpty,
            ref int paramIssue)
        {
            // (a) instance — preferencial sempre que disponível
            Parameter? targetInstance = SharedParameterAccessor.GetParameter(
                element, TigreCodesSharedParameters.Code);
            if (targetInstance != null && !targetInstance.IsReadOnly)
            {
                TryClear(targetInstance, ref cleared, ref alreadyEmpty, ref paramIssue);
                return;
            }

            // (b) fallback type — famílias catálogo Tigre (param mora no type)
            ElementId typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                Element? type = _doc.GetElement(typeId);
                if (type != null)
                {
                    Parameter? targetType = SharedParameterAccessor.GetParameter(
                        type, TigreCodesSharedParameters.Code);
                    if (targetType != null && !targetType.IsReadOnly)
                    {
                        TryClear(targetType, ref cleared, ref alreadyEmpty, ref paramIssue);
                        return;
                    }
                }
            }

            // (c) sem param acessível — Cleaner é mais silencioso que
            // Applier (sem TigreApplyIssue), apenas conta.
            paramIssue++;
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
    }
}
