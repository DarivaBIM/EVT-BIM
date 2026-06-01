using System;
using System.Collections.Generic;
using System.Numerics;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Blindagem do sinal do OutwardNormal (follow-up Codex #2, roadmap v1.2).
    /// Conteudo Revit mal autorado pode reportar BasisZ apontando para DENTRO da
    /// peca; isso inverteria a relacao deflexao = 180 - raw que o
    /// <see cref="TopologyInferenceEngine"/> assume — um BasisZ inward num par
    /// reto vira angulo 0 (joelho) em vez de 180 (luva), casando codigo errado no
    /// catalogo (protege o dinheiro). Funcao PURA (Domain, testavel headless): o
    /// Adapter chama antes de Infer.
    /// </summary>
    public static class OutwardNormalGuard
    {
        /// <summary>
        /// Garante que todo OutwardNormal aponte para FORA do centroide das
        /// Origins. Para cada conector, se dot(normal, origin - centroide) for
        /// negativo (normal apontando para dentro), inverte o normal. Precisa de
        /// >= 2 conectores para um centroide significativo; com 0 ou 1 a lista
        /// volta intacta (Cap nao tem como ser blindado e nem precisa).
        /// </summary>
        public static IReadOnlyList<ConnectorReading> EnsureOutward(IReadOnlyList<ConnectorReading> readings)
        {
            if (readings is null || readings.Count < 2)
            {
                return readings ?? Array.Empty<ConnectorReading>();
            }

            Vector3 centroid = Vector3.Zero;
            for (int i = 0; i < readings.Count; i++)
            {
                centroid += readings[i].Origin;
            }

            centroid /= (float)readings.Count;

            var result = new ConnectorReading[readings.Count];
            for (int i = 0; i < readings.Count; i++)
            {
                ConnectorReading r = readings[i];
                Vector3 outward = r.Origin - centroid;
                // FOLLOW-UP (smoke Fase 4): dot < 0 e sinal claro. Quando dot ~= 0
                // (normal ortogonal ao radial, ou conector praticamente no centroide)
                // o sinal e ambiguo e hoje NAO invertemos; revisitar com epsilon +
                // diagnostic (ou validar pela tangente do tubo conectado) se o smoke
                // mostrar caso real.
                if (Vector3.Dot(r.OutwardNormal, outward) < 0f)
                {
                    result[i] = r with { OutwardNormal = -r.OutwardNormal };
                }
                else
                {
                    result[i] = r;
                }
            }

            return result;
        }
    }
}
