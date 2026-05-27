namespace DarivaBIM.Application.DTOs.Tigre
{
    /// <summary>
    /// Detalha um elemento que o applier do Codificar Tigre não conseguiu
    /// gravar — tipicamente família IsTigre sem parâmetro <c>Tigre: Código</c>
    /// disponível nem no instance nem no type. Slice 3.3 substitui o
    /// contador flat <see cref="TigreSelectiveApplyResult.ParameterIssue"/>
    /// por listas detalhadas que a UI pode exibir individualmente.
    /// </summary>
    public sealed class TigreApplyIssue
    {
        public TigreApplyIssue(long elementId, string familyName, string reason)
        {
            ElementId = elementId;
            FamilyName = familyName ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public long ElementId { get; }

        /// <summary>Nome da família responsável (vazio se elemento órfão).</summary>
        public string FamilyName { get; }

        /// <summary>
        /// Texto explicativo curto exibível ao usuário. Ex.: "Tigre: Código
        /// não disponível no instance nem no type da família 'X'".
        /// </summary>
        public string Reason { get; }
    }
}
