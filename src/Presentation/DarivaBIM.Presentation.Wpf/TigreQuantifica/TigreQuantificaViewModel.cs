using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
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
    ///
    /// Slice 4.3.B F3 — ganha <see cref="SearchText"/> + projeção
    /// <see cref="FilteredCategories"/> de <see cref="Categories"/> com
    /// busca substring case-insensitive sobre os campos textuais de cada
    /// grupo. Categorias sem matches ficam com IsVisible=false (o XAML
    /// colapsa o card via DataTrigger).
    ///
    /// Slice 4.3.B F4 — setters em <see cref="ProjectClient"/> e
    /// <see cref="ProjectAuthor"/> + <see cref="SaveProjectInfoCommand"/>
    /// pra editar Cliente/Autor inline no header. O VM não conhece
    /// RevitAPI — quem dispara o ExternalEvent é o code-behind via
    /// callback <see cref="SaveProjectInfoCallback"/>.
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

        /// <summary>
        /// Callback injetado pelo code-behind pra disparar o
        /// UpdateProjectInfoExternalEvent que escreve Cliente/Autor de
        /// volta em <c>Document.ProjectInformation</c>. Slice 4.3.B F4.
        /// </summary>
        public Action<ProjectInfoDto>? SaveProjectInfoCallback { get; set; }

        private ProjectInfoDto _projectInfo = new ProjectInfoDto();
        private string? _errorMessage;
        private bool _isBusy;
        private string _statusMessage = string.Empty;
        private int _totalGroups;
        private int _totalElementCount;
        private int _redFindingsCount;
        private int _yellowFindingsCount;
        private string _searchText = string.Empty;
        private bool _isProjectInfoDirty;
        private string _projectClient = string.Empty;
        private string _projectAuthor = string.Empty;

        public TigreQuantificaViewModel()
        {
            SaveProjectInfoCommand = new RelayCommand(ExecuteSaveProjectInfo, () => IsProjectInfoDirty);
        }

        public ProjectInfoDto ProjectInfo
        {
            get => _projectInfo;
            private set
            {
                if (SetField(ref _projectInfo, value))
                {
                    // Slice 4.3.B F4 + Codex HIGH#2 fix — sincroniza
                    // _projectClient/_projectAuthor com o snapshot fresco
                    // APENAS quando não há edição em curso. Se
                    // IsProjectInfoDirty=true, user está digitando e
                    // sobrescrever os campos locais apagaria a entrada dele
                    // (race entre re-scan automático e foco do TextBox).
                    // Após save bem-sucedido o snapshot já vem com os
                    // valores normalizados; RefreshDirty detecta igualdade
                    // e zera o flag naturalmente.
                    if (!IsProjectInfoDirty)
                    {
                        _projectClient = value.Client ?? string.Empty;
                        _projectAuthor = value.Author ?? string.Empty;
                    }
                    RefreshDirty();
                    OnPropertyChanged(nameof(ProjectName));
                    OnPropertyChanged(nameof(ProjectClient));
                    OnPropertyChanged(nameof(ProjectAuthor));
                    OnPropertyChanged(nameof(ProjectIssueDate));
                    OnPropertyChanged(nameof(ProjectVersion));
                }
            }
        }

        public string ProjectName => _projectInfo.Name;

        /// <summary>
        /// Cliente do projeto — agora editável inline (Slice 4.3.B F4).
        /// Setter marca <see cref="IsProjectInfoDirty"/> quando muda em
        /// relação ao snapshot. Save é assíncrono (ExternalEvent), por
        /// isso o setter NÃO escreve no Revit direto — só marca dirty.
        /// </summary>
        public string ProjectClient
        {
            get => _projectClient;
            set
            {
                string normalized = value ?? string.Empty;
                if (SetField(ref _projectClient, normalized))
                {
                    RefreshDirty();
                }
            }
        }

        /// <summary>
        /// Autor do projeto — agora editável inline (Slice 4.3.B F4).
        /// </summary>
        public string ProjectAuthor
        {
            get => _projectAuthor;
            set
            {
                string normalized = value ?? string.Empty;
                if (SetField(ref _projectAuthor, normalized))
                {
                    RefreshDirty();
                }
            }
        }

        public string ProjectIssueDate => _projectInfo.IssueDate;
        public string ProjectVersion => _projectInfo.Version;

        /// <summary>
        /// True quando o usuário editou Cliente/Autor e o save ainda não
        /// foi disparado (ou ainda não retornou). XAML mostra indicador
        /// visual sutil enquanto true; CanExecute de
        /// <see cref="SaveProjectInfoCommand"/> espelha esse flag.
        /// </summary>
        public bool IsProjectInfoDirty
        {
            get => _isProjectInfoDirty;
            private set => SetField(ref _isProjectInfoDirty, value);
        }

        /// <summary>
        /// Slice 4.3.B F4 — dispara o callback de save (code-behind
        /// resolve via ExternalEvent). XAML chama via LostFocus / Enter
        /// no TextBox editável.
        /// </summary>
        public ICommand SaveProjectInfoCommand { get; }

        public ObservableCollection<QuantityCategoryViewModel> Categories { get; } = new();

        /// <summary>
        /// Slice 4.3.B F3 — projeção filtrada de <see cref="Categories"/>
        /// que o XAML bindea (em vez da coleção crua). Cada item carrega
        /// os grupos sobreviventes do filtro; categorias zeradas pelo
        /// filtro têm <c>IsVisible=false</c> e ficam Collapsed via
        /// DataTrigger no <c>CategoryCardTemplate</c>.
        /// </summary>
        public ObservableCollection<CategoryDisplayViewModel> FilteredCategories { get; } = new();

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

        /// <summary>
        /// Slice 4.3.B F3 — texto de busca substring. Empty mostra
        /// todas as categorias e todos os grupos. Setter sempre re-roda
        /// o filtro (não otimiza por igualdade de string trim,
        /// ObservableObject.SetField já cuida disso).
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                string normalized = value ?? string.Empty;
                if (SetField(ref _searchText, normalized))
                {
                    RebuildFilteredCategories();
                }
            }
        }

        public bool HasSearchText => !string.IsNullOrEmpty(_searchText);

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
                FilteredCategories.Clear();
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

            RebuildFilteredCategories();
            RecomputeKpis(snapshot);
            RefreshPipesNeedCoding();
        }

        public void ApplyError(string message)
        {
            ErrorMessage = message;
            Categories.Clear();
            FilteredCategories.Clear();
            Findings.Clear();
            ResetKpis();
            RefreshPipesNeedCoding();
        }

        private void ApplyEmpty(string status)
        {
            ErrorMessage = null;
            ProjectInfo = new ProjectInfoDto();
            Categories.Clear();
            FilteredCategories.Clear();
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

        // ---------------- Slice 4.3.B F3 — filter ----------------

        /// <summary>
        /// Reconstrói <see cref="FilteredCategories"/> a partir do
        /// estado atual de <see cref="Categories"/> aplicando substring
        /// case-insensitive de <see cref="SearchText"/> sobre os campos
        /// textuais de cada grupo (Família, Tipo, Diâmetro, Código
        /// Tigre, Descrição, Fabricante, Sistema).
        /// </summary>
        private void RebuildFilteredCategories()
        {
            FilteredCategories.Clear();
            string needle = (_searchText ?? string.Empty).Trim();
            bool hasFilter = needle.Length > 0;

            foreach (QuantityCategoryViewModel cat in Categories)
            {
                IEnumerable<QuantityGroupViewModel> matched =
                    hasFilter
                        ? cat.Groups.Where(g => GroupMatches(g, needle))
                        : cat.Groups;

                CategoryDisplayViewModel display = new(cat, matched);
                display.IsVisible = display.Groups.Count > 0;
                FilteredCategories.Add(display);
            }

            OnPropertyChanged(nameof(HasSearchText));
        }

        private static bool GroupMatches(QuantityGroupViewModel group, string needle)
        {
            // Codex HIGH#1 fix — Sistema NÃO entra no match. O Slice 4.5
            // tirou Sistema da GroupKey do scanner; o DTO agregado sempre
            // tem System=null/empty. Manter Sistema no filtro criava silent
            // no-op (busca por "Água Fria" nunca achava nada, sem feedback
            // ao usuário). Quando Sistema voltar de outra forma (ex: lista
            // dos sistemas distintos por grupo), reincluir no match.
            return Contains(group.Family, needle)
                || Contains(group.Type, needle)
                || Contains(group.Diameter, needle)
                || Contains(group.TigreCode, needle)
                || Contains(group.Description, needle)
                || Contains(group.TigreDescription, needle)
                || Contains(group.Manufacturer, needle);
        }

        private static bool Contains(string? haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            // Codex MED#7 fix: busca insensível a acentos. Usuario digita
            // "soldavel"/"agua"/"descricao" sem acento e espera achar
            // "Soldável"/"Água Fria"/"Descrição". TigreTextUtils.NormalizeForSearch
            // lowercase + remove combining marks (FormD), preservando
            // significantes — chamada duplicada em ambos os lados pra
            // garantir simetria sem cross-feature impacto.
            string h = DarivaBIM.Domain.Tigre.TigreTextUtils.NormalizeForSearch(haystack);
            string n = DarivaBIM.Domain.Tigre.TigreTextUtils.NormalizeForSearch(needle);
            if (n.Length == 0) return false;
            return h.IndexOf(n, StringComparison.Ordinal) >= 0;
        }

        // ---------------- Slice 4.3.B F4 — edit project info ----------------

        private void RefreshDirty()
        {
            string baselineClient = _projectInfo.Client ?? string.Empty;
            string baselineAuthor = _projectInfo.Author ?? string.Empty;
            bool dirty =
                !string.Equals(_projectClient, baselineClient, StringComparison.Ordinal)
                || !string.Equals(_projectAuthor, baselineAuthor, StringComparison.Ordinal);
            IsProjectInfoDirty = dirty;
        }

        private void ExecuteSaveProjectInfo()
        {
            if (!IsProjectInfoDirty) return;
            Action<ProjectInfoDto>? cb = SaveProjectInfoCallback;
            if (cb == null) return;

            // Snapshot do estado atual — preserva os campos não-editáveis
            // (Name/IssueDate/Version) pra escrita parcial não apagar
            // valores que a UI não controla.
            ProjectInfoDto dto = new ProjectInfoDto
            {
                Name = _projectInfo.Name,
                Client = _projectClient,
                Author = _projectAuthor,
                IssueDate = _projectInfo.IssueDate,
                Version = _projectInfo.Version,
            };

            cb(dto);
        }
    }
}
