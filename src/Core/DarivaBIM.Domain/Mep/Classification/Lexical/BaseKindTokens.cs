using System;
using System.Collections.Generic;

namespace DarivaBIM.Domain.Mep.Classification.Lexical
{
    /// <summary>
    /// BaseKind (chave lowercase) -> tokens lexicais pt-BR que o sinalizam (secao
    /// 10.2 do rulebook), na forma NORMALIZADA (sem acento). Consumido pelo Classify
    /// (fase 2.B) no score lexical hibrido; NAO e usado pelo <see cref="LexicalNormalizer"/>
    /// (que so tokeniza texto). Mora aqui porque e dado lexical do modulo, junto dos
    /// aliases.
    /// </summary>
    public static class BaseKindTokens
    {
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Map =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["elbow"] = new[] { "joelho", "curva" },
                ["tee"] = new[] { "te" },
                ["wye"] = new[] { "juncao", "wye" },
                ["union"] = new[] { "luva", "uniao" },
                ["reducer"] = new[] { "reducao", "bucha", "redutor" },
                ["cap"] = new[] { "cap", "tampao", "plug" },
                ["valve"] = new[] { "registro", "valvula", "esfera", "gaveta", "retencao" },
                ["cross"] = new[] { "cruzeta", "cross" },
                ["multiport"] = new[] { "manifold", "barrilete", "distribuidor", "coletor" },
            };
    }
}
