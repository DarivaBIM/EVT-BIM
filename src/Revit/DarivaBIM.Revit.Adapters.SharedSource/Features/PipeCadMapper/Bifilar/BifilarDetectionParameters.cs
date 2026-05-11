using System;
using System.Collections.Generic;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Bifilar
{
    /// <summary>
    /// Conjunto completo de limiares numéricos do detector bifilar. Em vez
    /// de expor cada parâmetro na UI, o usuário controla um único slider
    /// "Tolerância" (0..100) e este record produz a tupla de valores via
    /// <see cref="FromTolerance"/>. 0 = casamento estrito (poucos pares,
    /// alta precisão); 100 = casamento muito permissivo (muitos pares,
    /// inclusive em regiões com cotas, T-fittings e outros símbolos do
    /// mesmo layer atravessando o eixo do tubo).
    ///
    /// <see cref="MinEdgeDistanceMm"/> / <see cref="MaxEdgeDistanceMm"/>
    /// dependem da lista de diâmetros disponíveis no tipo selecionado.
    /// O intervalo é generoso porque desenhos reais frequentemente fogem
    /// dos nominais (a regra final é o <c>DiameterSnapper</c>, que ainda
    /// aproxima a medida para um diâmetro disponível).
    ///
    /// <see cref="AvailableDiametersMm"/> é carregado aqui também para o
    /// detector poder usar como critério de DESEMPATE entre pares
    /// concorrentes: pares com distância entre paredes próxima de um
    /// nominal vencem pares "errados" (gaps entre tubos diferentes, por
    /// exemplo) na hora de travar candidatos.
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
        public IReadOnlyList<double> AvailableDiametersMm { get; init; } = Array.Empty<double>();

        /// <summary>
        /// Mapeia o slider 0..100 para todos os limiares.
        ///
        /// Decisões importantes:
        /// <list type="bullet">
        /// <item>Range de distância entre paredes BEM amplo (0,3× a 2,5× dos
        /// nominais do tipo). Tubos desenhados levemente fora do nominal
        /// — 47mm em vez de 50, 90mm em vez de 100 — entram e o
        /// <c>DiameterSnapper</c> os arruma para o nominal mais próximo.</item>
        /// <item>Filtro de símbolos praticamente desligado a partir de 80%.
        /// Como já filtramos por layer, símbolos remanescentes vêm do
        /// próprio sistema (juntas, T-fittings); em alta tolerância o
        /// usuário aceita esse ruído em troca de não perder pipes.</item>
        /// <item>Limite alto de candidatos (até 10000) para projetos grandes
        /// não terem paredes ignoradas só por aparecerem depois no scan.</item>
        /// </list>
        /// </summary>
        public static BifilarDetectionParameters FromTolerance(
            double tolerancePercent,
            IReadOnlyList<double> availableDiametersMm)
        {
            double t = Math.Clamp(tolerancePercent / 100.0, 0.0, 1.0);

            double minDiamMm = 10.0;
            double maxDiamMm = 200.0;
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

            // Folga MUITO generosa: 0,3× do menor diâmetro para baixo,
            // 2,5× do maior para cima. Para um tipo só com 50mm o range
            // fica 15..125mm, o que aceita tubos desenhados 25..100mm
            // (DiameterSnapper depois os arruma).
            double minEdgeMm = Math.Max(2.0, minDiamMm * 0.3);
            double maxEdgeMm = Math.Max(minEdgeMm + 30.0, maxDiamMm * 2.5);

            int maxHard;
            int maxTotal;
            if (t >= 0.8)
            {
                maxHard = 100;
                maxTotal = 500;
            }
            else
            {
                maxHard = (int)Math.Round(Lerp(t / 0.8, strict: 0.0, loose: 6.0));
                maxTotal = (int)Math.Round(Lerp(t / 0.8, strict: 2.0, loose: 20.0));
            }

            return new BifilarDetectionParameters
            {
                MinCandidateLengthMm = Lerp(t, strict: 250.0, loose: 20.0),

                MinEdgeDistanceMm = minEdgeMm,
                MaxEdgeDistanceMm = maxEdgeMm,

                AngleToleranceDeg = Lerp(t, strict: 1.5, loose: 12.0),
                MinOverlapMm = Lerp(t, strict: 200.0, loose: 15.0),

                ClusterSnapMm = 25.0,

                SymbolBufferMm = Lerp(t, strict: 120.0, loose: 25.0),

                MaxHardSymbolsInside = maxHard,
                MaxTotalSymbolsInside = maxTotal,

                EndpointIgnoreMm = Lerp(t, strict: 25.0, loose: 300.0),

                // Limites altos para projetos grandes — o custo extra do
                // pareamento é amortizado pelo grid espacial e pelo filtro
                // de ângulo rápido (a maioria dos candidatos vizinhos é
                // descartada em O(1) antes de pagar o custo de overlap).
                MaxCandidateSegments = (int)Math.Round(Lerp(t, strict: 800.0, loose: 10000.0)),
                MaxValidPairsStored = (int)Math.Round(Lerp(t, strict: 5000.0, loose: 30000.0)),
                SegmentGridCellMm = 400.0,
                LockEdgeAfterPair = true,
                AvailableDiametersMm = availableDiametersMm,
            };
        }

        private static double Lerp(double t, double strict, double loose)
        {
            return strict + (loose - strict) * t;
        }
    }
}
