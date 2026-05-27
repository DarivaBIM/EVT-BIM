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
        public string Manufacturer => _group.Manufacturer ?? string.Empty;
        public string System => _group.System ?? string.Empty;
        public string Unit => _group.MeasurementKind.ToUnitLabel();
        public int ElementCount => _group.ElementCount;
        public string ElementCountText => _group.ElementCount.ToString(PtBr);

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
