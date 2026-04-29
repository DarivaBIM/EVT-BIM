using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using DarivaBIM.Infrastructure.Persistence.Settings;
using DarivaBIM.Presentation.Wpf.Models;
using DarivaBIM.Presentation.Wpf.PipeConverter;
using DarivaBIM.Plugin.V2026.Features.PipeCadMapper.Tools;

namespace DarivaBIM.Plugin.V2026.Features.PipeCadMapper
{
    /// <summary>
    /// Carrega sistemas, tipos de tubo, diâmetros e níveis disponíveis no
    /// documento ativo e popula o <see cref="PipeConverterViewModel"/>.
    /// </summary>
    public class PipeConverterDataLoadHandler : IExternalEventHandler
    {
        public PipeConverterViewModel? ViewModel { get; set; }

        /// <summary>
        /// Configurações persistidas a aplicar nas seleções (definidas pela
        /// UI ao abrir a janela). Limpadas após o primeiro uso para que
        /// recarregamentos posteriores não sobrescrevam o estado do usuário.
        /// </summary>
        public PipeCadMapperSettings? PendingSettings { get; set; }

        public void Execute(UIApplication app)
        {
            PipeConverterViewModel? vm = ViewModel;
            if (vm == null)
            {
                return;
            }

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    vm.StatusMessage = "Abra um projeto Revit (não-família) para carregar os dados.";
                    return;
                }

                Document doc = uiDoc.Document;

                List<PipingSystemOptionViewModel> systems = new FilteredElementCollector(doc)
                    .OfClass(typeof(PipingSystemType))
                    .Cast<PipingSystemType>()
                    .OrderBy(s => s.Name)
                    .Select(s => new PipingSystemOptionViewModel(RevitElementIdConversions.ToLong(s.Id), s.Name))
                    .ToList();

                List<PipeTypeOptionViewModel> pipeTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(PipeType))
                    .Cast<PipeType>()
                    .OrderBy(t => t.Name)
                    .Select(t => new PipeTypeOptionViewModel(
                        RevitElementIdConversions.ToLong(t.Id),
                        t.Name,
                        GetAvailableDiametersMm(doc, t)))
                    .ToList();

                List<LevelOptionViewModel> levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .Select(l => new LevelOptionViewModel(RevitElementIdConversions.ToLong(l.Id), l.Name, l.Elevation))
                    .ToList();

                PopulateViewModel(vm, systems, pipeTypes, levels);

                ApplyPendingSettings(vm);

                if (vm.Systems.Count == 0 || vm.PipeTypes.Count == 0 || vm.Levels.Count == 0)
                {
                    vm.StatusMessage = "Projeto sem sistemas/tipos/níveis suficientes para criar tubos.";
                }
                else if (string.IsNullOrEmpty(vm.StatusMessage)
                         || vm.StatusMessage.StartsWith("Recarregando"))
                {
                    vm.StatusMessage = "Pronto. Configure os parâmetros e ative a ferramenta.";
                }
            }
            catch (Exception ex)
            {
                vm.StatusMessage = $"Falha ao carregar dados do projeto: {ex.Message}";
            }
        }

        public string GetName() => "TigreBIM.PipeConverterDataLoadHandler";

        private void ApplyPendingSettings(PipeConverterViewModel vm)
        {
            PipeCadMapperSettings? settings = PendingSettings;
            if (settings == null)
                return;

            // Consome o pedido — recarregamentos seguintes (ex.: troca de
            // projeto) não devem reaplicar o snapshot inicial por cima do que
            // o usuário tiver mexido.
            PendingSettings = null;

            if (!string.IsNullOrEmpty(settings.SystemName))
            {
                foreach (PipingSystemOptionViewModel option in vm.Systems)
                {
                    if (string.Equals(option.Name, settings.SystemName, StringComparison.Ordinal))
                    {
                        vm.SelectedSystem = option;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(settings.PipeTypeName))
            {
                foreach (PipeTypeOptionViewModel option in vm.PipeTypes)
                {
                    if (string.Equals(option.Name, settings.PipeTypeName, StringComparison.Ordinal))
                    {
                        vm.SelectedPipeType = option;
                        break;
                    }
                }
            }

            if (settings.DiameterMm.HasValue)
            {
                foreach (double d in vm.Diameters)
                {
                    if (Math.Abs(d - settings.DiameterMm.Value) < 0.001)
                    {
                        vm.SelectedDiameterMm = d;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(settings.LevelName))
            {
                foreach (LevelOptionViewModel option in vm.Levels)
                {
                    if (string.Equals(option.Name, settings.LevelName, StringComparison.Ordinal))
                    {
                        vm.SelectedLevel = option;
                        break;
                    }
                }
            }

            vm.OffsetMm = settings.OffsetMm;
        }

        private static void PopulateViewModel(
            PipeConverterViewModel vm,
            IReadOnlyList<PipingSystemOptionViewModel> systems,
            IReadOnlyList<PipeTypeOptionViewModel> pipeTypes,
            IReadOnlyList<LevelOptionViewModel> levels)
        {
            vm.Systems.Clear();
            foreach (PipingSystemOptionViewModel s in systems)
            {
                vm.Systems.Add(s);
            }

            vm.PipeTypes.Clear();
            foreach (PipeTypeOptionViewModel t in pipeTypes)
            {
                vm.PipeTypes.Add(t);
            }

            vm.Levels.Clear();
            foreach (LevelOptionViewModel l in levels)
            {
                vm.Levels.Add(l);
            }

            if (vm.SelectedSystem == null && vm.Systems.Count > 0)
            {
                vm.SelectedSystem = vm.Systems[0];
            }

            if (vm.SelectedPipeType == null && vm.PipeTypes.Count > 0)
            {
                vm.SelectedPipeType = vm.PipeTypes[0];
            }

            if (vm.SelectedLevel == null && vm.Levels.Count > 0)
            {
                vm.SelectedLevel = vm.Levels[0];
            }
        }

        private static IReadOnlyList<double> GetAvailableDiametersMm(Document doc, PipeType pipeType)
        {
            HashSet<double> diameters = new();

            try
            {
                RoutingPreferenceManager? manager = pipeType.RoutingPreferenceManager;
                if (manager == null)
                {
                    return Array.Empty<double>();
                }

                int count = manager.GetNumberOfRules(RoutingPreferenceRuleGroupType.Segments);
                for (int i = 0; i < count; i++)
                {
                    RoutingPreferenceRule rule = manager.GetRule(RoutingPreferenceRuleGroupType.Segments, i);
                    if (doc.GetElement(rule.MEPPartId) is not Segment segment)
                    {
                        continue;
                    }

                    foreach (MEPSize size in segment.GetSizes())
                    {
                        double mm = UnitUtils.ConvertFromInternalUnits(
                            size.NominalDiameter,
                            UnitTypeId.Millimeters);

                        diameters.Add(Math.Round(mm, 2));
                    }
                }
            }
            catch
            {
                // Tipos sem RoutingPreferenceManager utilizável: devolve lista vazia.
            }

            return diameters.OrderBy(d => d).ToList();
        }
    }
}
