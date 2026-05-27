using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;
using DarivaBIM.Application.DTOs.Quantifica;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.TigreQuantifica
{
    /// <summary>
    /// Façade de uma <see cref="QuantityAuditFinding"/>. A cor vermelha vs
    /// amarela é expressa via <see cref="SeverityKey"/> — o XAML mapeia a
    /// key pro brush, não fica acoplado a System.Windows.Media nesta camada.
    ///
    /// Slice 4.3.A F2 — ganha <see cref="SelectInRevitCommand"/> que aciona
    /// um callback (injetado pelo code-behind via
    /// <see cref="TigreQuantificaViewModel.SelectInRevitCallback"/>).
    /// Slice 4.3.A F1 ampliado — ganha <see cref="CorrigirAgoraCommand"/>
    /// habilitado SOMENTE em findings "Tigre: Código ausente". O VM não
    /// conhece o ExternalEvent, mantendo Presentation isolado de Revit.
    /// </summary>
    public sealed class AuditFindingViewModel
    {
        private readonly QuantityAuditFinding _finding;
        private readonly Action<IReadOnlyCollection<long>>? _selectInRevit;
        private readonly Action<IReadOnlyCollection<long>>? _corrigirAgora;

        public AuditFindingViewModel(QuantityAuditFinding finding)
            : this(finding, selectInRevit: null, corrigirAgora: null)
        {
        }

        public AuditFindingViewModel(
            QuantityAuditFinding finding,
            Action<IReadOnlyCollection<long>>? selectInRevit,
            Action<IReadOnlyCollection<long>>? corrigirAgora)
        {
            _finding = finding;
            _selectInRevit = selectInRevit;
            _corrigirAgora = corrigirAgora;
            SelectInRevitCommand = new RelayCommand(ExecuteSelectInRevit, () => CanSelectInRevit);
            CorrigirAgoraCommand = new RelayCommand(ExecuteCorrigirAgora, () => CanCorrigirAgora);
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

        /// <summary>
        /// Lista de IDs de elementos relacionados ao finding. Vazia para
        /// findings ProjectInfo (Cliente/Autor/etc), populada para findings
        /// agregados de gap (Tigre: Código ausente, Fabricante ausente, etc).
        /// Slice 4.3.A F1 ampliado.
        /// </summary>
        public IReadOnlyList<long> ElementIds => _finding.ElementIds;

        public string ElementIdText
        {
            get
            {
                if (_finding.ElementIds == null || _finding.ElementIds.Count == 0)
                    return string.Empty;
                if (_finding.ElementIds.Count == 1)
                    return _finding.ElementIds[0].ToString(CultureInfo.InvariantCulture);
                return $"{_finding.ElementIds.Count} elemento(s)";
            }
        }

        /// <summary>
        /// Habilita o command de seleção quando existe ao menos um ElementId
        /// E o callback de seleção foi injetado (ou seja, estamos rodando
        /// dentro de uma janela com Revit, não em tests puros).
        /// </summary>
        public bool CanSelectInRevit =>
            _selectInRevit != null && _finding.ElementIds != null && _finding.ElementIds.Count > 0;

        public ICommand SelectInRevitCommand { get; }

        private void ExecuteSelectInRevit()
        {
            if (!CanSelectInRevit) return;
            _selectInRevit!(_finding.ElementIds);
        }

        // ---- Slice 4.3.A F1 ampliado ----

        /// <summary>
        /// True apenas em findings "Tigre: Código ausente" (Red). É o
        /// gate do botão "Corrigir agora" inline.
        /// </summary>
        public bool IsTigreCodigoMissing => _finding.IsTigreCodigoMissing;

        /// <summary>
        /// Habilita o "Corrigir agora" — só quando o finding é
        /// especificamente Tigre: Código ausente E temos IDs E o
        /// callback foi injetado.
        /// </summary>
        public bool CanCorrigirAgora =>
            _corrigirAgora != null
            && _finding.IsTigreCodigoMissing
            && _finding.ElementIds != null
            && _finding.ElementIds.Count > 0;

        public ICommand CorrigirAgoraCommand { get; }

        private void ExecuteCorrigirAgora()
        {
            if (!CanCorrigirAgora) return;
            _corrigirAgora!(_finding.ElementIds);
        }
    }
}
