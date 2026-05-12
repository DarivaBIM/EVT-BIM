using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using DarivaBIM.Application.Contracts.UtilizationPoints;
using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Domain.Hydraulics.UtilizationPoints;
using DarivaBIM.Revit.Adapters.Common.Units;

namespace DarivaBIM.Revit.Adapters.Features.UtilizationPoints
{
    /// <summary>
    /// Implementação Revit-side de
    /// <see cref="IUtilizationPointInsertionService"/>. Recebe os IDs já
    /// selecionados na UI, ativa os tipos referenciados nas regras do grupo
    /// ativo, e percorre os conectores livres aplicando o algoritmo do
    /// script Python de referência (height-band → place → orient → connect).
    /// Toda a inserção ocorre em uma única <see cref="Transaction"/>.
    /// </summary>
    public sealed class RevitUtilizationPointInsertionService : IUtilizationPointInsertionService
    {
        private const string TransactionName = "Inserir Pontos de Utilização";

        private readonly Document _doc;

        public RevitUtilizationPointInsertionService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public InsertionSummaryDto Insert(
            IReadOnlyList<long> elementIds,
            UtilizationPointGroup group,
            long? referenceLevelId)
        {
            InsertionSummaryDto summary = new();

            if (elementIds == null || elementIds.Count == 0)
            {
                summary.Messages.Add(new InsertionMessageDto(
                    UtilizationPointInsertionOutcome.ElementWithoutFreeConnectors,
                    "Nenhum elemento informado."));
                return summary;
            }

            if (group == null || !group.HasAnyValidRule)
            {
                summary.Messages.Add(new InsertionMessageDto(
                    UtilizationPointInsertionOutcome.FamilyMissing,
                    "O grupo ativo não tem regras válidas para usar."));
                return summary;
            }

            Level? userLevel = ResolveUserLevel(referenceLevelId);

            using Transaction tx = new(_doc, TransactionName);
            tx.Start();

            try
            {
                Dictionary<FamilyTypeReference, FamilySymbol?> resolvedSymbols = ResolveSymbolsForGroup(group);
                ActivateResolvedSymbols(resolvedSymbols.Values);

                foreach (long elementId in elementIds)
                {
                    Element? element;
                    try { element = _doc.GetElement(new ElementId(elementId)); }
                    catch { element = null; }

                    if (element == null) continue;

                    summary.ElementsAnalyzed++;
                    ProcessElement(element, group, resolvedSymbols, userLevel, summary);
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                summary.Errors++;
                summary.Messages.Add(new InsertionMessageDto(
                    UtilizationPointInsertionOutcome.CreationError,
                    $"Erro inesperado durante a inserção: {ex.Message}"));
            }

            return summary;
        }

        private Level? ResolveUserLevel(long? referenceLevelId)
        {
            if (!referenceLevelId.HasValue) return null;
            try
            {
                return _doc.GetElement(new ElementId(referenceLevelId.Value)) as Level;
            }
            catch
            {
                return null;
            }
        }

        private Dictionary<FamilyTypeReference, FamilySymbol?> ResolveSymbolsForGroup(UtilizationPointGroup group)
        {
            Dictionary<FamilyTypeReference, FamilySymbol?> map = new();
            for (int i = 0; i < group.Rules.Count; i++)
            {
                FamilyTypeReference reference = group.Rules[i].FamilyType;
                if (map.ContainsKey(reference)) continue;
                map[reference] = RevitFamilyTypeResolver.Resolve(_doc, reference);
            }
            return map;
        }

        private void ActivateResolvedSymbols(IEnumerable<FamilySymbol?> symbols)
        {
            foreach (FamilySymbol? symbol in symbols)
            {
                if (symbol == null) continue;
                try
                {
                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                        _doc.Regenerate();
                    }
                }
                catch
                {
                    // Falha ao ativar — será reportada por elemento mais tarde.
                }
            }
        }

        private void ProcessElement(
            Element element,
            UtilizationPointGroup group,
            IReadOnlyDictionary<FamilyTypeReference, FamilySymbol?> resolvedSymbols,
            Level? userLevel,
            InsertionSummaryDto summary)
        {
            IReadOnlyList<Autodesk.Revit.DB.Connector> freeConnectors =
                RevitConnectorUtilities.GetFreeConnectors(element);

            if (freeConnectors.Count == 0)
            {
                summary.Messages.Add(new InsertionMessageDto(
                    UtilizationPointInsertionOutcome.ElementWithoutFreeConnectors,
                    $"Elemento {element.Id.Value} sem conectores livres."));
                return;
            }

            RevitReferenceLevelResolver.LevelResolution levelResolution =
                RevitReferenceLevelResolver.Resolve(_doc, element, userLevel);

            for (int i = 0; i < freeConnectors.Count; i++)
            {
                summary.FreeConnectorsFound++;
                ProcessConnector(
                    element,
                    freeConnectors[i],
                    levelResolution,
                    group,
                    resolvedSymbols,
                    summary);
            }
        }

        private void ProcessConnector(
            Element element,
            Autodesk.Revit.DB.Connector connector,
            RevitReferenceLevelResolver.LevelResolution levelResolution,
            UtilizationPointGroup group,
            IReadOnlyDictionary<FamilyTypeReference, FamilySymbol?> resolvedSymbols,
            InsertionSummaryDto summary)
        {
            double heightMeters;
            try
            {
                double heightFeet = connector.Origin.Z - levelResolution.ElevationFeet;
                heightMeters = RevitUnitConverter.FeetToMeters(heightFeet);
            }
            catch (Exception ex)
            {
                summary.Errors++;
                summary.Messages.Add(new InsertionMessageDto(
                    UtilizationPointInsertionOutcome.CreationError,
                    $"Falha ao calcular altura do conector em {element.Id.Value}: {ex.Message}"));
                return;
            }

            UtilizationPointRule? rule = group.FindRuleForHeight(heightMeters);
            if (rule == null)
            {
                summary.ConnectorsWithoutRange++;
                summary.Messages.Add(new InsertionMessageDto(
                    UtilizationPointInsertionOutcome.NoMatchingRange,
                    $"Conector do elemento {element.Id.Value} a {heightMeters:0.000} m: sem faixa compatível."));
                return;
            }

            if (!resolvedSymbols.TryGetValue(rule.FamilyType, out FamilySymbol? symbol) || symbol == null)
            {
                summary.Errors++;
                summary.Messages.Add(new InsertionMessageDto(
                    UtilizationPointInsertionOutcome.FamilyMissing,
                    $"Regra '{rule.FamilyType}' aponta para um tipo de família não encontrado no documento."));
                return;
            }

            RevitFamilyInstancePlacementService.PlacementResult result =
                RevitFamilyInstancePlacementService.Place(
                    _doc,
                    symbol,
                    connector,
                    element,
                    levelResolution.Level,
                    autoConnect: true);

            switch (result.Outcome)
            {
                case RevitFamilyInstancePlacementService.PlacementOutcome.CreatedAndConnected:
                    summary.PointsInserted++;
                    summary.PointsConnected++;
                    summary.Messages.Add(new InsertionMessageDto(
                        UtilizationPointInsertionOutcome.InsertedAndConnected,
                        $"{rule.FamilyType} inserido a {heightMeters:0.000} m no elemento {element.Id.Value}."));
                    break;

                case RevitFamilyInstancePlacementService.PlacementOutcome.CreatedNotConnected:
                case RevitFamilyInstancePlacementService.PlacementOutcome.NoFreeConnectorInFamily:
                    summary.PointsInserted++;
                    summary.Messages.Add(new InsertionMessageDto(
                        UtilizationPointInsertionOutcome.InsertedNotConnected,
                        $"{rule.FamilyType} a {heightMeters:0.000} m no elemento {element.Id.Value}: {result.Message}"));
                    break;

                case RevitFamilyInstancePlacementService.PlacementOutcome.CreationFailed:
                default:
                    summary.Errors++;
                    summary.Messages.Add(new InsertionMessageDto(
                        UtilizationPointInsertionOutcome.CreationError,
                        $"Falha ao inserir '{rule.FamilyType}' no elemento {element.Id.Value}: {result.Message}"));
                    break;
            }
        }
    }
}
