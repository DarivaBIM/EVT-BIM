using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Adapters.Features.FloorDrainExtension;

namespace DarivaBIM.Plugin.Features.FloorDrainExtension
{
    /// <summary>
    /// Modos suportados pelo external event de criação de prolongadores:
    /// seleção interativa (PickObjects), todas as caixas do projeto ou
    /// apenas as visíveis na vista ativa. O dropdown por tipo de caixa
    /// (vindo da janela) é aplicado em qualquer um dos três fluxos.
    /// </summary>
    public enum FloorDrainExtensionRunMode
    {
        PickInProject,
        AllInProject,
        VisibleInActiveView,
    }

    /// <summary>
    /// External event que executa o fluxo de criação de prolongadores
    /// (<see cref="FloorDrainExtensionCreator"/>), com o modo de coleta
    /// das caixas definido pelo botão clicado e o mapa de PipeType por
    /// tipo de caixa vindo da UI. Roda no contexto modeless da
    /// <see cref="FloorDrainExtensionWindow"/>.
    /// </summary>
    public class FloorDrainExtensionExternalEvent
    {
        private readonly FloorDrainExtensionHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public FloorDrainExtensionExternalEvent()
        {
            _handler = new FloorDrainExtensionHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(
            FloorDrainExtensionWindow window,
            double lengthMeters,
            FloorDrainExtensionRunMode mode,
            IReadOnlyDictionary<long, long> pipeTypeBySymbolId)
        {
            _handler.Window = window;
            _handler.LengthMeters = lengthMeters;
            _handler.Mode = mode;
            _handler.PipeTypeBySymbolId = pipeTypeBySymbolId;
            _externalEvent.Raise();
        }
    }

    internal class FloorDrainExtensionHandler : IExternalEventHandler
    {
        public FloorDrainExtensionWindow? Window { get; set; }
        public double LengthMeters { get; set; }
        public FloorDrainExtensionRunMode Mode { get; set; }
        public IReadOnlyDictionary<long, long> PipeTypeBySymbolId { get; set; } =
            new Dictionary<long, long>();

        public string GetName() => "EvtBim.FloorDrainExtensionHandler";

        public void Execute(UIApplication app)
        {
            FloorDrainExtensionWindow? win = Window;
            if (win == null)
                return;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    win.SetStatus("Abra um projeto Revit para usar a ferramenta.");
                    return;
                }

                Document doc = uiDoc.Document;

                List<FamilyInstance>? caixas = CollectBoxes(uiDoc, win, Mode);
                if (caixas == null)
                    return;

                if (caixas.Count == 0)
                {
                    win.SetStatus(BuildEmptyMessage(Mode));
                    return;
                }

                FloorDrainExtensionResult result =
                    FloorDrainExtensionCreator.Run(doc, caixas, LengthMeters, PipeTypeBySymbolId);

                string status =
                    $"{result.Created} prolongador(es) criado(s). " +
                    $"Sem conector vertical: {result.FailedNoVerticalConnector}, " +
                    $"sem PipeType: {result.FailedNoPipeType}, " +
                    $"outros erros: {result.FailedOther}.";

                win.SetStatus(status);

                if (result.Created == 0)
                {
                    string preview = string.Join("\n", result.Logs.Take(40));
                    TaskDialog.Show("EVT-BIM — Prolongador", status + "\n\nLog:\n" + preview);
                }
            }
            catch (Exception ex)
            {
                win.SetStatus($"Erro inesperado: {ex.Message}");
                TaskDialog.Show("EVT-BIM", $"Erro ao criar prolongadores:\n{ex.Message}");
            }
        }

        private static List<FamilyInstance>? CollectBoxes(
            UIDocument uiDoc,
            FloorDrainExtensionWindow win,
            FloorDrainExtensionRunMode mode)
        {
            Document doc = uiDoc.Document;

            switch (mode)
            {
                case FloorDrainExtensionRunMode.AllInProject:
                    return FloorDrainExtensionBoxScanner.CollectAllBoxes(doc);

                case FloorDrainExtensionRunMode.VisibleInActiveView:
                    View view = doc.ActiveView;
                    if (view == null)
                    {
                        win.SetStatus("Não há vista ativa no Revit.");
                        return new List<FamilyInstance>();
                    }
                    return FloorDrainExtensionBoxScanner.CollectBoxesInView(doc, view);

                case FloorDrainExtensionRunMode.PickInProject:
                default:
                    return PickBoxes(uiDoc, win);
            }
        }

        private static List<FamilyInstance>? PickBoxes(UIDocument uiDoc, FloorDrainExtensionWindow win)
        {
            Document doc = uiDoc.Document;
            IList<Reference> refs;
            try
            {
                refs = uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    new PlumbingFixtureSelectionFilter(),
                    "Selecione as caixas sifonadas/secas. ENTER ou ESC para finalizar.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                win.SetStatus("Seleção cancelada.");
                return null;
            }

            if (refs == null || refs.Count == 0)
            {
                win.SetStatus("Nenhuma caixa selecionada.");
                return new List<FamilyInstance>();
            }

            return refs
                .Select(r => doc.GetElement(r))
                .OfType<FamilyInstance>()
                .ToList();
        }

        private static string BuildEmptyMessage(FloorDrainExtensionRunMode mode) => mode switch
        {
            FloorDrainExtensionRunMode.AllInProject =>
                "Nenhuma caixa sifonada/seca encontrada no projeto.",
            FloorDrainExtensionRunMode.VisibleInActiveView =>
                "Nenhuma caixa sifonada/seca visível na vista ativa.",
            _ => "Os elementos selecionados não são instâncias de família.",
        };
    }

    /// <summary>
    /// Aceita FamilyInstances de PlumbingFixtures. Caixas sifonadas/secas vivem
    /// nesta categoria (OST_PlumbingFixtures).
    /// </summary>
    internal class PlumbingFixtureSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem is not FamilyInstance fi)
                return false;

            Category? cat = fi.Category;
            if (cat == null)
                return false;

            long id = cat.Id.Value;
            return id == (long)BuiltInCategory.OST_PlumbingFixtures
                || id == (long)BuiltInCategory.OST_PipeAccessory
                || id == (long)BuiltInCategory.OST_GenericModel
                || id == (long)BuiltInCategory.OST_MechanicalEquipment;
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
