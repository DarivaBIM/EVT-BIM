using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Presentation.Wpf.PipeConverter;
using DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Markers;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Handler do <c>ExternalEvent</c> que converte todos os marcadores
    /// (placeholders DBIM_PIPE_MARKER) presentes na vista ativa em tubos
    /// reais, encadeando os utilitários do adapter: conexão de pontas
    /// adjacentes, <c>PlumbingUtils.ConvertPipePlaceholders</c>, plug em
    /// tubos pré-existentes e limpeza dos overrides visuais.
    /// </summary>
    public class MarkerConversionHandler : IExternalEventHandler
    {
        public PipeConverterViewModel? ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            PipeConverterViewModel? vm = ViewModel;
            if (vm == null) return;

            try
            {
                vm.IsBusy = true;

                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    vm.StatusMessage = "Abra um projeto Revit para usar a ferramenta.";
                    return;
                }

                Document doc = uiDoc.Document;
                View activeView = doc.ActiveView;

                PipeMarkerConversionResult result = PipeMarkerConverter.ConvertAllInView(doc, activeView);

                vm.ActiveViewMarkerCount = PipeMarkerCollector.CountInView(doc, activeView);

                if (!result.Success)
                {
                    vm.StatusMessage = result.ErrorMessage ?? "Falha desconhecida ao converter marcadores.";
                    return;
                }

                vm.StatusMessage = $"Convertido(s) {result.ConvertedCount} marcador(es) em tubos.";
            }
            catch (Exception ex)
            {
                vm.StatusMessage = "Erro ao converter marcadores: " + ex.Message;
            }
            finally
            {
                vm.IsBusy = false;
            }
        }

        public string GetName() => "EvtBim.MarkerConversionHandler";
    }
}
