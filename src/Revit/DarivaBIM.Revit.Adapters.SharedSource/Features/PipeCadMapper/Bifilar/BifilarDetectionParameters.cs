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

        // Restrição de bend angles aplicada na cadeia de centerlines do
        // pareamento polyline-aware. Vazia + AllowAnyBendAngle=true ⇒ não
        // aplica snap (geometria preservada). Senão, cada bend é forçado
        // ao ângulo permitido mais próximo dentro de ±15°. Bends |b|<15°
        // viram retas, exceto quando AllowAnyBendAngle=true.
        public IReadOnlyList<double> AllowedBendAnglesDeg { get; init; } = Array.Empty<double>();
        public bool AllowAnyBendAngle { get; init; } = true;

        /// <summary>
        /// Mapeia o slider 0..100 para todos os limiares.
        ///
        /// Decisões importantes:
        /// <list type="bullet">
        /// <item>Distância entre paredes filtrada em DOIS estágios: aqui um
        /// sanity belt amplo (Min/MaxEdgeDistanceMm em torno do menor/maior
        /// nominal); no detector, um filtro estrito de ±2mm de algum
        /// nominal (<c>IsEdgeNearAnyNominal</c>). O segundo é universal e
        /// não escala com a tolerância — gaps fora dessa janela JAMAIS
        /// viram marcador, mesmo na tolerância máxima.</item>
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
            IReadOnlyList<double> availableDiametersMm,
            IReadOnlyList<double>? allowedBendAnglesDeg = null,
            bool allowAnyBendAngle = true)
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

            // Limites Min/Max servem como sanity belt rápido — o filtro
            // estrito mora no detector (IsEdgeNearAnyNominal: ±2mm de algum
            // nominal). Aqui mantemos uma janela um pouco maior em volta do
            // menor/maior nominal só para descartar pares grosseiramente
            // fora antes de chegar no filtro fino. O upper bound antes era
            // 2,5× do maior nominal e deixava passar gaps de 200..500mm
            // quando o maior nominal era 200 — geometria errada virava
            // marcador snappado. Agora amarramos em maxDiam + 5mm.
            double minEdgeMm = Math.Max(2.0, minDiamMm - 5.0);
            double maxEdgeMm = maxDiamMm + 5.0;

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
                AllowedBendAnglesDeg = allowedBendAnglesDeg ?? Array.Empty<double>(),
                AllowAnyBendAngle = allowAnyBendAngle,
            };
        }

        /// <summary>
        /// Parâmetros para o picker bifilar (parede-a-parede). Bem mais
        /// permissivo que <see cref="FromTolerance"/>: o usuário JÁ tomou a
        /// decisão de que aquela linha é parede de tubo, então não faz sentido
        /// rejeitar por overlap curto, ângulo levemente fora, ou símbolos
        /// cruzando o eixo. O ÚNICO filtro estrito que sobra é o de distância
        /// entre paredes ±2mm de algum nominal (aplicado no detector via
        /// <c>IsEdgeNearAnyNominal</c>) — esse é universal.
        ///
        /// O picker existe para "limpar" o que escapou do batch; com regras
        /// idênticas ao batch, ele ofereceria zero valor adicional.
        /// </summary>
        public static BifilarDetectionParameters ForPicker(
            IReadOnlyList<double> availableDiametersMm,
            IReadOnlyList<double>? allowedBendAnglesDeg = null,
            bool allowAnyBendAngle = true)
        {
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

            // Sanity belt — mesma forma do FromTolerance. O filtro fino de
            // ±2mm em IsEdgeNearAnyNominal faz o trabalho real de validação.
            double minEdgeMm = Math.Max(2.0, minDiamMm - 5.0);
            double maxEdgeMm = maxDiamMm + 5.0;

            return new BifilarDetectionParameters
            {
                // Linhas curtas viram candidates — o batch as ignora pra não
                // virar ruído mas no pick a anchor pode ser uma delas.
                MinCandidateLengthMm = 5.0,

                MinEdgeDistanceMm = minEdgeMm,
                MaxEdgeDistanceMm = maxEdgeMm,

                // Aceita paredes desenhadas levemente fora de paralelismo perfeito.
                AngleToleranceDeg = 15.0,

                // Overlap mínimo bem baixo: pares com pouquíssima coincidência
                // longitudinal voltam a ser elegíveis.
                MinOverlapMm = 5.0,

                ClusterSnapMm = 25.0,

                // Filtro de símbolos praticamente desligado: o usuário decide.
                SymbolBufferMm = 5.0,
                MaxHardSymbolsInside = 10000,
                MaxTotalSymbolsInside = 10000,

                EndpointIgnoreMm = 25.0,

                MaxCandidateSegments = 10000,
                MaxValidPairsStored = 30000,
                SegmentGridCellMm = 400.0,
                LockEdgeAfterPair = false,
                AvailableDiametersMm = availableDiametersMm,
                AllowedBendAnglesDeg = allowedBendAnglesDeg ?? Array.Empty<double>(),
                AllowAnyBendAngle = allowAnyBendAngle,
            };
        }

        private static double Lerp(double t, double strict, double loose)
        {
            return strict + (loose - strict) * t;
        }
    }
}
