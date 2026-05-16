using System.Text.Json.Serialization;

namespace DarivaBIM.Sidecar.Ipc.Methods
{
    /// <summary>
    /// Parametros do metodo "importFamily". Espelha (subset de) o
    /// <c>DarivaBIM.Application.DTOs.Family.ImportFamilyRequest</c> do plugin,
    /// mas vive aqui pra que o sidecar EXE nao precise referenciar a camada
    /// Application do Revit.
    ///
    /// Apenas os campos necessarios para download + identificacao ficam aqui;
    /// metadados de UI (ImageUrl, Youtube, etc.) sao responsabilidade do
    /// AcervoBIM e nao precisam atravessar o pipe.
    /// </summary>
    public class ImportFamilyParams
    {
        [JsonPropertyName("familyId")]
        public int FamilyId { get; set; }

        [JsonPropertyName("familyName")]
        public string FamilyName { get; set; } = string.Empty;

        [JsonPropertyName("manufacturerName")]
        public string ManufacturerName { get; set; } = string.Empty;

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("latestVersion")]
        public int LatestVersion { get; set; }
    }

    /// <summary>
    /// Resultado do metodo "importFamily" quando ele e enfileirado com sucesso
    /// no Revit. Note que <em>nao</em> espera placement do usuario — o
    /// PostRequestForElementTypePlacement do Revit nao notifica conclusao,
    /// entao a resposta volta logo apos a familia ser carregada e o tipo
    /// ativado.
    /// </summary>
    public class ImportFamilyResult
    {
        [JsonPropertyName("loadedFamilyName")]
        public string LoadedFamilyName { get; set; } = string.Empty;

        [JsonPropertyName("cachedFilePath")]
        public string CachedFilePath { get; set; } = string.Empty;
    }
}
