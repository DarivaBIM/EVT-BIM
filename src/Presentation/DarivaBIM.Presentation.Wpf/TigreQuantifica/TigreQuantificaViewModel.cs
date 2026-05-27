using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DarivaBIM.Application.DTOs.Quantifica;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.TigreQuantifica
{
    /// <summary>
    /// View model raiz da janela "Tigre Quantifica". Espelha o snapshot do
    /// scanner: cabeçalho do projeto, lista de categorias com grupos
    /// internos, lista de findings, KPIs. ExternalEvents do Revit nunca
    /// chegam aqui — quem orquestra é o code-behind da janela.
    ///
    /// Slice 4.3.A F2 — recebe um callback opcional
    /// <see cref="SelectInRevitCallback"/> + F1 ampliado
    /// <see cref="CorrigirAgoraCallback"/> que o code-behind seta após
    /// criar o ExternalEvent. AuditFindingViewModel consome esses
    /// callbacks via construtor — VM não vê RevitAPI.
    /// </summary>
    public sealed class TigreQuantificaViewModel : ObservableObject
    {
        /// <summary>
        /// Callback injetado pelo code-behind pra disparar SetElementIds +
        /// ShowElements via SelectElementsExternalEvent. Setado antes de
        /// chamar ApplyScan — findings construídos após esse set já
        /// herdarão o callback.
        /// </summary>
        public Action<IReadOnlyCollection<long>>? SelectInRevitCallback { get; set; }

        /// <summary>
        /// Callback injetado pelo code-behind pra abrir Codificar Tigre
        /// pré-filtrado nos IDs do finding "Tigre: Código ausente".
        /// Slice 4.3.A F1 ampliado.
        /// </summary>
        public Action<IReadOnlyCollection<long>>? CorrigirAgoraCallback { get; set; }

        private ProjectInfoDto _projectInfo = new ProjectInfoDto();
        private string? _errorMessage;
        private bool _isBusy;
        private string _statusMessage = string.Empty;
        private int _totalGroups;
        private int _totalElementCount;
        private int _redFindingsCount;
        private int _yellowFindingsCount;

        public ProjectInfoDto ProjectInfo
        {
            get => _projectInfo;
            private set
            {
                if (SetField(ref _projectInfo, value))
                {
                    OnPropertyChanged(nameof(ProjectName));
                    OnPropertyChanged(nameof(ProjectClient));
                    OnPropertyChanged(nameof(ProjectAuthor));
                    OnPropertyChanged(nameof(ProjectIssueDate));
                    OnPropertyChanged(nameof(ProjectVersion));
                }
            }
        }

        public string ProjectName => _projectInfo.Name;
        public string ProjectClient => _projectInfo.Client;
        public string ProjectAuthor => _projectInfo.Author;
        public string ProjectIssueDate => _projectInfo.IssueDate;
        public string ProjectVersion => _projectInfo.Version;

        public ObservableCollection<QuantityCategoryViewModel> Categories { get; } = new();

        public ObservableCollection<AuditFindingViewModel> Findings { get; } = new();

        public string? ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (SetField(ref _errorMessage, value))
                    OnPropertyChanged(nameof(HasErrorMessage));
            }
        }

        public bool HasErrorMessage => !string.IsNullOrWhiteSpace(_errorMessage);

        public bool IsBusy
        {
            get => _isBusy;
            set => SetField(ref _isBusy, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        // ---------------- KPIs ----------------

        public int TotalGroups
        {
            get => _totalGroups;
            private set => SetField(ref _totalGroups, value);
        }

        public int TotalElementCount
        {
            get => _totalElementCount;
            private set => SetField(ref _totalElementCount, value);
        }

        public int RedFindingsCount
        {
            get => _redFindingsCount;
            private set => SetField(ref _redFindingsCount, value);
        }

        public int YellowFindingsCount
        {
            get => _yellowFindingsCount;
            private set => SetField(ref _yellowFindingsCount, value);
        }

        public int TotalFindingsCount => _redFindingsCount + _yellowFindingsCount;

        /// <summary>
        /// <c>true</c> quando existe pelo menos uma categoria de tubulações
        /// no scan E nenhum dos grupos de tubulação tem código Tigre
        /// preenchido. Slice 1.6 F1 — banner amarelo "Codificar Tubos antes".
        /// </summary>
        public bool PipesNeedCoding { get; private set; }

        // ---------------- Operações ----------------

        public void ApplyScan(QuantitySnapshot snapshot)
        {
            if (snapshot == null)
            {
                ApplyEmpty("Sem dados.");
                return;
            }

            ErrorMessage = snapshot.ErrorMessage;

            if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
            {
                Categories.Clear();
                Findings.Clear();
                ProjectInfo = snapshot.ProjectInfo ?? new ProjectInfoDto();
                ResetKpis();
                RefreshPipesNeedCoding();
                return;
            }

            ProjectInfo = snapshot.ProjectInfo ?? new ProjectInfoDto();

            Categories.Clear();
            foreach (QuantityCategoryViewModel cat in BuildCategoryViewModels(snapshot.Groups))
                Categories.Add(cat);

            Findings.Clear();
            foreach (QuantityAuditFinding f in snapshot.AuditFindings ?? new QuantityAuditFinding[0])
                Findings.Add(new AuditFindingViewModel(f, SelectInRevitCallback, CorrigirAgoraCallback));

            RecomputeKpis(snapshot);
            RefreshPipesNeedCoding();
        }

        public void ApplyError(string message)
        {
            ErrorMessage = message;
            Categories.Clear();
            Findings.Clear();
            ResetKpis();
            RefreshPipesNeedCoding();
        }

        private void ApplyEmpty(string status)
        {
            ErrorMessage = null;
            ProjectInfo = new ProjectInfoDto();
            Categories.Clear();
            Findings.Clear();
            ResetKpis();
            StatusMessage = status;
            RefreshPipesNeedCoding();
        }

        private static IEnumerable<QuantityCategoryViewModel> BuildCategoryViewModels(
            IReadOnlyList<QuantityGroup>? groups)
        {
            if (groups == null || groups.Count == 0)
                yield break;

            var byCategory = groups
                .GroupBy(g => g.Category, System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, System.StringComparer.OrdinalIgnoreCase);

            foreach (var group in byCategory)
            {
                // MeasurementKind primário da categoria = o do primeiro grupo
                // (todos compartilham a mesma BIC, portanto a mesma kind).
                MeasurementKind kind = group.First().MeasurementKind;
                IEnumerable<QuantityGroupViewModel> groupVms = group.Select(g => new QuantityGroupViewModel(g));
                yield return new QuantityCategoryViewModel(group.Key, kind, groupVms);
            }
        }

        private void RecomputeKpis(QuantitySnapshot snapshot)
        {
            int totalGroups = snapshot.Groups?.Count ?? 0;
            int totalElements = 0;
            if (snapshot.Groups != null)
            {
                foreach (QuantityGroup g in snapshot.Groups)
                    totalElements += g.ElementCount;
            }

            int red = 0, yellow = 0;
            if (snapshot.AuditFindings != null)
            {
                foreach (QuantityAuditFinding f in snapshot.AuditFindings)
                {
                    if (f.Severity == AuditSeverity.Red) red++;
                    else yellow++;
                }
            }

            TotalGroups = totalGroups;
            TotalElementCount = totalElements;
            RedFindingsCount = red;
            YellowFindingsCount = yellow;
            OnPropertyChanged(nameof(TotalFindingsCount));
        }

        private void ResetKpis()
        {
            TotalGroups = 0;
            TotalElementCount = 0;
            RedFindingsCount = 0;
            YellowFindingsCount = 0;
            OnPropertyChanged(nameof(TotalFindingsCount));
        }

        private void RefreshPipesNeedCoding()
        {
            // Conjunto = (ao menos uma categoria de PipeCurves) AND
            //          (todos os grupos de PipeCurves estão sem código).
            bool hasPipeCategory = false;
            bool anyPipeWithCode = false;
            foreach (QuantityCategoryViewModel cat in Categories)
            {
                if (!cat.HasPipeCurvesCategory) continue;
                hasPipeCategory = true;
                foreach (QuantityGroupViewModel g in cat.Groups)
                {
                    if (g.IsPipeCurvesCategory && !string.IsNullOrWhiteSpace(g.TigreCode))
                    {
                        anyPipeWithCode = true;
                        break;
                    }
                }
                if (anyPipeWithCode) break;
            }

            bool newValue = hasPipeCategory && !anyPipeWithCode;
            if (PipesNeedCoding != newValue)
            {
                PipesNeedCoding = newValue;
                OnPropertyChanged(nameof(PipesNeedCoding));
            }
        }
    }
}
