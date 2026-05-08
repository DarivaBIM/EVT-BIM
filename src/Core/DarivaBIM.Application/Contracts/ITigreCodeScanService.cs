using DarivaBIM.Application.DTOs.Tigre;

namespace DarivaBIM.Application.Contracts
{
    /// <summary>
    /// Lê os tubos do documento ativo e produz um <see cref="TigreScanResult"/>
    /// agrupado por (Tipo, Diâmetro, Status) sem alterar o modelo. É a base
    /// que alimenta a janela "Codificar Tubos" — tudo que o usuário vê na
    /// UI vem daqui.
    /// </summary>
    public interface ITigreCodeScanService
    {
        TigreScanResult Scan();
    }
}
