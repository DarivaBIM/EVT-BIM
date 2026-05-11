using System;
using System.Collections.Generic;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Bifilar
{
    /// <summary>
    /// Conjunto completo de limiares numéricos do detector bifilar. Em vez
    /// de expor cada parâmetro na UI, o usuário controla um único slider
    /// "Tolerância" (0..100) e este record produz a tupla de valores via
    /// <see cref="FromTolerance"/>. 0 = casamento estrito (poucos pares,
    /// alta precisão); 100 = casamento frouxo (muitos pares, mais ruído).
    ///
    /// <see cref="MinEdgeDistanceMm"/> / <see cref="MaxEdgeDistanceMm"/>
    /// dependem da lista de diâmetros disponíveis no tipo selecionado:
    /// abaixo do menor diâmetro -1mm é ruído; acima do maior diâmetro
    /// +50% provavelmente são linhas paralelas que não formam um tubo.
    /// </summary>
    public sealed class BifilarDetectionParameters
    {
        public double MinCandidateLengthMm { get; init; }
        public double MinEdgeDistanceMm { get; init; }
        public double MaxEdgeDistanceMm { get; init; }
        public double AngleToleranceDeg { get; init; }
        public double MinOverlapMm { get; init; }
        public double ClusterSnapMm { get; init; }
        public double SymbolBufferMm { get; init; }
        public int MaxHardSymbolsInside { get; init; }
        public int MaxTotalSymbolsInside { get; init; }
        public double EndpointIgnoreMm { get; init; }
        public int MaxCandidateSegments { get; init; }
        public int MaxValidPairsStored { get; init; }
        public double SegmentGridCellMm { get; init; }
        public bool LockEdgeAfterPair { get; init; }

        /// <summary>
        /// Mapeia o slider 0..100 para todos os limiares. As escolhas vêm de
        /// testes no Dynamo: o caminho intermediário (~50) acerta ~80% dos
        /// tubos em CADs bifilar típicos; abaixo de 30 perde tubos
        /// inclinados/curtos; acima de 70 começa a pareiar linhas vizinhas
        /// não relacionadas.
        /// </summary>
        public static BifilarDetectionParameters FromTolerance(
            double tolerancePercent,
            IReadOnlyList<double> availableDiametersMm)
        {
            double t = Math.Clamp(tolerancePercent / 100.0, 0.0, 1.0);

            // min / max edge distance dependem dos diâmetros disponíveis do
            // tipo selecionado. Se a lista veio vazia (tipo sem routing
            // preferences), caímos para o range "amplo" do código Dynamo.
            double minDiamMm = 5.0;
            double maxDiamMm = 250.0;
            if (availableDiametersMm.Count > 0)
            {
                minDiamMm = double.MaxValue;
                maxDiamMm = 0.0;
                foreach (double d in availableDiametersMm)
                {
                    if (d < minDiamMm) minDiamMm = d;
                    if (d > maxDiamMm) maxDiamMm = d;
                }
            }

            // Folga: -1mm para baixo e +30% para cima do range nominal,
            // mas com piso/teto para não colapsar quando só há um diâmetro.
            double minEdgeMm = Math.Max(2.0, minDiamMm - 1.0);
            double maxEdgeMm = Math.Max(minEdgeMm + 10.0, maxDiamMm * 1.3);

            return new BifilarDetectionParameters
            {
                // Frouxo (t→1) aceita segmentos curtos; estrito (t→0) só os longos.
                MinCandidateLengthMm = Lerp(t, strict: 250.0, loose: 40.0),

                MinEdgeDistanceMm = minEdgeMm,
                MaxEdgeDistanceMm = maxEdgeMm,

                AngleToleranceDeg = Lerp(t, strict: 1.5, loose: 10.0),
                MinOverlapMm = Lerp(t, strict: 200.0, loose: 25.0),

                ClusterSnapMm = 25.0,

                SymbolBufferMm = Lerp(t, strict: 35.0, loose: 100.0),

                // 0 símbolos "duros" (arc/ellipse) dentro = casamento limpo;
                // afrouxar permite até 3, útil quando o CAD tem cotas/textos.
                MaxHardSymbolsInside = (int)Math.Round(Lerp(t, strict: 0.0, loose: 3.0)),
                MaxTotalSymbolsInside = (int)Math.Round(Lerp(t, strict: 2.0, loose: 12.0)),

                EndpointIgnoreMm = Lerp(t, strict: 200.0, loose: 40.0),

                // Performance: limita o universo de busca para nunca
                // estourar. 700 candidatos x ~50 vizinhos = ~35k testes.
                MaxCandidateSegments = 700,
                MaxValidPairsStored = 5000,
                SegmentGridCellMm = 400.0,
                LockEdgeAfterPair = true,
            };
        }

        private static double Lerp(double t, double strict, double loose)
        {
            return strict + (loose - strict) * t;
        }
    }
}
