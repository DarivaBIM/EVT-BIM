using System.Collections.Generic;

namespace DarivaBIM.Revit.Adapters.Features.FloorDrainExtension
{
    /// <summary>
    /// Resultado de uma execução de <see cref="FloorDrainExtensionCreator"/>.
    /// <see cref="Logs"/> é a trilha por caixa, útil tanto para o usuário
    /// quanto para depuração quando nada é criado.
    /// </summary>
    public sealed class FloorDrainExtensionResult
    {
        public int Created { get; set; }
        public int FailedNoVerticalConnector { get; set; }
        public int FailedNoPipeType { get; set; }
        public int FailedOther { get; set; }
        public List<string> Logs { get; } = new();
    }
}
