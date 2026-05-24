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
        /// (.rfa), retorna um snapshot com <c>ErrorMessage</c> preenchida
        /// (não lança). Modelo sem elementos nas categorias mapeadas é
        /// estado válido — devolve snapshot com <c>Groups</c> vazio e
        /// <c>ProjectInfo</c> lido normalmente; a UI mostra um status
        /// amigável em vez de erro.
        /// </summary>
        QuantitySnapshot Scan();
    }
}
