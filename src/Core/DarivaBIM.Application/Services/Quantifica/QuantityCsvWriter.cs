using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DarivaBIM.Application.DTOs.Quantifica;

namespace DarivaBIM.Application.Services.Quantifica
{
    /// <summary>
    /// Serializa um <see cref="QuantitySnapshot"/> em CSV pt-BR (separador
    /// <c>;</c>, vírgula decimal, BOM UTF-8 no início pra Excel reconhecer
    /// acentuação). Pure function — quem grava em disco é o code-behind da
    /// janela. Isolar o I/O permite testar o output headless e sem mock de
    /// SaveFileDialog.
    /// </summary>
    public static class QuantityCsvWriter
    {
        /// <summary>
        /// Marca de BOM UTF-8 (U+FEFF) embutida no início da string. Quem
        /// chama deve gravar com <c>UTF8Encoding(false)</c> (sem BOM
        /// adicional do encoder), senão fica BOM duplicado.
        /// </summary>
        public const string Utf8ByteOrderMark = "﻿";

        private const char Separator = ';';
        private const string LineEnding = "\r\n";

        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        // F6-LITE — Coluna "Tigre: Descrição" entra entre "Descrição" e
        // "Fabricante". Posicionamento mantém agrupamento lógico (descrições
        // próximas, depois metadados de identidade do fornecedor/sistema,
        // depois quantitativos). Ordem das colunas é parte de contrato com
        // Tigre — qualquer reordenação deve sair via memória do projeto.
        private static readonly string[] HeaderColumns =
        {
            "Categoria",
            "Família",
            "Tipo",
            "Diâmetro",
            "Cód. Tigre",
            "Descrição",
            "Tigre: Descrição",
            "Fabricante",
            "Sistema",
            "Qtd",
            "Quantidade",
            "Un",
            "Auditoria",
        };

        public static string Write(QuantitySnapshot snapshot)
        {
            StringBuilder sb = new(2048);
            sb.Append(Utf8ByteOrderMark);
            AppendRow(sb, HeaderColumns);

            if (snapshot == null || snapshot.Groups == null)
                return sb.ToString();

            foreach (QuantityGroup group in snapshot.Groups)
            {
                AppendRow(sb, BuildGroupRow(group));
            }

            return sb.ToString();
        }

        private static IReadOnlyList<string> BuildGroupRow(QuantityGroup group)
        {
            return new[]
            {
                group.Category ?? string.Empty,
                group.Family ?? string.Empty,
                group.Type ?? string.Empty,
                group.Diameter ?? string.Empty,
                group.TigreCode ?? string.Empty,
                group.Description ?? string.Empty,
                group.TigreDescription ?? string.Empty,
                group.Manufacturer ?? string.Empty,
                group.System ?? string.Empty,
                group.ElementCount.ToString(PtBr),
                FormatQuantity(group.Quantity, group.MeasurementKind),
                group.MeasurementKind.ToUnitLabel(),
                group.AuditNote ?? string.Empty,
            };
        }

        private static string FormatQuantity(decimal quantity, MeasurementKind kind)
        {
            // Count → inteiro (combina com ElementCount); demais → 2 casas decimais.
            string format = kind == MeasurementKind.Count ? "0" : "0.00";
            return quantity.ToString(format, PtBr);
        }

        private static void AppendRow(StringBuilder sb, IReadOnlyList<string> fields)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                    sb.Append(Separator);
                sb.Append(EscapeField(fields[i]));
            }
            sb.Append(LineEnding);
        }

        private static string EscapeField(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool needsQuoting =
                value.IndexOf(Separator) >= 0 ||
                value.IndexOf('"') >= 0 ||
                value.IndexOf('\n') >= 0 ||
                value.IndexOf('\r') >= 0;

            if (!needsQuoting)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
