using DarivaBIM.Domain.Hydraulics.UtilizationPoints;

namespace DarivaBIM.Application.DTOs.UtilizationPoints
{
    /// <summary>
    /// Linha do log da última execução de inserção: o que aconteceu com cada
    /// conector processado. A WPF consome essa coleção para mostrar a área de
    /// mensagens. Não carrega tipos Revit.
    /// </summary>
    public sealed class InsertionMessageDto
    {
        public InsertionMessageDto(
            UtilizationPointInsertionOutcome outcome,
            string text)
        {
            Outcome = outcome;
            Text = text ?? string.Empty;
        }

        public UtilizationPointInsertionOutcome Outcome { get; }
        public string Text { get; }
    }
}
