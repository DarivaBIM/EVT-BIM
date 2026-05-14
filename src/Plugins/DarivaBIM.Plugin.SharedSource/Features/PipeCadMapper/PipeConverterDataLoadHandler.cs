using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using DarivaBIM.Infrastructure.Persistence.Settings;
using DarivaBIM.Presentation.Wpf.Models;
using DarivaBIM.Presentation.Wpf.PipeConverter;
using DarivaBIM.Plugin.Features.PipeCadMapper.Tools;
using DarivaBIM.Revit.Adapters.Common.Pipes;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Carrega sistemas, tipos de tubo, diâmetros e níveis disponíveis no
    /// documento ativo e popula o <see cref="PipeConverterViewModel"/>.
    /// Layer de CAD não é populado aqui: depende de qual vínculo o usuário
    /// escolher, e é o <see cref="CadLinkPickHandler"/> quem cuida disso.
    /// </summary>
    public class PipeConverterDataLoadHandler : IExternalEventHandler
    {
        public PipeConverterViewModel? ViewModel { get; set; }
        public PipeCadMapperSettings? PendingSettings { get; set; }

        public void Execute(UIApplication app)
        {
            PipeConverterViewModel? vm = ViewModel;
            if (vm == null) return;

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
                        PipeDiameterDiscoveryService.GetAvailableDiametersMm(doc, t)))
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
                    vm.StatusMessage = "Projeto sem sistemas/tipos/níveis suficientes para criar marcadores.";
                }
                else if (!vm.HasCadLink)
                {
                    vm.StatusMessage = "Pronto. Selecione um vínculo CAD para começar.";
                }
            }
            catch (Exception ex)
            {
                vm.StatusMessage = $"Falha ao carregar dados do projeto: {ex.Message}";
            }
        }

        public string GetName() => "EvtBim.PipeConverterDataLoadHandler";

        private void ApplyPendingSettings(PipeConverterViewModel vm)
        {
            PipeCadMapperSettings? settings = PendingSettings;
            if (settings == null) return;

            // Limpa para que reloads posteriores não sobrescrevam o estado do usuário.
            PendingSettings = null;

            if (FindByName(vm.Systems, settings.SystemName, s => s.Name) is { } system)
                vm.SelectedSystem = system;

            if (FindByName(vm.PipeTypes, settings.PipeTypeName, t => t.Name) is { } pipeType)
                vm.SelectedPipeType = pipeType;

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

            if (FindByName(vm.Levels, settings.LevelName, l => l.Name) is { } level)
                vm.SelectedLevel = level;

            vm.OffsetMm = settings.OffsetMm;
            vm.UseCadElevation = settings.UseCadElevation;

            BifilarToleranceLevel toleranceLevel = BifilarToleranceLevel.Medium;
            if (!string.IsNullOrEmpty(settings.ToleranceLevel) &&
                Enum.TryParse<BifilarToleranceLevel>(settings.ToleranceLevel, ignoreCase: true, out BifilarToleranceLevel parsed))
            {
                toleranceLevel = parsed;
            }
            vm.SetToleranceLevel(toleranceLevel);

            if (!string.IsNullOrEmpty(settings.LayerName))
            {
                // Pré-seleciona o layer pelo nome para o caso de o usuário
                // escolher um CAD com a mesma nomenclatura — se o layer
                // não estiver presente no CAD selecionado depois, o pick
                // handler ajusta a seleção.
                vm.SelectedLayer = settings.LayerName;
            }

            if (string.Equals(settings.Mode, nameof(PipeCadMappingMode.Bifilar), StringComparison.OrdinalIgnoreCase))
                vm.Mode = PipeCadMappingMode.Bifilar;
            else
                vm.Mode = PipeCadMappingMode.Unifilar;
        }

        // Helper compartilhado entre o restore de System/PipeType/Level: cada
        // settings persistido guarda o NOME da opção (não o id), porque ids do
        // Revit mudam entre sessões. Comparação Ordinal é deliberada — nomes
        // de tipos do Revit são case-sensitive na API.
        private static T? FindByName<T>(IEnumerable<T> options, string? name, Func<T, string> nameSelector)
            where T : class
        {
            if (string.IsNullOrEmpty(name)) return null;

            foreach (T option in options)
            {
                if (string.Equals(nameSelector(option), name, StringComparison.Ordinal))
                    return option;
            }
            return null;
        }

        private static void PopulateViewModel(
            PipeConverterViewModel vm,
            IReadOnlyList<PipingSystemOptionViewModel> systems,
            IReadOnlyList<PipeTypeOptionViewModel> pipeTypes,
            IReadOnlyList<LevelOptionViewModel> levels)
        {
            vm.Systems.Clear();
            foreach (PipingSystemOptionViewModel s in systems) vm.Systems.Add(s);

            vm.PipeTypes.Clear();
            foreach (PipeTypeOptionViewModel t in pipeTypes) vm.PipeTypes.Add(t);

            vm.Levels.Clear();
            foreach (LevelOptionViewModel l in levels) vm.Levels.Add(l);

            if (vm.SelectedSystem == null && vm.Systems.Count > 0)
                vm.SelectedSystem = vm.Systems[0];

            // Garante uma opção de tolerância selecionada na primeira abertura
            // — o ComboBox da UI ficaria vazio sem isso se nada estiver
            // persistido em settings.
            if (vm.SelectedToleranceOption == null && vm.ToleranceOptions.Count > 0)
                vm.SetToleranceLevel(BifilarToleranceLevel.Medium);

            if (vm.SelectedPipeType == null && vm.PipeTypes.Count > 0)
                vm.SelectedPipeType = vm.PipeTypes[0];

            if (vm.SelectedLevel == null && vm.Levels.Count > 0)
                vm.SelectedLevel = vm.Levels[0];
        }
    }
}
