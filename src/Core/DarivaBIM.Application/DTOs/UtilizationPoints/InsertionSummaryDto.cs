using System.Collections.Generic;

namespace DarivaBIM.Application.DTOs.UtilizationPoints
{
    /// <summary>
    /// Resultado agregado de uma execução de inserção de pontos de utilização.
    /// Reflete o que o algoritmo Python de referência devolvia em
    /// <c>relatorio</c>/<c>avisos</c>, em formato amigável para a WPF.
    /// </summary>
    public sealed class InsertionSummaryDto
    {
        public int ElementsAnalyzed { get; set; }
        public int FreeConnectorsFound { get; set; }
        public int PointsInserted { get; set; }
        public int PointsConnected { get; set; }
        public int ConnectorsWithoutRange { get; set; }
        public int Errors { get; set; }

        public List<InsertionMessageDto> Messages { get; set; } = new();
    }
}
