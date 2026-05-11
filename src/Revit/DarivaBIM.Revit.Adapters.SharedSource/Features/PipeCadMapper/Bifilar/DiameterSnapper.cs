using System.Collections.Generic;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Bifilar
{
    /// <summary>
    /// Arredonda uma distância medida (em mm, entre as duas paredes do tubo
    /// bifilar) para o diâmetro nominal mais próximo disponível no tipo de
    /// tubo escolhido. Quando a lista vem vazia (tipo sem routing preferences)
    /// o fallback é o diâmetro default da configuração.
    /// </summary>
    internal static class DiameterSnapper
    {
        public static double Snap(double measuredMm, IReadOnlyList<double> availableMm, double fallbackMm)
        {
            if (availableMm == null || availableMm.Count == 0)
                return fallbackMm;

            double bestDiff = double.MaxValue;
            double best = availableMm[0];

            foreach (double d in availableMm)
            {
                double diff = d - measuredMm;
                if (diff < 0) diff = -diff;

                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = d;
                }
            }

            return best;
        }
    }
}
