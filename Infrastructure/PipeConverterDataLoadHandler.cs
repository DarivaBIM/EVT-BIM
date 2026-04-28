using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using FamiliesImporterHub.UI;

namespace FamiliesImporterHub.Infrastructure
{
    /// <summary>
    /// Carrega sistemas, tipos de tubo, diâmetros e níveis disponíveis no
    /// documento ativo e popula o <see cref="PipeConverterViewModel"/>.
    /// </summary>
    public class PipeConverterDataLoadHandler : IExternalEventHandler
    {
        public PipeConverterViewModel? ViewModel { get; set; }

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

                List<PipingSystemOption> systems = new FilteredElementCollector(doc)
                    .OfClass(typeof(PipingSystemType))
                    .Cast<PipingSystemType>()
                    .OrderBy(s => s.Name)
                    .Select(s => new PipingSystemOption(s.Id, s.Name))
                    .ToList();

                List<PipeTypeOption> pipeTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(PipeType))
                    .Cast<PipeType>()
                    .OrderBy(t => t.Name)
                    .Select(t => new PipeTypeOption(t.Id, t.Name, GetAvailableDiametersMm(doc, t)))
                    .ToList();

                List<LevelOption> levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .Select(l => new LevelOption(l.Id, l.Name, l.Elevation))
                    .ToList();

                PopulateViewModel(vm, systems, pipeTypes, levels);

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

        private static void PopulateViewModel(
            PipeConverterViewModel vm,
            IReadOnlyList<PipingSystemOption> systems,
            IReadOnlyList<PipeTypeOption> pipeTypes,
            IReadOnlyList<LevelOption> levels)
        {
            vm.Systems.Clear();
            foreach (PipingSystemOption s in systems)
            {
                vm.Systems.Add(s);
            }

            vm.PipeTypes.Clear();
            foreach (PipeTypeOption t in pipeTypes)
            {
                vm.PipeTypes.Add(t);
            }

            vm.Levels.Clear();
            foreach (LevelOption l in levels)
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
