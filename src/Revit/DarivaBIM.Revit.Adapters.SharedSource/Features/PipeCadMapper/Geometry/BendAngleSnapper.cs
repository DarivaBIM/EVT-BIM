using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Geometry
{
    /// <summary>
    /// Algoritmo puro de "snap de bends" para uma polyline. Dada uma cadeia
    /// de vértices e um conjunto de ângulos permitidos (ex.: 22.5°, 45°,
    /// 60°, 90°), reescreve a polyline forçando cada bend (mudança de
    /// direção entre dois segmentos consecutivos) para o ângulo permitido
    /// mais próximo, dentro de uma janela de tolerância (±15°). Bends muito
    /// pequenos (|bend| &lt; <see cref="ZeroSnapThresholdDeg"/>) são
    /// achatados para 0° e os dois segmentos colineares resultantes são
    /// fundidos em um único segmento — evita "tracinhos" de ~0° que vêm de
    /// imprecisão de desenho do CAD.
    ///
    /// <para>Tudo no plano XY (Z é mantido do vértice anterior).</para>
    /// </summary>
    public static class BendAngleSnapper
    {
        public const double ZeroSnapThresholdDeg = 15.0;
        public const double AllowedSnapWindowDeg = 15.0;

        /// <summary>
        /// Reescreve <paramref name="vertices"/> com bends snappados.
        /// <list type="bullet">
        /// <item>Quando <paramref name="allowAnyAngle"/> é true OU
        /// <paramref name="allowedAnglesDeg"/> está vazia, devolve uma cópia
        /// dos vértices originais (sem snap) — comportamento neutro.</item>
        /// <item>Caso contrário, percorre os vértices em ordem, mede o bend
        /// em cada vértice intermediário e aplica o snap. Comprimentos dos
        /// segmentos originais são preservados; o que muda é a DIREÇÃO do
        /// segmento seguinte.</item>
        /// <item>Quando o snap "achata" para 0°, os dois segmentos viram um
        /// só (vértice intermediário descartado).</item>
        /// </list>
        /// </summary>
        public static List<XYZ> SnapPolylineBends(
            IReadOnlyList<XYZ> vertices,
            IReadOnlyList<double> allowedAnglesDeg,
            bool allowAnyAngle)
        {
            if (vertices == null || vertices.Count < 2)
                return new List<XYZ>(vertices ?? (IReadOnlyList<XYZ>)Array.Empty<XYZ>());

            // Sem restrições efetivas → comportamento neutro (mantém geometria).
            if (allowAnyAngle || allowedAnglesDeg == null || allowedAnglesDeg.Count == 0)
                return new List<XYZ>(vertices);

            // Polyline com 1 só segmento (2 vértices) não tem bend a snapar.
            if (vertices.Count == 2)
                return new List<XYZ>(vertices);

            // Estado corrente do "fim" da polyline reconstruída:
            //   currentEnd: último ponto que vai compor o output (ainda em aberto)
            //   currentDir: direção UNITÁRIA do último segmento aceito
            //   currentLength: comprimento acumulado do segmento em construção
            //                  (cresce quando bends ≈ 0 fazem fusão de segmentos)
            //
            // Quando um bend real (não-zero) acontece, fechamos o segmento
            // atual emitindo (currentSegmentStart, currentSegmentStart +
            // currentDir * currentLength) e começamos um novo a partir dali.
            List<XYZ> result = new(vertices.Count) { vertices[0] };

            XYZ currentSegmentStart = vertices[0];
            XYZ currentDir = NormalizeXY(vertices[1] - vertices[0]);
            double currentLengthFt = vertices[0].DistanceTo(vertices[1]);
            double currentZ = vertices[1].Z;

            for (int i = 2; i < vertices.Count; i++)
            {
                XYZ originalPrev = vertices[i - 1];
                XYZ originalNext = vertices[i];

                double nextLengthFt = originalPrev.DistanceTo(originalNext);
                if (nextLengthFt <= 1e-9)
                    continue; // segmento degenerado, ignora

                XYZ originalDir = NormalizeXY(originalNext - originalPrev);

                // Bend signed (–180..180): rotação de currentDir até originalDir.
                double bendDeg = SignedAngleDegXY(currentDir, originalDir);
                double snappedBendDeg = SnapBendAngle(bendDeg, allowedAnglesDeg);

                if (Math.Abs(snappedBendDeg) < 1e-6)
                {
                    // Bend ≈ 0 → fundir o próximo segmento ao atual (estende
                    // currentLength preservando direção). Vértice intermediário
                    // não é emitido.
                    currentLengthFt += nextLengthFt;
                    currentZ = originalNext.Z;
                    continue;
                }

                // Bend real: fecha o segmento atual e começa um novo.
                XYZ currentEnd = currentSegmentStart + currentDir * currentLengthFt;
                currentEnd = new XYZ(currentEnd.X, currentEnd.Y, currentZ);
                result.Add(currentEnd);

                XYZ newDir = RotateXY(currentDir, snappedBendDeg);
                currentSegmentStart = currentEnd;
                currentDir = newDir;
                currentLengthFt = nextLengthFt;
                currentZ = originalNext.Z;
            }

            // Fecha o último segmento.
            XYZ finalEnd = currentSegmentStart + currentDir * currentLengthFt;
            finalEnd = new XYZ(finalEnd.X, finalEnd.Y, currentZ);
            result.Add(finalEnd);

            return result;
        }

        /// <summary>
        /// Snap do ângulo signed-bend para o conjunto de ângulos permitidos.
        /// <list type="bullet">
        /// <item>|bend| &lt; <see cref="ZeroSnapThresholdDeg"/> → 0° (sempre,
        /// independentemente da lista de permitidos).</item>
        /// <item>|bend| dentro de ±<see cref="AllowedSnapWindowDeg"/> de
        /// algum ângulo permitido → snap para esse ângulo (preservando o
        /// sinal do bend original).</item>
        /// <item>Caso contrário → bend original (sem snap).</item>
        /// </list>
        /// </summary>
        public static double SnapBendAngle(double bendDeg, IReadOnlyList<double> allowedAnglesDeg)
        {
            double absBend = Math.Abs(bendDeg);
            if (absBend < ZeroSnapThresholdDeg) return 0.0;

            if (allowedAnglesDeg == null || allowedAnglesDeg.Count == 0) return bendDeg;

            double sign = bendDeg < 0 ? -1.0 : 1.0;
            double bestAllowed = -1.0;
            double bestDiff = double.MaxValue;
            for (int i = 0; i < allowedAnglesDeg.Count; i++)
            {
                double allowed = allowedAnglesDeg[i];
                double diff = Math.Abs(allowed - absBend);
                if (diff <= AllowedSnapWindowDeg && diff < bestDiff)
                {
                    bestDiff = diff;
                    bestAllowed = allowed;
                }
            }

            return bestAllowed >= 0 ? sign * bestAllowed : bendDeg;
        }

        // ---------- helpers de geometria 2D ----------

        private static XYZ NormalizeXY(XYZ v)
        {
            double len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            if (len < 1e-12) return new XYZ(1.0, 0.0, 0.0);
            return new XYZ(v.X / len, v.Y / len, 0.0);
        }

        // Ângulo SIGNED no plano XY de "from" para "to", em graus, em (–180, 180].
        // Positivo = rotação anti-horária. Z ignorado.
        private static double SignedAngleDegXY(XYZ from, XYZ to)
        {
            double dot = from.X * to.X + from.Y * to.Y;
            double cross = from.X * to.Y - from.Y * to.X;
            double rad = Math.Atan2(cross, dot);
            return rad * 180.0 / Math.PI;
        }

        // Rotaciona vetor unitário no plano XY por angleDeg (positivo = anti-horário).
        private static XYZ RotateXY(XYZ dir, double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            return new XYZ(
                dir.X * cos - dir.Y * sin,
                dir.X * sin + dir.Y * cos,
                0.0);
        }
    }
}
