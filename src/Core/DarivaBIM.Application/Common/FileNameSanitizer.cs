using System;
using System.IO;

namespace DarivaBIM.Application.Common
{
    /// <summary>
    /// Helpers para gerar nomes de arquivo/segmento de path seguros a partir
    /// de strings vindas da API ou do usuário. Centraliza a regra antes
    /// duplicada em <c>ImportFamilyRequest</c> e <c>FamilyCacheService</c>:
    /// substitui caracteres inválidos por <c>_</c>, colapsa espaços, faz
    /// trim e devolve um fallback quando o resultado fica vazio.
    /// </summary>
    public static class FileNameSanitizer
    {
        public static string Sanitize(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] chars = value!.Trim().ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalidChars, chars[i]) >= 0)
                    chars[i] = '_';
            }

            string sanitized = new string(chars);

            while (sanitized.IndexOf("  ", StringComparison.Ordinal) >= 0)
                sanitized = sanitized.Replace("  ", " ");

            sanitized = sanitized.Trim();

            return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
        }
    }
}
