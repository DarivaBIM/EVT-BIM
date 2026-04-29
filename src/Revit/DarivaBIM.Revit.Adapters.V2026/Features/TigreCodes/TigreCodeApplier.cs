using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Revit.Adapters.V2026.Common.SharedParameters;
using DarivaBIM.Revit.Adapters.V2026.Features.TigreCodes.SharedParameters;

namespace DarivaBIM.Revit.Adapters.V2026.Features.TigreCodes
{
    /// <summary>
    /// Implementação Revit 2026 de <see cref="ITigreCodeApplyService"/>.
    /// Orquestra o fluxo do script Dynamo:
    ///
    /// 1. Garante o shared parameter declarado em
    ///    <see cref="TigreCodesSharedParameters.Code"/> via
    ///    <see cref="SharedParameterService"/>.
    /// 2. Coleta todos os tubos do projeto
    ///    (<see cref="TigrePipeCollector"/>).
    /// 3. Para cada tubo, lê descrição/segmento/tipo/diâmetro
    ///    (<see cref="TigrePipeDataReader"/>) e procura o match no
    ///    <see cref="TigreCatalog"/>.
    /// 4. Escreve o código no parâmetro alvo
    ///    (<see cref="TigreCodeWriter"/>).
    /// 5. Preenche o relatório (<see cref="TigreCodeApplyResult"/>).
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

        public TigreCodeApplyResult Apply()
        {
            TigreCodeApplyResult report = new TigreCodeApplyResult();
            TigreCatalog catalog = _catalogProvider.Load();
            report.CatalogCount = catalog.Entries.Count;

            if (catalog.Entries.Count == 0)
                throw new InvalidOperationException("Catálogo Tigre vazio.");

            ExecuteInWriteTransaction(_doc, "Tigre — Aplicar códigos nos tubos", () =>
            {
                SharedParameterEnsureResult ensure =
                    SharedParameterService.Ensure(_doc, TigreCodesSharedParameters.Code);
                report.ParameterAction = ensure.Action;
                report.Warnings.AddRange(ensure.Warnings);

                _doc.Regenerate();

                IList<Pipe> pipes = TigrePipeCollector.CollectPipes(_doc);
                report.PipesTotal = pipes.Count;

                foreach (Pipe pipe in pipes)
                    ProcessPipe(pipe, catalog, report);
            });

            return report;
        }

        private static void ExecuteInWriteTransaction(Document doc, string transactionName, Action action)
        {
            if (doc.IsModifiable)
            {
                action();
                return;
            }

            using Transaction tx = new Transaction(doc, transactionName);
            TransactionStatus status = tx.Start();
            if (status != TransactionStatus.Started)
                throw new InvalidOperationException("Não foi possível abrir transação para aplicar os códigos Tigre.");

            action();
            tx.Commit();
        }

        private void ProcessPipe(Pipe pipe, TigreCatalog catalog, TigreCodeApplyResult report)
        {
            TigrePipeData data = TigrePipeDataReader.Read(_doc, pipe);
            string combined = TigreTextUtils.Normalize($"{data.Description} {data.Segment} {data.TypeName}");

            if (!data.DiameterMm.HasValue)
            {
                RegisterNoMatch(report, pipe, null, data);
                return;
            }

            TigreCatalogEntry? match = catalog.FindMatch(
                data.Description, data.Segment, data.TypeName, combined, data.DiameterMm.Value);
            if (match == null)
            {
                RegisterNoMatch(report, pipe, data.DiameterMm, data);
                return;
            }

            Parameter? target = SharedParameterService.GetParameter(pipe, TigreCodesSharedParameters.Code);
            if (target == null || target.IsReadOnly)
            {
                report.PipesParameterIssue++;
                return;
            }

            TigreWriteOutcome outcome = TigreCodeWriter.Write(target, match.Code);
            switch (outcome)
            {
                case TigreWriteOutcome.AlreadyOk:
                    report.PipesAlreadyOk++;
                    report.PipesUpdated++;
                    break;
                case TigreWriteOutcome.Overwritten:
                    report.PipesOverwritten++;
                    report.PipesUpdated++;
                    break;
                case TigreWriteOutcome.ParameterIssue:
                    report.PipesParameterIssue++;
                    break;
            }
        }

        private static void RegisterNoMatch(TigreCodeApplyResult report, Pipe pipe, int? diaMm, TigrePipeData data)
        {
            report.PipesNoMatch++;
            report.Unmatched.Add(new UnmatchedPipe
            {
                ElementId = pipe.Id.Value,
                DiameterMm = diaMm,
                Description = data.Description,
                Segment = data.Segment,
                TypeName = data.TypeName,
            });
        }
    }
}
