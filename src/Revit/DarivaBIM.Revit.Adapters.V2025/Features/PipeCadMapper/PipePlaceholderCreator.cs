using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace DarivaBIM.Revit.Adapters.V2025.Features.PipeCadMapper
{
    /// <summary>
    /// Saída de <see cref="PipePlaceholderCreator.CreatePlaceholders"/>:
    /// lista de placeholders já criados (com seus pontos de início/fim
    /// projetados na cota alvo), quantidade pulada por serem mais curtos
    /// que a tolerância e a contagem efetiva.
    /// </summary>
    internal readonly struct PipePlaceholderBatch
    {
        public PipePlaceholderBatch(
            List<(Pipe Pipe, XYZ Start, XYZ End)> placeholders,
            int created,
            int skipped)
        {
            Placeholders = placeholders;
            Created = created;
            Skipped = skipped;
        }

        public List<(Pipe Pipe, XYZ Start, XYZ End)> Placeholders { get; }
        public int Created { get; }
        public int Skipped { get; }
    }

    /// <summary>
    /// Cria os placeholders de tubo a partir dos segmentos extraídos do
    /// vínculo CAD, projetando o Z para a cota alvo (level + offset) e
    /// ajustando o diâmetro para o valor escolhido pelo usuário. A
    /// transação deve estar aberta pelo caller — esta classe só faz o
    /// trabalho dentro dela.
    /// </summary>
    internal static class PipePlaceholderCreator
    {
        public static PipePlaceholderBatch CreatePlaceholders(
            Document doc,
            IReadOnlyList<(XYZ Start, XYZ End)> segments,
            PipeConversionConfig config,
            double targetZ,
            double diameterFeet,
            double tol)
        {
            int created = 0;
            int skipped = 0;
            List<(Pipe, XYZ, XYZ)> placeholders = new(segments.Count);

            foreach ((XYZ startRaw, XYZ endRaw) in segments)
            {
                XYZ start = new(startRaw.X, startRaw.Y, targetZ);
                XYZ end = new(endRaw.X, endRaw.Y, targetZ);

                if (start.DistanceTo(end) < tol)
                {
                    skipped++;
                    continue;
                }

                Pipe placeholder = Pipe.CreatePlaceholder(
                    doc,
                    config.SystemTypeId,
                    config.PipeTypeId,
                    config.LevelId,
                    start,
                    end);

                placeholder.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameterFeet);

                placeholders.Add((placeholder, start, end));
                created++;
            }

            return new PipePlaceholderBatch(placeholders, created, skipped);
        }
    }
}
