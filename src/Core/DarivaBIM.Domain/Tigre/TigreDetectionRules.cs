using System;
using System.Collections.Generic;
using System.Linq;

namespace DarivaBIM.Domain.Tigre
{
    /// <summary>
    /// Sinais que o <see cref="TigreDetectionRules"/> dispara ao avaliar um
    /// elemento. Útil pra debug/log do veredito sem perder a procedência.
    /// </summary>
    public enum TigreDetectionSignal
    {
        None,
        ExistingCodeMatch,
        ManufacturerVeto,
        ManufacturerTigre,
        FamilyNameContainsTigre,
        DistinctiveBrandToken,
        DescriptionMentionsTigre,
    }

    public readonly struct TigreDetectionResult : IEquatable<TigreDetectionResult>
    {
        public TigreDetectionResult(bool isTigre, TigreDetectionSignal signal)
        {
            IsTigre = isTigre;
            Signal = signal;
        }

        public bool IsTigre { get; }
        public TigreDetectionSignal Signal { get; }

        public bool Equals(TigreDetectionResult other)
            => IsTigre == other.IsTigre && Signal == other.Signal;

        public override bool Equals(object? obj)
            => obj is TigreDetectionResult r && Equals(r);

        public override int GetHashCode()
            => (IsTigre.GetHashCode() * 397) ^ (int)Signal;

        public static bool operator ==(TigreDetectionResult a, TigreDetectionResult b) => a.Equals(b);
        public static bool operator !=(TigreDetectionResult a, TigreDetectionResult b) => !a.Equals(b);

        public override string ToString() => $"IsTigre={IsTigre} ({Signal})";
    }

    /// <summary>
    /// Heurística pura (sem RevitAPI) que classifica se um elemento é
    /// Tigre, em 6 sinais ordenados:
    ///
    /// Sinal 0 — Existing code preenchido E presente no catálogo → TRUE.
    ///           Trumpa qualquer veto.
    /// Sinal 1 — Veto Manufacturer: presente E sem token "tigre" → FALSE.
    /// Sinal 2 — Manufacturer com token "tigre" → TRUE.
    /// Sinal 3 — Family.Name contém "tigre" (normalizado) → TRUE.
    /// Sinal 4 — Combined(family+description) com token de marca
    ///           distintiva Tigre → TRUE.
    /// Sinal 5 — Description menciona "tigre" → TRUE.
    /// Default — FALSE.
    ///
    /// Veto Manufacturer (Sinal 1) é estrito por design (Codex blocker
    /// do 2C): qualquer Manufacturer não-Tigre marca o elemento como
    /// não-Tigre, mesmo se família/descrição mencionam linha Tigre.
    /// Famílias Knauf/Amanco frequentemente herdam descrições copiadas
    /// de pranchas Tigre legacy, e o veto impede falso positivo no
    /// audit do Tigre Quantifica.
    /// </summary>
    public static class TigreDetectionRules
    {
        /// <summary>
        /// Tokens que identificam linha/marca EXCLUSIVA Tigre. Aparecer
        /// em family+description é evidência forte de família Tigre.
        /// Comparação após TigreTextUtils.Normalize (lowercase + sem
        /// acentos): "AQUATHERM" → "aquatherm".
        ///
        /// O token "tigre" propriamente NÃO entra aqui — Sinal 5
        /// (DescriptionMentionsTigre) cobre esse caso especificamente
        /// pra que o veredito do detector reporte a razão certa.
        ///
        /// Codex HIGH#5 fix: removidos "soldavel", "roscavel", "sr", "sn"
        /// — esses são termos hidráulicos brasileiros GENÉRICOS (não
        /// exclusivos Tigre). Falso positivo concreto:
        /// `Astra_Registro_Soldavel` com Manufacturer vazio era
        /// classificado como Tigre via Sinal 4. Os 5 tokens restantes
        /// (aquatherm/clicpex/ppr/redux/tigrefire) são linhas de produto
        /// que Tigre identifica comercialmente.
        /// </summary>
        public static readonly ISet<string> DistinctiveBrandTokens =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "aquatherm", "clicpex", "ppr", "redux", "tigrefire",
            };

        /// <summary>
        /// Lista (não-exaustiva) de fabricantes concorrentes conhecidos.
        /// Não é usada diretamente no veto — o veto pega QUALQUER
        /// Manufacturer não-Tigre — mas serve de documentação e pode ser
        /// consumida por logs/diagnóstico futuros.
        /// </summary>
        public static readonly ISet<string> KnownCompetitorManufacturers =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "knauf", "amanco", "astra", "krona", "plastilit",
                "fortlev", "brasilit", "eternit", "preserve", "tubocon",
            };

        public static TigreDetectionResult Detect(
            string? familyName,
            string? manufacturer,
            string? description,
            int? existingCode,
            TigreCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            // Sinal 0 — código pré-existente catalógico trumpa qualquer veto.
            if (existingCode.HasValue && catalog.HasCode(existingCode.Value))
                return new TigreDetectionResult(true, TigreDetectionSignal.ExistingCodeMatch);

            // Sinais 1 e 2 — análise do Manufacturer (token "tigre" como
            // palavra inteira, não substring, pra evitar falso positivo
            // tipo "Petigreva").
            string manufacturerNorm = TigreTextUtils.Normalize(manufacturer);
            if (!string.IsNullOrWhiteSpace(manufacturerNorm))
            {
                HashSet<string> mfgTokens = new HashSet<string>(
                    Split(manufacturerNorm),
                    StringComparer.Ordinal);
                if (mfgTokens.Contains("tigre"))
                    return new TigreDetectionResult(true, TigreDetectionSignal.ManufacturerTigre);

                // Manufacturer presente mas sem token "tigre" — veto
                // explícito. Conservador: trumpa Family/Description.
                return new TigreDetectionResult(false, TigreDetectionSignal.ManufacturerVeto);
            }

            // Sinal 3 — Family.Name contém "tigre" como palavra inteira.
            string familyNorm = TigreTextUtils.Normalize(familyName);
            if (!string.IsNullOrWhiteSpace(familyNorm))
            {
                HashSet<string> familyTokens = new HashSet<string>(
                    Split(familyNorm),
                    StringComparer.Ordinal);
                if (familyTokens.Contains("tigre"))
                    return new TigreDetectionResult(true, TigreDetectionSignal.FamilyNameContainsTigre);
            }

            // Sinal 4 — combined(family+description) tem token de marca
            // distintiva Tigre.
            string combinedRaw = (familyName ?? string.Empty) + " " +
                                 (description ?? string.Empty);
            string combinedNorm = TigreTextUtils.Normalize(combinedRaw);
            HashSet<string> queryTokens = new HashSet<string>(
                Split(combinedNorm),
                StringComparer.Ordinal);
            if (queryTokens.Overlaps(DistinctiveBrandTokens))
                return new TigreDetectionResult(true, TigreDetectionSignal.DistinctiveBrandToken);

            // Sinal 5 — Description menciona "tigre" (palavra inteira).
            string descNorm = TigreTextUtils.Normalize(description);
            if (!string.IsNullOrWhiteSpace(descNorm))
            {
                HashSet<string> descTokens = new HashSet<string>(
                    Split(descNorm),
                    StringComparer.Ordinal);
                if (descTokens.Contains("tigre"))
                    return new TigreDetectionResult(true, TigreDetectionSignal.DescriptionMentionsTigre);
            }

            return new TigreDetectionResult(false, TigreDetectionSignal.None);
        }

        private static readonly char[] _spaceSplit = new[] { ' ' };

        private static IEnumerable<string> Split(string s)
            => s.Split(_spaceSplit, StringSplitOptions.RemoveEmptyEntries);
    }
}
