using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Revit.Adapters.Common.Pipes;
using DarivaBIM.Revit.Adapters.Common.Transactions.FailurePreprocessors;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Markers
{
    public sealed class PipeMarkerConversionResult
    {
        private PipeMarkerConversionResult(bool success, int convertedCount, string? errorMessage)
        {
            Success = success;
            ConvertedCount = convertedCount;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public int ConvertedCount { get; }
        public string? ErrorMessage { get; }

        public static PipeMarkerConversionResult Ok(int count) => new(true, count, null);
        public static PipeMarkerConversionResult Failed(string message) => new(false, 0, message);
    }

    /// <summary>
    /// Converte todos os marcadores (placeholders DBIM_PIPE_MARKER) de uma
    /// vista em tubos reais. Antes da conversão, conecta extremidades
    /// coincidentes entre marcadores adjacentes (para o Revit injetar
    /// joelhos/tês automaticamente). Depois da conversão, conecta as
    /// pontas abertas a tubos pré-existentes do projeto. Por fim, limpa
    /// o override visual de marcador dos elementos resultantes.
    /// </summary>
    public static class PipeMarkerConverter
    {
        public static PipeMarkerConversionResult ConvertAllInView(Document doc, View view)
        {
            List<Pipe> markers = PipeMarkerCollector.CollectInView(doc, view);
            if (markers.Count == 0)
                return PipeMarkerConversionResult.Failed("Nenhum marcador encontrado na vista ativa.");

            double tol = doc.Application.ShortCurveTolerance;

            using Transaction tx = new(doc, "PipeCADMapper — converter marcadores em tubos");
            FailureHandlingOptions failureOptions = tx.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new PipeCreationFailurePreprocessor());
            failureOptions.SetClearAfterRollback(true);
            failureOptions.SetForcedModalHandling(false);
            tx.SetFailureHandlingOptions(failureOptions);
            tx.Start();

            try
            {
                ConnectAdjacentMarkers(markers, tol);

                List<ElementId> markerIds = new(markers.Count);
                foreach (Pipe p in markers) markerIds.Add(p.Id);

                // Limpa o tag ANTES da conversão para evitar arrastar a
                // marcação para o tubo final (o id permanece em muitos casos
                // de ConvertPipePlaceholders).
                foreach (Pipe p in markers) PipeMarkerTag.Clear(p);

                ICollection<ElementId> convertedIds;
                try
                {
                    convertedIds = PlumbingUtils.ConvertPipePlaceholders(doc, markerIds);
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    return PipeMarkerConversionResult.Failed(
                        "Falha ao converter marcadores em tubos: " + ex.Message +
                        " — verifique as preferências de roteamento (joelhos/tês) do tipo de tubo.");
                }

                List<Pipe> converted = new(convertedIds.Count);
                foreach (ElementId id in convertedIds)
                {
                    if (doc.GetElement(id) is Pipe p) converted.Add(p);
                }

                PipeConnectorService.ConnectToExistingPipes(doc, converted, tol);

                // Remove o override magenta dos tubos resultantes para que
                // eles apareçam com o estilo padrão do sistema/tipo.
                foreach (ElementId id in convertedIds)
                {
                    PipeMarkerOverrides.Clear(view, id);
                }
                // Também limpa override dos ids originais (alguns podem
                // ter sido reutilizados pelo Revit).
                foreach (ElementId id in markerIds)
                {
                    if (!convertedIds.Contains(id))
                        PipeMarkerOverrides.Clear(view, id);
                }

                tx.Commit();
                return PipeMarkerConversionResult.Ok(converted.Count);
            }
            catch (Exception ex)
            {
                if (tx.HasStarted() && !tx.HasEnded())
                    tx.RollBack();
                return PipeMarkerConversionResult.Failed("Erro inesperado: " + ex.Message);
            }
        }

        private static void ConnectAdjacentMarkers(List<Pipe> markers, double tol)
        {
            // Indexa endpoints por uma grade XYZ grosseira para encontrar
            // pares próximos sem o custo O(n²). Cada marcador participa
            // de dois endpoints.
            double cellFt = Math.Max(tol * 4.0, 0.005);
            Dictionary<(int, int, int), List<(Pipe Pipe, XYZ Point)>> grid = new();

            void Add(Pipe pipe, XYZ p)
            {
                var key = ((int)Math.Floor(p.X / cellFt), (int)Math.Floor(p.Y / cellFt), (int)Math.Floor(p.Z / cellFt));
                if (!grid.TryGetValue(key, out var bucket))
                {
                    bucket = new List<(Pipe, XYZ)>();
                    grid[key] = bucket;
                }
                bucket.Add((pipe, p));
            }

            foreach (Pipe pipe in markers)
            {
                foreach (Connector c in pipe.ConnectorManager.Connectors)
                    Add(pipe, c.Origin);
            }

            HashSet<(ElementId, ElementId)> alreadyConnected = new();

            foreach (Pipe pipe in markers)
            {
                foreach (Connector c in pipe.ConnectorManager.Connectors)
                {
                    if (c.IsConnected) continue;

                    int cx = (int)Math.Floor(c.Origin.X / cellFt);
                    int cy = (int)Math.Floor(c.Origin.Y / cellFt);
                    int cz = (int)Math.Floor(c.Origin.Z / cellFt);

                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var key = (cx + dx, cy + dy, cz + dz);
                        if (!grid.TryGetValue(key, out var bucket)) continue;

                        foreach ((Pipe other, XYZ point) in bucket)
                        {
                            if (other.Id == pipe.Id) continue;
                            if (point.DistanceTo(c.Origin) > tol) continue;

                            var pairKey = pipe.Id.Value < other.Id.Value
                                ? (pipe.Id, other.Id)
                                : (other.Id, pipe.Id);
                            if (!alreadyConnected.Add(pairKey)) continue;

                            PipeConnectorService.TryConnectPipesAt(pipe, other, c.Origin, tol);
                        }
                    }
                }
            }
        }
    }
}
