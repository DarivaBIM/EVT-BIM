namespace DarivaBIM.Application.DTOs.Tigre
{
    /// <summary>
    /// Estado de um tubo (ou grupo de tubos com mesmo Tipo + Diâmetro) frente
    /// ao catálogo Tigre. Os quatro estados alimentam diretamente as quatro
    /// caixinhas coloridas da janela "Codificar Tubos".
    /// </summary>
    public enum TigrePipeStatus
    {
        /// <summary>
        /// Tubo cuja descrição/segmento/tipo + diâmetro não casa com nenhuma
        /// entrada do catálogo. Vermelho — exibido só como informação.
        /// </summary>
        NoMatch = 0,

        /// <summary>
        /// Tubo já tem o parâmetro Tigre: Código preenchido, mas o valor
        /// gravado é diferente do código que o catálogo casaria hoje. Laranja —
        /// permite ao usuário sobrescrever.
        /// </summary>
        Divergent = 1,

        /// <summary>
        /// Tubo casa com uma entrada do catálogo, mas o parâmetro Tigre: Código
        /// está vazio (ou ausente). Amarelo — pendente de inserção.
        /// </summary>
        Missing = 2,

        /// <summary>
        /// Tubo casa com uma entrada do catálogo e o parâmetro Tigre: Código
        /// já está com o valor correto. Verde — nada a fazer.
        /// </summary>
        Ok = 3,
    }
}
