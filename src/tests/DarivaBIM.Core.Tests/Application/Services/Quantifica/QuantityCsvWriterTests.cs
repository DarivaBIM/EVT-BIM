using DarivaBIM.Application.DTOs.Quantifica;
using DarivaBIM.Application.Services.Quantifica;
using Xunit;

namespace DarivaBIM.Core.Tests.Application.Services.Quantifica
{
    public class QuantityCsvWriterTests
    {
        [Fact]
        public void Write_starts_with_utf8_bom_and_header_row()
        {
            string csv = QuantityCsvWriter.Write(new QuantitySnapshot());

            Assert.StartsWith("﻿", csv);
            string firstLine = csv.Substring(1).Split('\n')[0].TrimEnd('\r');
            // Slice 4.5 — coluna "Sistema" removida. Sistema deixou de
            // ser chave de agrupamento e nao aparece mais no relatorio
            // tabular. Audit Yellow "Sistema ausente" continua via
            // QuantityAuditFinding em paralelo.
            Assert.Equal(
                "Categoria;Família;Tipo;Diâmetro;Cód. Tigre;Descrição;Tigre: Descrição;Fabricante;Qtd;Quantidade;Un;Auditoria",
                firstLine);
        }

        [Fact]
        public void Write_emits_one_row_per_group_after_the_header()
        {
            QuantitySnapshot snapshot = new()
            {
                Groups = new[]
                {
                    new QuantityGroup
                    {
                        Category = "Tubulações",
                        Family = "Tubo - Soldável",
                        Type = "PVC 25mm",
                        Diameter = "25 mm",
                        TigreCode = "47013",
                        Description = "Tubo PVC Soldável",
                        TigreDescription = "Tubo Sold. PVC 25mm",
                        Manufacturer = "Tigre",
                        System = "Água Fria",
                        MeasurementKind = MeasurementKind.LengthMeters,
                        ElementCount = 3,
                        Quantity = 12.5m,
                        IsPipeCurvesCategory = true,
                    },
                },
            };

            string csv = QuantityCsvWriter.Write(snapshot);
            string[] lines = csv.Substring(1).Split('\n');

            Assert.True(lines.Length >= 2);
            string dataLine = lines[1].TrimEnd('\r');
            // Slice 4.5 — coluna Sistema removida do CSV. Linha vai direto
            // de Fabricante pra Qtd; "Água Fria" continua sendo lido pelo
            // scanner pra audit findings, mas nao aparece no relatorio.
            Assert.Equal(
                "Tubulações;Tubo - Soldável;PVC 25mm;25 mm;47013;Tubo PVC Soldável;Tubo Sold. PVC 25mm;Tigre;3;12,50;m;",
                dataLine);
        }

        [Fact]
        public void Write_uses_pt_br_decimal_comma_for_area_and_length()
        {
            QuantitySnapshot snapshot = new()
            {
                Groups = new[]
                {
                    new QuantityGroup
                    {
                        Category = "Paredes",
                        Family = "Parede Básica",
                        Type = "200mm",
                        MeasurementKind = MeasurementKind.AreaSquareMeters,
                        ElementCount = 4,
                        Quantity = 187.345m,
                    },
                },
            };

            string csv = QuantityCsvWriter.Write(snapshot);

            Assert.Contains(";4;187,35;m²;", csv);
        }

        [Fact]
        public void Write_escapes_separator_in_field_with_double_quotes()
        {
            QuantitySnapshot snapshot = new()
            {
                Groups = new[]
                {
                    new QuantityGroup
                    {
                        Category = "Conexões",
                        Family = "Tê",
                        Type = "PVC; soldável",   // contém separador
                        Description = "linha com \"aspas\"",
                        MeasurementKind = MeasurementKind.Count,
                        ElementCount = 2,
                        Quantity = 2m,
                    },
                },
            };

            string csv = QuantityCsvWriter.Write(snapshot);

            // Campo com ; vira "PVC; soldável"
            Assert.Contains("\"PVC; soldável\"", csv);
            // Campo com aspas vira "linha com ""aspas"""
            Assert.Contains("\"linha com \"\"aspas\"\"\"", csv);
        }

        [Fact]
        public void Write_renders_audit_note_in_last_column_when_present()
        {
            QuantitySnapshot snapshot = new()
            {
                Groups = new[]
                {
                    new QuantityGroup
                    {
                        Category = "Tubulações",
                        Family = "Tubo Soldável",
                        Type = "PVC 32mm",
                        Diameter = "32 mm",
                        MeasurementKind = MeasurementKind.LengthMeters,
                        ElementCount = 1,
                        Quantity = 3m,
                        IsPipeCurvesCategory = true,
                        AuditNote = "Sem código Tigre",
                    },
                },
            };

            string csv = QuantityCsvWriter.Write(snapshot);

            Assert.EndsWith(";Sem código Tigre\r\n", csv);
        }

        [Fact]
        public void Write_returns_only_header_when_snapshot_has_no_groups()
        {
            string csv = QuantityCsvWriter.Write(new QuantitySnapshot());

            // BOM + header + \r\n; nada além disso.
            string[] lines = csv.Substring(1).Split(new[] { "\r\n" }, System.StringSplitOptions.None);
            Assert.Equal(2, lines.Length);          // header + linha vazia final (split do "\r\n" final)
            Assert.Equal(string.Empty, lines[1]);
        }

        [Fact]
        public void Write_emits_tigre_description_after_description_column()
        {
            QuantitySnapshot snapshot = new()
            {
                Groups = new[]
                {
                    new QuantityGroup
                    {
                        Category = "Conexões de tubulação",
                        Family = "Joelho Soldável",
                        Type = "PVC 25mm",
                        Description = "Joelho 90°",
                        TigreCode = "12345",
                        TigreDescription = "Joelho Sold. 25mm Tigre",
                        MeasurementKind = MeasurementKind.Count,
                        ElementCount = 4,
                        Quantity = 4m,
                    },
                },
            };

            string csv = QuantityCsvWriter.Write(snapshot);

            // Posicionamento: após "Joelho 90°" (Descrição) vem
            // "Joelho Sold. 25mm Tigre" (Tigre: Descrição), depois ;
            // (Fabricante vazio).
            Assert.Contains(";Joelho 90°;Joelho Sold. 25mm Tigre;;", csv);
        }

        [Fact]
        public void Write_does_not_emit_system_column_even_when_dto_has_system()
        {
            // Slice 4.5 — Sistema saiu do CSV. Mesmo que o scanner deixasse
            // System preenchido no DTO (legado / instance fallback raro),
            // o writer NAO inclui essa coluna. Este test protege contra
            // regressao acidental que volte a serializar System.
            QuantitySnapshot snapshot = new()
            {
                Groups = new[]
                {
                    new QuantityGroup
                    {
                        Category = "Tubulações",
                        Family = "Tubo Sold",
                        Type = "PVC 25",
                        Description = "Tubo",
                        Manufacturer = "Tigre",
                        System = "Água Fria",
                        MeasurementKind = MeasurementKind.LengthMeters,
                        ElementCount = 1,
                        Quantity = 4m,
                    },
                },
            };

            string csv = QuantityCsvWriter.Write(snapshot);

            Assert.DoesNotContain(";Água Fria;", csv);
            Assert.DoesNotContain("Sistema", csv);
        }

        [Fact]
        public void Write_renders_empty_tigre_description_when_null()
        {
            // Grupo sem Tigre: Descrição (família não-Tigre ou param vazio).
            // F6-LITE — coluna existe mas valor fica vazio; não quebra layout.
            QuantitySnapshot snapshot = new()
            {
                Groups = new[]
                {
                    new QuantityGroup
                    {
                        Category = "Tubulações",
                        Family = "Generic",
                        Type = "X",
                        Description = "qualquer",
                        TigreDescription = null,
                        MeasurementKind = MeasurementKind.LengthMeters,
                        ElementCount = 1,
                        Quantity = 5m,
                    },
                },
            };

            string csv = QuantityCsvWriter.Write(snapshot);

            // Entre "qualquer" e Fabricante vazio fica ";;"
            Assert.Contains(";qualquer;;", csv);
        }
    }
}
