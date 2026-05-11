using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DarivaBIM.Plugin.Features.PipeCadMapper.Tools;
using DarivaBIM.Presentation.Wpf.PipeConverter;
using DarivaBIM.Revit.Adapters.Common.Cad;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Handler do <c>ExternalEvent</c> que pede ao usuário para clicar em
    /// um vínculo CAD (<see cref="ImportInstance"/>) e, ao receber a
    /// referência, popula no <see cref="PipeConverterViewModel"/>:
    /// <list type="bullet">
    /// <item>o id e o nome do vínculo selecionado;</item>
    /// <item>a lista de layers presentes na geometria desse vínculo.</item>
    /// </list>
    /// Outras seleções da janela só ficam habilitadas depois desse passo.
    /// </summary>
    public class CadLinkPickHandler : IExternalEventHandler
    {
        public PipeConverterViewModel? ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            PipeConverterViewModel? vm = ViewModel;
            if (vm == null) return;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    vm.StatusMessage = "Abra um projeto Revit para selecionar o vínculo CAD.";
                    return;
                }

                Document doc = uiDoc.Document;

                Reference reference;
                try
                {
                    reference = uiDoc.Selection.PickObject(
                        ObjectType.Element,
                        new ImportInstanceSelectionFilter(),
                        "PipeCADMapper — clique em um vínculo CAD para usá-lo como fonte.");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    vm.StatusMessage = "Seleção de vínculo CAD cancelada.";
                    return;
                }

                Element? element = doc.GetElement(reference);
                if (element is not ImportInstance importInstance)
                {
                    vm.StatusMessage = "O elemento selecionado não é um vínculo CAD.";
                    return;
                }

                IReadOnlyList<string> layers = CadLayerScanner.GetLayers(doc, importInstance);

                vm.CadLayers.Clear();
                foreach (string layer in layers) vm.CadLayers.Add(layer);

                vm.SelectedCadLinkId = RevitElementIdConversions.ToLong(importInstance.Id);
                vm.SelectedCadLinkName = TryGetName(importInstance) ?? "vínculo sem nome";

                if (vm.CadLayers.Count == 0)
                {
                    vm.SelectedLayer = null;
                    vm.StatusMessage = $"Vínculo selecionado: {vm.SelectedCadLinkName}. Nenhum layer com geometria utilizável foi encontrado.";
                }
                else
                {
                    // Preserva seleção anterior se ainda existir nesse CAD,
                    // senão escolhe o primeiro layer disponível para o usuário
                    // não ter que abrir a combo no caso trivial de um layer só.
                    if (vm.SelectedLayer == null || !vm.CadLayers.Contains(vm.SelectedLayer))
                        vm.SelectedLayer = vm.CadLayers[0];

                    vm.StatusMessage = $"Vínculo selecionado: {vm.SelectedCadLinkName}. {vm.CadLayers.Count} layer(s) disponível(is).";
                }
            }
            catch (Exception ex)
            {
                vm.StatusMessage = "Erro ao selecionar vínculo CAD: " + ex.Message;
            }
        }

        public string GetName() => "EvtBim.CadLinkPickHandler";

        private static string? TryGetName(Element element)
        {
            try { return element.Name; }
            catch { return null; }
        }

        private sealed class ImportInstanceSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is ImportInstance;
            public bool AllowReference(Reference reference, XYZ position) => true;
        }
    }
}
