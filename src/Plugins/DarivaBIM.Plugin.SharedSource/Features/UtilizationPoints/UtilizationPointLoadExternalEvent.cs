using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Adapters.Features.UtilizationPoints;

namespace DarivaBIM.Plugin.Features.UtilizationPoints
{
    /// <summary>
    /// ExternalEvent que varre o documento ativo para alimentar a janela com
    /// os tipos de família candidatos a ponto de utilização e a lista de
    /// níveis. Roda no contexto modeless da
    /// <see cref="UtilizationPointInsertionWindow"/>.
    /// </summary>
    public class UtilizationPointLoadExternalEvent
    {
        private readonly UtilizationPointLoadHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public UtilizationPointLoadExternalEvent()
        {
            _handler = new UtilizationPointLoadHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(UtilizationPointInsertionWindow window)
        {
            _handler.Window = window;
            _externalEvent.Raise();
        }
    }

    internal class UtilizationPointLoadHandler : IExternalEventHandler
    {
        public UtilizationPointInsertionWindow? Window { get; set; }

        public string GetName() => "EvtBim.UtilizationPointLoadHandler";

        public void Execute(UIApplication app)
        {
            UtilizationPointInsertionWindow? window = Window;
            if (window == null) return;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    window.ApplyCatalog(
                        Array.Empty<FamilyTypeOptionDto>(),
                        Array.Empty<LevelOptionDto>(),
                        "Abra um projeto Revit para usar a ferramenta.");
                    return;
                }

                RevitFamilyTypeCatalogService catalog = new(uiDoc.Document);
                IReadOnlyList<FamilyTypeOptionDto> familyTypes = catalog.GetAvailableFamilyTypes();
                IReadOnlyList<LevelOptionDto> levels = catalog.GetLevels();

                string message = familyTypes.Count == 0
                    ? "Nenhum tipo de família hidrossanitário encontrado no documento."
                    : $"{familyTypes.Count} tipo(s) de família carregado(s).";

                window.ApplyCatalog(familyTypes, levels, message);
            }
            catch (Exception ex)
            {
                window.ApplyCatalog(
                    Array.Empty<FamilyTypeOptionDto>(),
                    Array.Empty<LevelOptionDto>(),
                    $"Falha ao ler o catálogo do documento: {ex.Message}");
            }
        }
    }
}
