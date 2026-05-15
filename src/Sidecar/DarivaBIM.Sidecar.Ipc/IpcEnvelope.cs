using System.Text.Json.Serialization;

namespace DarivaBIM.Sidecar.Ipc
{
    /// <summary>
    /// Envelope de requisicao no protocolo wire. Cada mensagem do sidecar para
    /// o plugin segue este formato; o plugin responde com <see cref="IpcResponse"/>
    /// preservando o mesmo <see cref="Id"/>.
    ///
    /// Estilo inspirado em JSON-RPC 2.0 minimo — sem version field, sem batch
    /// requests, sem notifications (toda chamada espera resposta).
    /// </summary>
    public class IpcRequest
    {
        /// <summary>
        /// ID gerado pelo cliente (sidecar). Plugin ecoa de volta na resposta
        /// pra que o cliente correlacione com a Task pendente.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Nome do metodo. Conhecidos atualmente: "importFamily", "ping".
        /// Servidor responde com erro -32601 (MethodNotFound) se desconhecido.
        /// </summary>
        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        /// <summary>
        /// Payload tipado pelo metodo. JSON cru — o servidor deserializa pra
        /// classe especifica baseado em <see cref="Method"/>.
        /// </summary>
        [JsonPropertyName("params")]
        public System.Text.Json.JsonElement Params { get; set; }
    }

    /// <summary>
    /// Envelope de resposta. Exatamente um entre <see cref="Result"/> e
    /// <see cref="Error"/> e populado. Sucesso devolve Result (pode ser
    /// objeto vazio); erro devolve Error.
    /// </summary>
    public class IpcResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("result")]
        public System.Text.Json.JsonElement? Result { get; set; }

        [JsonPropertyName("error")]
        public IpcError? Error { get; set; }

        public static IpcResponse Ok(string id, System.Text.Json.JsonElement? result = null)
        {
            return new IpcResponse
            {
                Id = id,
                Result = result,
            };
        }

        public static IpcResponse Fail(string id, int code, string message)
        {
            return new IpcResponse
            {
                Id = id,
                Error = new IpcError { Code = code, Message = message },
            };
        }
    }

    public class IpcError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Codigos de erro padrao no protocolo. Espelham JSON-RPC 2.0 onde fizer
    /// sentido, mais codigos do dominio nas faixas privadas.
    /// </summary>
    public static class IpcErrorCodes
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;

        // Faixa do dominio (positivos).
        public const int DownloadFailed = 100;
        public const int FamilyLoadFailed = 101;
        public const int NoActiveDocument = 102;
        public const int UnsupportedView = 103;
    }
}
