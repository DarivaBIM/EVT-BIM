using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Revit.Adapters.Common.SharedParameters;
using DarivaBIM.Revit.Adapters.Common.Transactions;

namespace DarivaBIM.Revit.Adapters.Features.TigreCodes
{
    /// <summary>
    /// Implementação Revit-side de <see cref="ITigreCodeApplyService"/>.
    /// Recebe os IDs marcados pelo usuário no WPF, refaz o match contra o
    /// <see cref="TigreCatalog"/> e grava o código no parâmetro Tigre: Código
    /// dentro de uma única transação. O ensure do shared parameter é
    /// responsabilidade do <see cref="TigreParameterBinder"/> — aqui
    /// assumimos que o binding já existe (caso contrário, contabiliza como
    /// "parameter issue").
    /// </summary>
    public sealed class TigreCodeApplier : ITigreCodeApplyService
    {
        private readonly Document _doc;
        private readonly ITigreCatalogProvider _catalogProvider;

        public TigreCodeApplier(Document doc, ITigreCatalogProvider catalogProvider)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        }

        public TigreSelectiveApplyResult Apply(IReadOnlyList<long> elementIds)
        {
            if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));

            TigreCatalog catalog = _catalogProvider.Load();
            if (catalog.Entries.Count == 0)
            {
                return new TigreSelectiveApplyResult
                {
                    Selected = elementIds.Count,
                    ErrorMessage = "Catálogo Tigre vazio.",
                };
            }

            int totalInProject = TigrePipeCollector.CollectPipes(_doc).Count;

            int inserted = 0;
            int overwritten = 0;
            int alreadyOk = 0;
            int noMatch = 0;
            int paramIssue = 0;

            RevitTransactionRunner.Run(_doc, "Tigre — Inserir/Atualizar códigos", () =>
            {
                foreach (long rawId in elementIds)
                {
                    Element? el = _doc.GetElement(new ElementId(rawId));
                    if (el is not Pipe pipe)
                    {
                        // Tubo deletado entre o scan e o apply — ignora.
                        continue;
                    }

                    ProcessPipe(
                        pipe,
                        catalog,
                        ref inserted,
                        ref overwritten,
                        ref alreadyOk,
                        ref noMatch,
                        ref paramIssue);
                }
            });

            return new TigreSelectiveApplyResult
            {
                CatalogCount = catalog.Entries.Count,
                PipesTotalInProject = totalInProject,
                Selected = elementIds.Count,
                Inserted = inserted,
                Overwritten = overwritten,
                AlreadyOk = alreadyOk,
                NoMatch = noMatch,
                ParameterIssue = paramIssue,
            };
        }

        private void ProcessPipe(
            Pipe pipe,
            TigreCatalog catalog,
            ref int inserted,
            ref int overwritten,
            ref int alreadyOk,
            ref int noMatch,
            ref int paramIssue)
        {
            TigrePipeData data = TigrePipeDataReader.Read(_doc, pipe);

            if (!data.DiameterMm.HasValue)
            {
                noMatch++;
                return;
            }

            string combined = TigreTextUtils.Normalize(
                $"{data.Description} {data.Segment} {data.TypeName}");
            TigreCatalogEntry? match = catalog.FindMatch(
                data.Description, data.Segment, data.TypeName, combined, data.DiameterMm.Value);

            if (match == null)
            {
                noMatch++;
                return;
            }

            Parameter? target = SharedParameterAccessor.GetParameter(pipe, TigreCodesSharedParameters.Code);
            if (target == null || target.IsReadOnly)
            {
                paramIssue++;
                return;
            }

            // Distingue Inserted (vazio → novo valor) de Overwritten (valor
            // pré-existente diferente). Para Integer, "vazio" é zero.
            bool wasEmpty = IsEmptyCode(target);

            TigreWriteOutcome outcome = TigreCodeWriter.Write(target, match.Code);
            switch (outcome)
            {
                case TigreWriteOutcome.AlreadyOk:
                    alreadyOk++;
                    break;
                case TigreWriteOutcome.Overwritten:
                    if (wasEmpty)
                        inserted++;
                    else
                        overwritten++;
                    break;
                case TigreWriteOutcome.ParameterIssue:
                    paramIssue++;
                    break;
            }
        }

        private static bool IsEmptyCode(Parameter param)
        {
            try
            {
                return param.StorageType switch
                {
                    StorageType.Integer => param.AsInteger() == 0,
                    StorageType.String => string.IsNullOrEmpty(param.AsString()),
                    _ => true,
                };
            }
            catch
            {
                return true;
            }
        }

    }
}
