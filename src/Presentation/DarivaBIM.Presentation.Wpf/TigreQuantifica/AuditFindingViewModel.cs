using DarivaBIM.Application.DTOs.Quantifica;

namespace DarivaBIM.Presentation.Wpf.TigreQuantifica
{
    /// <summary>
    /// Façade de uma <see cref="QuantityAuditFinding"/>. A cor vermelha vs
    /// amarela é expressa via <see cref="SeverityKey"/> — o XAML mapeia a
    /// key pro brush, não fica acoplado a System.Windows.Media nesta camada.
    /// </summary>
    public sealed class AuditFindingViewModel
    {
        private readonly QuantityAuditFinding _finding;

        public AuditFindingViewModel(QuantityAuditFinding finding)
        {
            _finding = finding;
        }

        public string FamilyType => _finding.FamilyType;

        public string MissingFieldsText
        {
            get
            {
                if (_finding.MissingFields == null || _finding.MissingFields.Count == 0)
                    return string.Empty;
                return string.Join(", ", _finding.MissingFields);
            }
        }

        public AuditSeverity Severity => _finding.Severity;

        /// <summary>
        /// Chave textual da severidade pro DataTrigger do XAML
        /// (<c>"Red"</c>/<c>"Yellow"</c>). Mantém Presentation sem
        /// dependência de System.Windows.Media — alinhado com
        /// <c>LayerIsolationTests.PresentationWpf_does_not_reference_RevitAPI</c>
        /// e com o ban de System.Windows que pode aparecer em testes futuros.
        /// </summary>
        public string SeverityKey => _finding.Severity == AuditSeverity.Red ? "Red" : "Yellow";

        public string ElementIdText =>
            _finding.ElementId.HasValue
                ? _finding.ElementId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty;
    }
}
