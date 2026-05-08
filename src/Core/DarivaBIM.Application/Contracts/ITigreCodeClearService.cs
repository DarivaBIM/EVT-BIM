using System.Collections.Generic;
using DarivaBIM.Application.DTOs.Tigre;

namespace DarivaBIM.Application.Contracts
{
    /// <summary>
    /// Zera o valor do parâmetro Tigre: Código nos tubos selecionados. Não
    /// remove o binding do shared parameter — apenas apaga o valor gravado
    /// (Integer → 0; String → vazio).
    /// </summary>
    public interface ITigreCodeClearService
    {
        TigreClearResult Clear(IReadOnlyList<long> elementIds);
    }
}
