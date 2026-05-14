using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using DarivaBIM.Infrastructure.Persistence.Settings;
using DarivaBIM.Plugin.Features.PipeCadMapper.Tools;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Presentation.Wpf.FloorDrainExtension;
using DarivaBIM.Presentation.Wpf.Models;
using DarivaBIM.Revit.Adapters.Common.Pipes;
using DarivaBIM.Revit.Adapters.Features.FloorDrainExtension;

namespace DarivaBIM.Plugin.Features.FloorDrainExtension
{
    /// <summary>
    /// External event que percorre o projeto ativo, descobre os tipos de
    /// caixa sifonada/seca já inseridos, infere o diâmetro do conector
    /// vertical de cada tipo, filtra os <see cref="PipeType"/> compatíveis
    /// e popula a janela com um grupo por tipo de caixa. Reaplica também
    /// a preferência persistida (se houver) ao terminar o scan.
    /// </summary>
    public class FloorDrainExtensionScanExternalEvent
    {
        private readonly FloorDrainExtensionScanHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public FloorDrainExtensionScanExternalEvent()
        {
            _handler = new FloorDrainExtensionScanHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(
            FloorDrainExtensionWindow window,
            FloorDrainExtensionViewModel viewModel,
            FloorDrainExtensionSettings settings)
        {
            _handler.Window = window;
            _handler.ViewModel = viewModel;
            _handler.Settings = settings;
            _externalEvent.Raise();
        }
    }

    internal class FloorDrainExtensionScanHandler : IExternalEventHandler
    {
        public FloorDrainExtensionWindow? Window { get; set; }
        public FloorDrainExtensionViewModel? ViewModel { get; set; }
        public FloorDrainExtensionSettings? Settings { get; set; }

        public string GetName() => "EvtBim.FloorDrainExtensionScanHandler";

        public void Execute(UIApplication app)
        {
            FloorDrainExtensionWindow? window = Window;
            FloorDrainExtensionViewModel? vm = ViewModel;
            if (window == null || vm == null)
                return;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    window.ApplyScanResult(Array.Empty<FloorDrainBoxGroupViewModel>(),
                        "Abra um projeto Revit para usar a ferramenta.");
                    return;
                }

                Document doc = uiDoc.Document;

                List<FamilyInstance> boxes = FloorDrainExtensionBoxScanner.CollectAllBoxes(doc);
                List<FloorDrainExtensionBoxScanner.BoxGroup> groups =
                    FloorDrainExtensionBoxScanner.GroupBySymbol(boxes);

                if (groups.Count == 0)
                {
                    window.ApplyScanResult(Array.Empty<FloorDrainBoxGroupViewModel>(),
                        "Nenhuma caixa sifonada/seca encontrada no projeto.");
                    return;
                }

                PipeDiameterDiscoveryCache diameterCache = new(doc);
                List<FloorDrainBoxGroupViewModel> groupViewModels = new();
                foreach (FloorDrainExtensionBoxScanner.BoxGroup g in groups)
                {
                    FloorDrainBoxGroupViewModel gvm = BuildGroupViewModel(doc, g, diameterCache);
                    groupViewModels.Add(gvm);
                }

                ApplyPersistedPreferences(groupViewModels, Settings);

                string status = BuildStatusMessage(groupViewModels);
                window.ApplyScanResult(groupViewModels, status);
            }
            catch (Exception ex)
            {
                window.ApplyScanResult(Array.Empty<FloorDrainBoxGroupViewModel>(),
                    $"Falha ao varrer o projeto: {ex.Message}");
            }
        }

        private static FloorDrainBoxGroupViewModel BuildGroupViewModel(
            Document doc,
            FloorDrainExtensionBoxScanner.BoxGroup g,
            PipeDiameterDiscoveryCache diameterCache)
        {
            long symbolIdHint = RevitElementIdConversions.ToLong(g.Symbol.Id);
            FloorDrainBoxGroupViewModel gvm = new(symbolIdHint, g.FamilyName, g.SymbolName, g.DiameterMm);

            List<PipeType> compatible = FloorDrainExtensionPipeTypeResolver
                .FindPipeTypesForDiameter(doc, g.DiameterMm, g.MaterialKind, diameterCache);

            foreach (PipeType pt in compatible)
            {
                IReadOnlyList<double> diameters = diameterCache.GetAvailableDiametersMm(pt);

                gvm.PipeTypes.Add(new PipeTypeOptionViewModel(
                    RevitElementIdConversions.ToLong(pt.Id),
                    pt.Name,
                    diameters));
            }

            if (gvm.PipeTypes.Count > 0)
            {
                // O FindPipeTypesForDiameter já devolve o preferido em
                // primeira posição quando disponível.
                gvm.SelectedPipeType = gvm.PipeTypes[0];
            }

            // Popula instâncias individuais (todas marcadas por padrão). O
            // usuário pode desmarcar caixas específicas no card expansível
            // antes de clicar em "Todas do projeto" / "Visíveis na vista".
            // O modo "Selecionar caixas" (pick interativo) ignora essa lista
            // — quem manda lá é o que o usuário pega no PickObjects.
            for (int i = 0; i < g.Instances.Count; i++)
            {
                FamilyInstance fi = g.Instances[i];
                long instanceId = RevitElementIdConversions.ToLong(fi.Id);
                string label = $"Caixa #{i + 1} · ID {instanceId}";
                gvm.Instances.Add(new FloorDrainBoxInstanceViewModel(instanceId, label));
            }

            return gvm;
        }

        private static void ApplyPersistedPreferences(
            IReadOnlyList<FloorDrainBoxGroupViewModel> groups,
            FloorDrainExtensionSettings? settings)
        {
            if (settings == null)
                return;

            foreach (FloorDrainBoxGroupViewModel g in groups)
            {
                string? saved = settings.GetPipeTypeName(g.FamilyName, g.SymbolName);
                if (string.IsNullOrEmpty(saved))
                    continue;

                PipeTypeOptionViewModel? match = g.PipeTypes
                    .FirstOrDefault(p => string.Equals(p.Name, saved, StringComparison.Ordinal));

                if (match != null)
                    g.SelectedPipeType = match;
            }
        }

        private static string BuildStatusMessage(IReadOnlyList<FloorDrainBoxGroupViewModel> groups)
        {
            int withTubes = groups.Count(g => g.HasPipeTypes);
            int withoutTubes = groups.Count - withTubes;

            if (groups.Count == 0)
                return "Nenhuma caixa sifonada/seca encontrada.";

            if (withoutTubes == 0)
                return $"{groups.Count} tipo(s) de caixa carregado(s). Defina os tubos e escolha onde inserir.";

            return
                $"{groups.Count} tipo(s) de caixa, {withoutTubes} sem tubo compatível no projeto " +
                $"(verifique RoutingPreferenceManager).";
        }
    }
}
