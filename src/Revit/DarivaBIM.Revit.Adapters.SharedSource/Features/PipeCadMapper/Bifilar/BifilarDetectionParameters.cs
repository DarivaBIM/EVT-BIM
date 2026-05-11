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
        /// Mapeia o slider 0..100 para todos os limiares.
        ///
        /// As escolhas vêm dos testes que o usuário rodou no Dynamo, mas com
        /// dois ajustes em relação à primeira versão deste detector:
        /// <list type="bullet">
        /// <item>O range de distância entre paredes (<see cref="MinEdgeDistanceMm"/>
        /// e <see cref="MaxEdgeDistanceMm"/>) ficou muito mais amplo
        /// (0,4× a 1,8× dos diâmetros nominais do tipo). Sem isso, um tubo
        /// desenhado 47mm em vez de 50mm já era rejeitado.</item>
        /// <item>O filtro de símbolos fica efetivamente desligado a partir
        /// de ~80% de tolerância. Como o detector já restringe a busca ao
        /// layer alvo, símbolos remanescentes são necessariamente
        /// componentes do próprio sistema (juntas, T-fittings, redutores
        /// desenhados como arcos) — não devem bloquear o pareamento quando
        /// o usuário aceita um pouco mais de falso-positivo.</item>
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

            // Folga generosa: 0,4× do menor diâmetro para baixo, 1,8× do maior
            // para cima. Para um tipo só com 50mm, o range fica 20..90mm, o
            // que aceita desenhos com pequenas distorções (47, 53, 70mm) e
            // ainda os snappa para 50mm via DiameterSnapper.
            double minEdgeMm = Math.Max(2.0, minDiamMm * 0.4);
            double maxEdgeMm = Math.Max(minEdgeMm + 20.0, maxDiamMm * 1.8);

            // Filtro de símbolos: estrito a 0%, praticamente desligado a 100%.
            // O ponto-chave: como já filtramos por layer, símbolos
            // remanescentes são parte do desenho do próprio sistema (juntas,
            // T-pieces). Em alta tolerância o usuário aceita esses ruídos.
            int maxHard;
            int maxTotal;
            if (t >= 0.8)
            {
                // "praticamente desligado" — um teto alto evita pareamentos
                // absurdos em CADs realmente caóticos sem custar nada nos
                // casos comuns.
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
                // Frouxo (t→1) aceita segmentos curtos; estrito (t→0) só os longos.
                MinCandidateLengthMm = Lerp(t, strict: 250.0, loose: 30.0),

                MinEdgeDistanceMm = minEdgeMm,
                MaxEdgeDistanceMm = maxEdgeMm,

                AngleToleranceDeg = Lerp(t, strict: 1.5, loose: 12.0),
                MinOverlapMm = Lerp(t, strict: 200.0, loose: 15.0),

                ClusterSnapMm = 25.0,

                // Buffer maior = caça mais símbolos = mais restritivo. Em
                // estrito, varremos uma faixa larga em volta da centerline;
                // em frouxo, só símbolos coladinhos contam.
                SymbolBufferMm = Lerp(t, strict: 120.0, loose: 25.0),

                MaxHardSymbolsInside = maxHard,
                MaxTotalSymbolsInside = maxTotal,

                // Ignorar mais perto das pontas = mais permissivo (verifica
                // só o miolo da centerline). Estrito = verifica quase tudo.
                EndpointIgnoreMm = Lerp(t, strict: 25.0, loose: 300.0),

                // Performance: limite escala suavemente com tolerância para
                // não pagar custo desnecessário em modo estrito.
                MaxCandidateSegments = (int)Math.Round(Lerp(t, strict: 500.0, loose: 2500.0)),
                MaxValidPairsStored = (int)Math.Round(Lerp(t, strict: 3000.0, loose: 12000.0)),
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
