using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DarivaBIM.Plugin.Ui;
#if REVIT2026
using DarivaBIM.Revit.Adapters.V2026.Features.Prolongador;
#elif REVIT2025
using DarivaBIM.Revit.Adapters.V2025.Features.Prolongador;
#endif

namespace DarivaBIM.Plugin.Features.Prolongador
{
    /// <summary>
    /// External event que abre um <c>PickObjects</c> filtrando por instâncias de
    /// família (caixas sifonadas/secas) e cria os tubos prolongadores. Roda no
    /// contexto modeless da <see cref="ProlongadorWindow"/>.
    /// </summary>
    public class ProlongadorExternalEvent
    {
        private readonly ProlongadorHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public ProlongadorExternalEvent()
        {
            _handler = new ProlongadorHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(ProlongadorWindow window, double lengthMeters)
        {
            _handler.Window = window;
            _handler.LengthMeters = lengthMeters;
            _externalEvent.Raise();
        }
    }

    internal class ProlongadorHandler : IExternalEventHandler
    {
        public ProlongadorWindow? Window { get; set; }
        public double LengthMeters { get; set; }

        public string GetName() => "EvtBim.ProlongadorHandler";

        public void Execute(UIApplication app)
        {
            ProlongadorWindow? win = Window;
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
                    return;
                }

                if (refs == null || refs.Count == 0)
                {
                    win.SetStatus("Nenhuma caixa selecionada.");
                    return;
                }

                List<FamilyInstance> caixas = refs
                    .Select(r => doc.GetElement(r))
                    .OfType<FamilyInstance>()
                    .ToList();

                if (caixas.Count == 0)
                {
                    win.SetStatus("Os elementos selecionados não são instâncias de família.");
                    return;
                }

                ProlongadorResult result = ProlongadorCreator.Run(doc, caixas, LengthMeters);

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
