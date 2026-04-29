using System.Globalization;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.V2026.Features.TigreCodes
{
    /// <summary>
    /// Resultado da escrita de um código Tigre em um <see cref="Parameter"/>.
    /// </summary>
    internal enum TigreWriteOutcome
    {
        AlreadyOk,
        Overwritten,
        ParameterIssue,
    }

    /// <summary>
    /// Escreve o código Tigre (inteiro) no parâmetro alvo de um tubo,
    /// considerando o <see cref="StorageType"/>: integer (set direto), string
    /// (converte para texto invariant) ou outros (cai no SetValueString).
    /// </summary>
    internal static class TigreCodeWriter
    {
        public static TigreWriteOutcome Write(Parameter target, int code)
        {
            if (target == null || target.IsReadOnly)
                return TigreWriteOutcome.ParameterIssue;

            try
            {
                switch (target.StorageType)
                {
                    case StorageType.Integer:
                    {
                        int current = target.AsInteger();
                        target.Set(code);
                        return current == code ? TigreWriteOutcome.AlreadyOk : TigreWriteOutcome.Overwritten;
                    }
                    case StorageType.String:
                    {
                        string current = target.AsString() ?? string.Empty;
                        string codeStr = code.ToString(CultureInfo.InvariantCulture);
                        target.Set(codeStr);
                        return current == codeStr ? TigreWriteOutcome.AlreadyOk : TigreWriteOutcome.Overwritten;
                    }
                    default:
                        target.Set(code.ToString(CultureInfo.InvariantCulture));
                        return TigreWriteOutcome.Overwritten;
                }
            }
            catch
            {
                return TigreWriteOutcome.ParameterIssue;
            }
        }
    }
}
