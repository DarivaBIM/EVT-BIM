using System.Text.Json;

namespace DarivaBIM.Sidecar.Ipc
{
    /// <summary>
    /// Wrapper centralizado pro <see cref="JsonSerializer"/> com opcoes
    /// consistentes entre as duas pontas do pipe. Sem isso, divergencia de
    /// PropertyNameCaseInsensitive ou de WriteIndented entre sidecar e plugin
    /// vira bug silencioso de mensagem que sai bem e chega indecifravel.
    /// </summary>
    public static class IpcSerializer
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            // Cliente JS no AcervoBIM pode mandar tanto "familyId" quanto
            // "FamilyId" dependendo do framework; aceitar ambas evita gotcha
            // de capitalizacao quando o desenvolvedor do frontend escreve
            // bridge.importFamily({ FamilyId: 1 }) em vez do esperado.
            PropertyNameCaseInsensitive = true,

            // Linha unica por mensagem — line-delimited JSON e o framing
            // que usamos no wire (uma linha = uma mensagem). WriteIndented
            // quebraria isso.
            WriteIndented = false,
        };

        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, Options);
        }

        public static T? Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        public static T? DeserializeParams<T>(JsonElement element)
        {
            return element.Deserialize<T>(Options);
        }
    }
}
