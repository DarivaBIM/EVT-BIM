using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DarivaBIM.Application.Contracts.UtilizationPoints;
using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Application.UseCases.UtilizationPoints;
using DarivaBIM.Domain.Hydraulics.UtilizationPoints;
using DarivaBIM.Infrastructure.Persistence.UtilizationPoints;
using DarivaBIM.Plugin.Features.UtilizationPoints;
using DarivaBIM.Presentation.Wpf.UtilizationPoints;

namespace DarivaBIM.Plugin.Ui
{
    /// <summary>
    /// Janela modeless "Inserir Pontos de Utilização". O code-behind cuida
    /// somente da coreografia entre o <see cref="UtilizationPointInsertionViewModel"/>
    /// e os ExternalEvents que interagem com a Revit API; toda a lógica de
    /// inserção vive em <c>RevitUtilizationPointInsertionService</c>.
    ///
    /// Estado do loop de inserção: ao clicar em "Ativar inserção", o handler
    /// entra num loop <c>pick → insert → repeat</c>; o usuário finaliza cada
    /// lote com ENTER/Concluir e encerra com ESC. Fechar a janela limpa
    /// <see cref="IsLoopActive"/> para o loop sair na próxima volta.
    ///
    /// Reordenação: drag-and-drop pelo handle de 6 pontinhos. O MouseDown
    /// arma <see cref="_draggedRule"/>; o MouseMove na janela (não no handle)
    /// é que de fato dispara o <c>DragDrop.DoDragDrop</c>, para que o gesto
    /// continue funcionando mesmo se o cursor sair do handle.
    /// </summary>
    public partial class UtilizationPointInsertionWindow : Window
    {
        private const string RuleDragFormat = "EvtBim.UtilizationPointRule";

        // Debounce para não escrever JSON em disco a cada keystroke / clique
        // de seta. 400 ms balanceia "salva rápido" e "não satura I/O".
        private const int SaveDebounceMilliseconds = 400;

        private static UtilizationPointInsertionWindow? _instance;

        private readonly IUtilizationPointSettingsStore _settingsStore = new UtilizationPointSettingsStore();
        private readonly UtilizationPointLoadExternalEvent _loadEvent = new();
        private readonly UtilizationPointInsertEvent _insertEvent = new();
        private readonly DispatcherTimer _saveTimer;
        private bool _suppressActiveGroupChange;
        private bool _initialLoadDone;
        private bool _isLoopActive;
        private bool _isClosing;

        private Point _dragStartPoint;
        private UtilizationPointRuleViewModel? _draggedRule;

        public UtilizationPointInsertionViewModel ViewModel { get; }

        public UtilizationPointInsertionWindow()
        {
            InitializeComponent();
            ViewModel = new UtilizationPointInsertionViewModel();
            DataContext = ViewModel;

            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SaveDebounceMilliseconds),
            };
            _saveTimer.Tick += OnSaveTimerTick;
        }

        public static UtilizationPointInsertionWindow ShowSingleton()
        {
            if (_instance == null)
            {
                _instance = new UtilizationPointInsertionWindow();
                _instance.Closed += (_, _) => _instance = null;
            }

            if (!_instance.IsVisible)
                _instance.Show();

            _instance.Activate();
            return _instance;
        }

        public bool IsLoopActive => _isLoopActive && !_isClosing;

        public void ApplyCatalog(
            IReadOnlyList<FamilyTypeOptionDto> familyTypes,
            IReadOnlyList<LevelOptionDto> levels,
            string statusMessage)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ViewModel.FamilyTypes.Clear();
                for (int i = 0; i < familyTypes.Count; i++)
                {
                    ViewModel.FamilyTypes.Add(new FamilyTypeOptionViewModel(familyTypes[i]));
                }

                ViewModel.Levels.Clear();
                ViewModel.Levels.Add(new LevelOptionViewModel(null, "Usar nível do elemento"));
                ViewModel.Levels.Add(new LevelOptionViewModel(
                    new LevelOptionDto(0, "Zero absoluto do projeto", 0),
                    "Zero absoluto do projeto"));
                for (int i = 0; i < levels.Count; i++)
                {
                    LevelOptionDto level = levels[i];
                    ViewModel.Levels.Add(new LevelOptionViewModel(level, level.Name));
                }

                if (ViewModel.SelectedLevel == null)
                    ViewModel.SelectedLevel = ViewModel.Levels[0];

                ResolveAllRuleReferences();
                RefreshAllGroupSummaries();
                ViewModel.StatusMessage = statusMessage;
                ViewModel.IsBusy = false;
            }));
        }

        // Chamado depois de CADA lote inserido durante o loop contínuo.
        // O banner amarelo permanece visível porque IsAwaitingSelection
        // continua true — o usuário ainda pode emendar outro lote.
        public void OnInsertionBatchCompleted(InsertionSummaryDto summary)
        {
            if (_isClosing) return;
            TryDispatch(() =>
            {
                ViewModel.LastSummary = summary;
                ViewModel.RefreshMessages();
                ViewModel.StatusMessage = BuildExecutionStatus(summary);
            });
        }

        // Chamado quando o loop termina (ESC, erro ou janela fechada). Pode
        // ser chamado DEPOIS de a janela ter sido fechada (o handler do Revit
        // ainda finaliza após a janela sumir); por isso o dispatch é
        // tolerante e os property setters são skipados quando _isClosing.
        public void NotifyInsertionEnded(string message)
        {
            TryDispatch(() =>
            {
                _isLoopActive = false;
                if (_isClosing) return;
                ViewModel.IsBusy = false;
                ViewModel.IsAwaitingSelection = false;
                ViewModel.StatusMessage = message;
            });
        }

        private void TryDispatch(Action action)
        {
            try
            {
                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
                Dispatcher.BeginInvoke(action);
            }
            catch
            {
                // Janela já foi descartada antes do callback do Revit chegar.
            }
        }

        // ---------------- Event handlers ----------------

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            LoadGroupsFromSettings();
            _initialLoadDone = true;
            ViewModel.IsBusy = true;
            ViewModel.StatusMessage = "Lendo tipos de família do projeto…";
            _loadEvent.Raise(this);
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            // Marca o flag de fechamento ANTES de limpar IsLoopActive. O
            // handler de inserção lê IsLoopActive como (_isLoopActive &&
            // !_isClosing), então a próxima iteração do loop sai por si só
            // — mesmo que o PickObjects ainda esteja bloqueado esperando o
            // ESC do usuário. Esse contrato evita uma janela de corrida em
            // que o loop continuaria após o fechamento.
            _isClosing = true;
            _isLoopActive = false;
            ViewModel.IsAwaitingSelection = false;
            ViewModel.IsBusy = false;

            if (!_initialLoadDone) return;

            // Garante salvamento síncrono final, ignorando o debounce.
            _saveTimer.Stop();
            SaveCurrentSettingsNow();
        }

        private void OnGroupItemClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is UtilizationPointGroupViewModel group)
            {
                SetActiveGroup(group);
            }
        }

        private void OnNewGroupClicked(object sender, RoutedEventArgs e)
        {
            string? name = PromptInline(
                "Nome do novo grupo",
                "Ex.: Banheiro, Cozinha, Área de serviço.",
                "Novo grupo");
            if (string.IsNullOrWhiteSpace(name)) return;

            UtilizationPointGroupViewModel group = new(Guid.NewGuid().ToString("N"), name!.Trim());
            ViewModel.Groups.Add(group);
            HookGroupEvents(group);
            SetActiveGroup(group);
            ScheduleSave();
        }

        private void OnRenameGroupClicked(object sender, RoutedEventArgs e)
        {
            UtilizationPointGroupViewModel? group = FindGroupFromTag(sender);
            if (group == null) return;

            string? name = PromptInline("Renomear grupo", "Digite o novo nome.", group.Name);
            if (string.IsNullOrWhiteSpace(name)) return;

            group.Name = name!.Trim();
            ViewModel.OnActiveGroupChanged();
            ScheduleSave();
        }

        private void OnDuplicateGroupClicked(object sender, RoutedEventArgs e)
        {
            UtilizationPointGroupViewModel? group = FindGroupFromTag(sender);
            if (group == null) return;

            UtilizationPointGroupDto dto = group.ToDto();
            dto.Id = Guid.NewGuid().ToString("N");
            dto.Name = group.Name + " (cópia)";

            UtilizationPointGroupViewModel clone = UtilizationPointGroupViewModel.FromDto(dto);
            ViewModel.Groups.Add(clone);
            HookGroupEvents(clone);
            ResolveRuleReferences(clone);
            clone.RefreshSummaries();
            SetActiveGroup(clone);
            ScheduleSave();
        }

        private void OnDeleteGroupClicked(object sender, RoutedEventArgs e)
        {
            UtilizationPointGroupViewModel? group = FindGroupFromTag(sender);
            if (group == null) return;

            MessageBoxResult result = MessageBox.Show(
                this,
                $"Excluir o grupo \"{group.Name}\"?",
                "Inserir Pontos de Utilização",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.OK) return;

            ViewModel.Groups.Remove(group);
            if (ReferenceEquals(ViewModel.ActiveGroup, group))
                SetActiveGroup(ViewModel.Groups.FirstOrDefault());

            ScheduleSave();
        }

        private void OnAddRuleClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.ActiveGroup == null)
            {
                ViewModel.StatusMessage = "Crie um grupo antes de adicionar regras.";
                return;
            }

            UtilizationPointRuleViewModel rule = new()
            {
                MinMeters = 0.0,
                MaxMeters = 0.5,
            };
            ViewModel.ActiveGroup.Rules.Add(rule);
            RefreshRuleStatus(rule);
            ViewModel.ActiveGroup.RefreshSummaries();
            ViewModel.OnActiveGroupChanged();
            HookRuleEvents(rule);
            ScheduleSave();
        }

        private void OnDuplicateRuleClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.Tag is not UtilizationPointRuleViewModel rule) return;
            if (ViewModel.ActiveGroup == null) return;

            UtilizationPointRuleViewModel clone = UtilizationPointRuleViewModel.FromDto(rule.ToDto());
            clone.SelectedFamilyType = rule.SelectedFamilyType;
            int index = ViewModel.ActiveGroup.Rules.IndexOf(rule);
            if (index >= 0)
                ViewModel.ActiveGroup.Rules.Insert(index + 1, clone);
            else
                ViewModel.ActiveGroup.Rules.Add(clone);

            RefreshRuleStatus(clone);
            HookRuleEvents(clone);
            ViewModel.ActiveGroup.RefreshSummaries();
            ViewModel.OnActiveGroupChanged();
            ScheduleSave();
        }

        private void OnDeleteRuleClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.Tag is not UtilizationPointRuleViewModel rule) return;
            if (ViewModel.ActiveGroup == null) return;

            UnhookRuleEvents(rule);
            ViewModel.ActiveGroup.Rules.Remove(rule);
            ViewModel.ActiveGroup.RefreshSummaries();
            ViewModel.OnActiveGroupChanged();
            ScheduleSave();
        }

        // ---------- Drag-and-drop reorder ----------

        private void OnRuleDragHandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not UtilizationPointRuleViewModel rule) return;

            _dragStartPoint = e.GetPosition(this);
            _draggedRule = rule;
        }

        private void OnWindowPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (_draggedRule == null) return;

            Point pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _dragStartPoint.X) <= SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(pos.Y - _dragStartPoint.Y) <= SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            UtilizationPointRuleViewModel rule = _draggedRule;
            _draggedRule = null;

            try
            {
                DataObject data = new(RuleDragFormat, rule);
                DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
            }
            catch
            {
                // DoDragDrop pode lançar se a window perde foco no meio do
                // gesto; ignorar mantém a UI estável.
            }
        }

        private void OnWindowPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _draggedRule = null;
        }

        private void OnRuleRowDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(RuleDragFormat)
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnRuleRowDrop(object sender, DragEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not UtilizationPointRuleViewModel target) return;
            if (e.Data.GetData(RuleDragFormat) is not UtilizationPointRuleViewModel source) return;
            if (ViewModel.ActiveGroup == null) return;
            if (ReferenceEquals(source, target)) return;

            int oldIndex = ViewModel.ActiveGroup.Rules.IndexOf(source);
            int newIndex = ViewModel.ActiveGroup.Rules.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0) return;

            ViewModel.ActiveGroup.Rules.Move(oldIndex, newIndex);
            ViewModel.ActiveGroup.RefreshSummaries();
            ViewModel.OnActiveGroupChanged();
            ScheduleSave();
            e.Handled = true;
        }

        // ---------- Preset I/O ----------

        private void OnImportPresetClicked(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new()
            {
                Filter = "JSON (*.json)|*.json",
                Title = "Importar preset de pontos de utilização",
            };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                string json = System.IO.File.ReadAllText(dlg.FileName);
                UtilizationPointProfilesDto? profiles =
                    System.Text.Json.JsonSerializer.Deserialize<UtilizationPointProfilesDto>(json);
                if (profiles == null) return;

                for (int i = 0; i < profiles.Groups.Count; i++)
                {
                    UtilizationPointGroupDto dto = profiles.Groups[i];
                    dto.Id = Guid.NewGuid().ToString("N");
                    UtilizationPointGroupViewModel g = UtilizationPointGroupViewModel.FromDto(dto);
                    ViewModel.Groups.Add(g);
                    HookGroupEvents(g);
                    ResolveRuleReferences(g);
                    g.RefreshSummaries();
                }

                if (ViewModel.ActiveGroup == null)
                    SetActiveGroup(ViewModel.Groups.FirstOrDefault());

                ScheduleSave();
                ViewModel.StatusMessage = $"Preset importado de {dlg.FileName}.";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Falha ao importar preset: {ex.Message}";
            }
        }

        private void OnExportPresetClicked(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new()
            {
                Filter = "JSON (*.json)|*.json",
                Title = "Exportar preset de pontos de utilização",
                FileName = "utilization-points.json",
            };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                UtilizationPointProfilesDto profiles = new();
                for (int i = 0; i < ViewModel.Groups.Count; i++)
                {
                    profiles.Groups.Add(ViewModel.Groups[i].ToDto());
                }

                string json = System.Text.Json.JsonSerializer.Serialize(profiles, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                });
                System.IO.File.WriteAllText(dlg.FileName, json);
                ViewModel.StatusMessage = $"Preset exportado em {dlg.FileName}.";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Falha ao exportar preset: {ex.Message}";
            }
        }

        private void OnPickAndInsertClicked(object sender, RoutedEventArgs e)
        {
            if (!EnsureActiveGroupReadyForInsertion(out UtilizationPointGroup? domainGroup)) return;

            _isLoopActive = true;
            ViewModel.IsBusy = true;
            ViewModel.IsAwaitingSelection = true;
            ViewModel.StatusMessage = "Aguardando seleção no Revit…";
            _insertEvent.Raise(this, domainGroup!, ViewModel.SelectedLevel?.ElementId);
        }

        // ---------------- Helpers ----------------

        private void LoadGroupsFromSettings()
        {
            UtilizationPointProfilesDto profiles = _settingsStore.Load();

            ViewModel.Groups.Clear();
            for (int i = 0; i < profiles.Groups.Count; i++)
            {
                UtilizationPointGroupViewModel g = UtilizationPointGroupViewModel.FromDto(profiles.Groups[i]);
                ViewModel.Groups.Add(g);
                HookGroupEvents(g);
            }

            if (ViewModel.Groups.Count == 0)
            {
                UtilizationPointGroupViewModel defaultGroup = BuildDefaultBathroomGroup();
                ViewModel.Groups.Add(defaultGroup);
                HookGroupEvents(defaultGroup);
            }

            SetActiveGroup(ViewModel.Groups[0]);
        }

        // ScheduleSave reinicia o timer; SaveCurrentSettingsNow grava de fato.
        private void ScheduleSave()
        {
            if (_isClosing) return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void OnSaveTimerTick(object? sender, EventArgs e)
        {
            _saveTimer.Stop();
            SaveCurrentSettingsNow();
        }

        private void SaveCurrentSettingsNow()
        {
            UtilizationPointProfilesDto profiles = new();
            for (int i = 0; i < ViewModel.Groups.Count; i++)
            {
                profiles.Groups.Add(ViewModel.Groups[i].ToDto());
            }
            try
            {
                _settingsStore.Save(profiles);
            }
            catch
            {
                // Persistência best-effort — não quebrar UX por falha de disco.
            }
        }

        private void SetActiveGroup(UtilizationPointGroupViewModel? group)
        {
            _suppressActiveGroupChange = true;
            try
            {
                ViewModel.ActiveGroup = group;
            }
            finally
            {
                _suppressActiveGroupChange = false;
            }

            ViewModel.OnActiveGroupChanged();
        }

        private void HookGroupEvents(UtilizationPointGroupViewModel group)
        {
            group.PropertyChanged += OnGroupChanged;
            group.Rules.CollectionChanged += (_, _) =>
                OnGroupChanged(group, new PropertyChangedEventArgs(nameof(group.Rules)));
            for (int i = 0; i < group.Rules.Count; i++)
            {
                HookRuleEvents(group.Rules[i]);
            }
        }

        private void HookRuleEvents(UtilizationPointRuleViewModel rule)
        {
            rule.PropertyChanged += OnRuleChanged;
        }

        private void UnhookRuleEvents(UtilizationPointRuleViewModel rule)
        {
            rule.PropertyChanged -= OnRuleChanged;
        }

        private void OnGroupChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressActiveGroupChange) return;
            if (sender is UtilizationPointGroupViewModel)
            {
                ViewModel.OnActiveGroupChanged();
                ScheduleSave();
            }
        }

        private void OnRuleChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressActiveGroupChange) return;
            if (sender is not UtilizationPointRuleViewModel rule) return;

            // Status é propriedade derivada — quando ele muda, é porque já
            // recalculamos. Evitamos um round trip que dispararia outro
            // RefreshSummaries desnecessário e outra escrita em disco.
            if (e.PropertyName == nameof(UtilizationPointRuleViewModel.Status)
                || e.PropertyName == nameof(UtilizationPointRuleViewModel.StatusLabel)
                || e.PropertyName == nameof(UtilizationPointRuleViewModel.IsOk)
                || e.PropertyName == nameof(UtilizationPointRuleViewModel.IsWarning))
            {
                if (ViewModel.ActiveGroup != null)
                {
                    ViewModel.ActiveGroup.RefreshSummaries();
                    ViewModel.OnActiveGroupChanged();
                }
                return;
            }

            RefreshRuleStatus(rule);
            if (ViewModel.ActiveGroup != null)
            {
                ViewModel.ActiveGroup.RefreshSummaries();
                ViewModel.OnActiveGroupChanged();
            }
            ScheduleSave();
        }

        private void ResolveAllRuleReferences()
        {
            for (int i = 0; i < ViewModel.Groups.Count; i++)
            {
                ResolveRuleReferences(ViewModel.Groups[i]);
            }
        }

        private void ResolveRuleReferences(UtilizationPointGroupViewModel group)
        {
            _suppressActiveGroupChange = true;
            try
            {
                for (int i = 0; i < group.Rules.Count; i++)
                {
                    UtilizationPointRuleViewModel rule = group.Rules[i];
                    if (rule.SelectedFamilyType != null)
                    {
                        RefreshRuleStatus(rule);
                        continue;
                    }

                    FamilyTypeOptionViewModel? match = ViewModel.FindFamilyType(rule.SavedFamilyName, rule.SavedTypeName);
                    if (match != null)
                    {
                        rule.SelectedFamilyType = match;
                    }
                    RefreshRuleStatus(rule);
                }
            }
            finally
            {
                _suppressActiveGroupChange = false;
            }
            group.RefreshSummaries();
        }

        private void RefreshAllGroupSummaries()
        {
            for (int i = 0; i < ViewModel.Groups.Count; i++)
            {
                ViewModel.Groups[i].RefreshSummaries();
            }
            ViewModel.OnActiveGroupChanged();
        }

        private static void RefreshRuleStatus(UtilizationPointRuleViewModel rule)
        {
            if (rule.SelectedFamilyType == null)
            {
                rule.Status = !string.IsNullOrWhiteSpace(rule.SavedFamilyName)
                              || !string.IsNullOrWhiteSpace(rule.SavedTypeName)
                    ? UtilizationPointRuleStatus.FamilyTypeNotFoundInDocument
                    : UtilizationPointRuleStatus.FamilyTypeMissing;
                return;
            }

            if (rule.MaxMeters < rule.MinMeters)
            {
                rule.Status = UtilizationPointRuleStatus.HeightRangeInvalid;
                return;
            }

            rule.Status = UtilizationPointRuleStatus.Ok;
        }

        // Validação pré-ativação. Bloqueia se houver QUALQUER linha não-Ok
        // e exibe um diálogo listando exatamente quais linhas estão com
        // problema. Usa o Status já computado pelos view models (cobre
        // "Sem tipo", "Tipo ausente" e "Faixa inválida").
        private bool EnsureActiveGroupReadyForInsertion(out UtilizationPointGroup? group)
        {
            group = null;
            if (ViewModel.ActiveGroup == null)
            {
                ViewModel.StatusMessage = "Selecione um grupo ativo.";
                return false;
            }

            if (ViewModel.ActiveGroup.Rules.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "O grupo ativo não tem nenhuma regra. Adicione ao menos uma regra com tipo e faixa de altura.",
                    "Não é possível ativar a inserção",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ViewModel.StatusMessage = "Grupo ativo vazio.";
                return false;
            }

            List<string> issues = new();
            for (int i = 0; i < ViewModel.ActiveGroup.Rules.Count; i++)
            {
                UtilizationPointRuleViewModel rule = ViewModel.ActiveGroup.Rules[i];
                if (rule.Status != UtilizationPointRuleStatus.Ok)
                {
                    issues.Add($"• Linha {i + 1}: {rule.StatusLabel}");
                }
            }

            if (issues.Count > 0)
            {
                StringBuilder sb = new();
                sb.Append("O grupo \"").Append(ViewModel.ActiveGroup.Name).Append("\" tem ");
                sb.Append(issues.Count).Append(issues.Count == 1 ? " item" : " itens");
                sb.AppendLine(" que precisa(m) de atenção:");
                sb.AppendLine();
                for (int i = 0; i < issues.Count; i++)
                {
                    sb.AppendLine(issues[i]);
                }
                sb.AppendLine();
                sb.Append("Corrija as linhas marcadas em amarelo e tente novamente.");

                MessageBox.Show(
                    this,
                    sb.ToString(),
                    "Não é possível ativar a inserção",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ViewModel.StatusMessage = $"Há {issues.Count} item(ns) problemático(s) no grupo ativo.";
                return false;
            }

            UtilizationPointGroupDto dto = ViewModel.ActiveGroup.ToDto();
            group = UtilizationPointProfilesMapper.ToDomain(dto);
            return true;
        }

        private static string BuildExecutionStatus(InsertionSummaryDto summary)
        {
            return $"Último lote: {summary.PointsInserted} inseridos ({summary.PointsConnected} conectados), " +
                $"{summary.ConnectorsWithoutRange} sem faixa, {summary.Errors} erros.";
        }

        private UtilizationPointGroupViewModel? FindGroupFromTag(object sender)
        {
            if (sender is not FrameworkElement fe) return null;
            if (fe.Tag is string id)
            {
                return ViewModel.Groups.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.Ordinal));
            }
            if (fe.DataContext is UtilizationPointGroupViewModel g2) return g2;
            return null;
        }

        // Grupo padrão para a primeira abertura: faixas comuns de banheiro,
        // sem tipos preenchidos (o usuário escolhe a partir do catálogo do
        // documento). Mantém o "starting kit" útil sem amarrar a UI a nomes
        // específicos de famílias.
        private static UtilizationPointGroupViewModel BuildDefaultBathroomGroup()
        {
            UtilizationPointGroupViewModel group = new(Guid.NewGuid().ToString("N"), "Banheiro");
            group.Rules.Add(new UtilizationPointRuleViewModel { MinMeters = 1.9, MaxMeters = 2.2 });
            group.Rules.Add(new UtilizationPointRuleViewModel { MinMeters = 0.10, MaxMeters = 0.30 });
            group.Rules.Add(new UtilizationPointRuleViewModel { MinMeters = 0.30, MaxMeters = 0.50 });
            group.Rules.Add(new UtilizationPointRuleViewModel { MinMeters = 0.50, MaxMeters = 0.80 });
            return group;
        }

        private string? PromptInline(string title, string hint, string? initial)
        {
            // Mini dialog inline para não introduzir uma janela WPF extra apenas para
            // capturar um nome. Mantém o code-behind autocontido.
            Window prompt = new()
            {
                Title = title,
                Width = 420,
                Height = 200,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Background = Brushes.White,
            };

            Grid grid = new() { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock hintLabel = new()
            {
                Text = hint,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
            };
            Grid.SetRow(hintLabel, 0);
            grid.Children.Add(hintLabel);

            TextBox input = new()
            {
                Text = initial ?? string.Empty,
                FontSize = 13,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 10),
                Height = 32,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            input.SelectAll();
            input.Focus();
            Grid.SetRow(input, 1);
            grid.Children.Add(input);

            StackPanel buttons = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            Grid.SetRow(buttons, 3);
            grid.Children.Add(buttons);

            Button cancel = new()
            {
                Content = "Cancelar",
                Padding = new Thickness(14, 4, 14, 4),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
            };
            cancel.Click += (_, _) => { prompt.DialogResult = false; prompt.Close(); };
            buttons.Children.Add(cancel);

            Button ok = new()
            {
                Content = "Salvar",
                Padding = new Thickness(14, 4, 14, 4),
                IsDefault = true,
                Background = new SolidColorBrush(Color.FromRgb(0x0E, 0xA5, 0xE9)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
            };
            ok.Click += (_, _) => { prompt.DialogResult = true; prompt.Close(); };
            buttons.Children.Add(ok);

            prompt.Content = grid;
            bool? result = prompt.ShowDialog();
            return result == true ? input.Text : null;
        }
    }
}
