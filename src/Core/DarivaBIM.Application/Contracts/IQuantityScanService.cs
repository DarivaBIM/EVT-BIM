using DarivaBIM.Application.DTOs.Quantifica;

namespace DarivaBIM.Application.Contracts
{
    /// <summary>
    /// Contrato lido pelo <c>GenerateQuantitySnapshotUseCase</c>: produz um
    /// snapshot completo do projeto (cabeçalho + grupos quantitativos +
    /// findings) sem abrir transação. A implementação Revit-side mora em
    /// <c>DarivaBIM.Revit.Adapters.Features.TigreQuantifica.QuantityScanner</c>.
    /// </summary>
    public interface IQuantityScanService
    {
        /// <summary>
        /// Lê o projeto e devolve o snapshot. Em caso de documento de família
        /// ou modelo vazio, retorna um snapshot com <c>ErrorMessage</c>
        /// preenchida (não lança).
        /// </summary>
        QuantitySnapshot Scan();
    }
}
