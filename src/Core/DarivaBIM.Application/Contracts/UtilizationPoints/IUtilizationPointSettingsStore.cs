using DarivaBIM.Application.DTOs.UtilizationPoints;

namespace DarivaBIM.Application.Contracts.UtilizationPoints
{
    /// <summary>
    /// Persistência das configurações da ferramenta "Inserir Pontos de
    /// Utilização". A implementação concreta vive em Infrastructure e grava o
    /// JSON em <c>%AppData%\DarivaBIM\EVT-BIM\UtilizationPoints\profiles.json</c>.
    /// </summary>
    public interface IUtilizationPointSettingsStore
    {
        /// <summary>
        /// Carrega os grupos persistidos. Se o arquivo não existir ou estiver
        /// corrompido, deve devolver um <see cref="UtilizationPointProfilesDto"/>
        /// vazio (versionado), nunca lançar.
        /// </summary>
        UtilizationPointProfilesDto Load();

        /// <summary>
        /// Sobrescreve o arquivo de configurações de forma best-effort: falhas
        /// de IO devem ser engolidas para não derrubar a janela WPF.
        /// </summary>
        void Save(UtilizationPointProfilesDto profiles);
    }
}
