using System;
using System.Globalization;
using DarivaBIM.Application.DTOs.Quantifica;

namespace DarivaBIM.Presentation.Wpf.TigreQuantifica
{
    /// <summary>
    /// Façade read-only de uma linha do relatório. Não herda
    /// <c>ObservableObject</c> de propósito — depois do <c>ApplyScan</c> o
    /// snapshot é imutável; mudanças vêm sempre por re-scan, não por edição
    /// in-place.
    /// </summary>
    public sealed class QuantityGroupViewModel
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        private readonly QuantityGroup _group;

        public QuantityGroupViewModel(QuantityGroup group)
        {
            _group = group;
        }

        public string Category => _group.Category;
        public string Family => _group.Family;
        public string Type => _group.Type;
        public string Diameter => _group.Diameter ?? string.Empty;
        public string TigreCode => _group.TigreCode ?? string.Empty;
        public string Description => _group.Description;
        public string TigreDescription => _group.TigreDescription ?? string.Empty;
        public string Manufacturer => _group.Manufacturer ?? string.Empty;
        // Slice 4.5 — System deixou de ser exibido na tabela e no CSV,
        // mas continua exposto pra consumers que filtram/buscam (F3 do
        // slice-43b usa em MatchesFilter). Sempre retorna string.Empty
        // após o Slice 4.5 porque o scanner zera o campo no DTO
        // agregado — a busca por sistema fica não-funcional até a próxima
        // iteração que decida se Sistema entra de alguma outra forma.
        public string System => _group.System ?? string.Empty;
        public string Unit => _group.MeasurementKind.ToUnitLabel();
        public int ElementCount => _group.ElementCount;

        /// <summary>
        /// Slice 4.5 — texto da coluna unificada ELEMENTO da tabela.
        /// Prefere <c>TigreDescription</c> (preenchido pelas familias do
        /// catalogo Tigre via F6-LITE) e cai pra "Familia · Tipo" quando
        /// ausente — coincide com a heuristica que o PipeCodes
        /// (Codificar Tigre) ja usa pra exibir elementos no relatorio.
        /// O tooltip XAML continua mostrando Familia/Tipo separados pra
        /// inspecao, e o CSV mantem as colunas separadas no export.
        /// </summary>
        public string ElementText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_group.TigreDescription))
                    return _group.TigreDescription!;

                string family = _group.Family ?? string.Empty;
                string type = _group.Type ?? string.Empty;
                if (string.IsNullOrEmpty(family))
                    return type;
                // Usa global::System.StringComparison pra contornar a colisao
                // com a propriedade publica "System" desta classe (o resolver
                // do C# acha a propriedade antes do namespace System dentro
                // do corpo do tipo).
                if (string.Equals(family, type, global::System.StringComparison.Ordinal))
                    return type;
                return $"{family} · {type}";
            }
        }

        public string QuantityText
        {
            get
            {
                string format = _group.MeasurementKind == MeasurementKind.Count ? "0" : "0.00";
                return _group.Quantity.ToString(format, PtBr);
            }
        }

        public string AuditNote => _group.AuditNote ?? string.Empty;
        public bool HasAuditNote => !string.IsNullOrWhiteSpace(_group.AuditNote);

        public bool IsPipeCurvesCategory => _group.IsPipeCurvesCategory;

        /// <summary>
        /// <c>true</c> quando esta linha é de tubulação e o código Tigre
        /// ainda não foi preenchido. Usado em conjunto com
        /// <see cref="QuantityCategoryViewModel.HasPipeCurvesCategory"/> pra
        /// alimentar o banner "Codificar Tubos antes" do slice 1.6 F1.
        /// </summary>
        public bool IsPipeWithoutCode =>
            _group.IsPipeCurvesCategory && string.IsNullOrWhiteSpace(_group.TigreCode);
    }
}
