using System.Globalization;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.V2026.Common.Parameters
{
    /// <summary>
    /// Helpers de escrita em <see cref="Parameter"/> respeitando o
    /// <see cref="StorageType"/>. Para <c>Double</c>, prefere
    /// <c>SetValueString</c> para que o Revit interprete a unidade de
    /// exibição (mm, m, °, etc.) e não confunda o usuário com valores
    /// internos em feet/radianos. Devolve <c>true</c>/<c>false</c> em vez
    /// de propagar exceções.
    /// </summary>
    public static class RevitParameterWriter
    {
        public static bool TrySetString(Parameter? p, string? value)
        {
            if (p == null || p.IsReadOnly) return false;
            try { return p.Set(value ?? string.Empty); }
            catch { return false; }
        }

        public static bool TrySetInteger(Parameter? p, int value)
        {
            if (p == null || p.IsReadOnly) return false;
            try { return p.Set(value); }
            catch { return false; }
        }

        public static bool TrySetDouble(Parameter? p, double internalValue)
        {
            if (p == null || p.IsReadOnly) return false;
            try { return p.Set(internalValue); }
            catch { return false; }
        }

        public static bool TrySetElementId(Parameter? p, ElementId? value)
        {
            if (p == null || p.IsReadOnly || value == null) return false;
            try { return p.Set(value); }
            catch { return false; }
        }

        /// <summary>
        /// Escreve respeitando o storage do parâmetro: tenta parsing nativo
        /// quando faz sentido e cai para <c>SetValueString</c> caso o usuário
        /// tenha digitado algo com unidade ("100 mm", "Yes", etc.).
        /// </summary>
        public static bool TrySetFromText(Parameter? p, string? value)
        {
            if (p == null || p.IsReadOnly) return false;

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        return p.Set(value ?? string.Empty);

                    case StorageType.Integer:
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                            return p.Set(i);
                        return p.SetValueString(value ?? string.Empty);

                    case StorageType.Double:
                        return p.SetValueString(value ?? string.Empty);

                    case StorageType.ElementId:
                        return p.SetValueString(value ?? string.Empty);

                    default:
                        return p.SetValueString(value ?? string.Empty);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
