using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Markers
{
    /// <summary>
    /// Localiza os tubos placeholder taggeados como DBIM_PIPE_MARKER em
    /// uma vista. Usado para (a) habilitar/desabilitar o botão "Converter
    /// marcadores em tubos" e (b) para a própria conversão.
    /// </summary>
    public static class PipeMarkerCollector
    {
        public static List<Pipe> CollectInView(Document doc, View view)
        {
            List<Pipe> markers = new();

            FilteredElementCollector collector = new(doc, view.Id);
            collector.OfClass(typeof(Pipe));

            foreach (Element el in collector)
            {
                if (el is not Pipe pipe) continue;
                if (!pipe.IsPlaceholder) continue;
                if (!PipeMarkerTag.IsTagged(pipe)) continue;
                markers.Add(pipe);
            }

            return markers;
        }

        // Antes era um foreach próprio que duplicava as 3 regras de filtro
        // (Pipe + IsPlaceholder + IsTagged). Delegar pra CollectInView garante
        // que as duas chamadas nunca divirjam quando o critério de "é
        // marcador" mudar. A vista ativa raramente tem mais que poucas
        // dezenas de placeholders taggeados — a alocação da List é desprezível
        // comparada com o trabalho da própria FilteredElementCollector.
        public static int CountInView(Document doc, View view) => CollectInView(doc, view).Count;
    }
}
