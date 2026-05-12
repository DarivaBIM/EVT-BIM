using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    /// </summary>
    public partial class UtilizationPointInsertionWindow : Window
    {
        private static UtilizationPointInsertionWindow? _instance;

        private readonly IUtilizationPointSettingsStore _settingsStore = new UtilizationPointSettingsStore();
        private readonly UtilizationPointLoadExternalEvent _loadEvent = new();
        private readonly UtilizationPointInsertEvent _insertEvent = new();
        private bool _suppressActiveGroupChange;
        private bool _initialLoadDone;

        public UtilizationPointInsertionViewModel ViewModel { get; }

        public UtilizationPointInsertionWindow()
        {
            InitializeComponent();
            ViewModel = new UtilizationPointInsertionViewModel();
            DataContext = ViewModel;
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

        public void ApplyInsertionSummary(InsertionSummaryDto summary)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ViewModel.LastSummary = summary;
                ViewModel.RefreshMessages();
                ViewModel.IsBusy = false;

                if (summary == null)
                {
                    ViewModel.StatusMessage = "Execução concluída.";
                    return;
                }

                ViewModel.StatusMessage = BuildExecutionStatus(summary);
            }));
        }

        public void NotifyCancelled(string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ViewModel.IsBusy = false;
                ViewModel.StatusMessage = message;
            }));
        }

        public bool IsContinuousMode => ViewModel.IsContinuousMode;

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
            if (!_initialLoadDone) return;
            SaveCurrentSettings();
        }

        private void OnGroupItemClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is UtilizationPointGroupViewModel group)
            {
                SetActiveGroup(group);
                UpdateSidebarSelection();
            }
        }

        private void OnNewGroupClicked(object sender, RoutedEventArgs e)
        {
            string defaultName = "Novo grupo";
            string? name = PromptInline(
                "Nome do novo grupo",
                "Ex.: Banheiro, Cozinha, Área de serviço.",
                defaultName);
            if (string.IsNullOrWhiteSpace(name)) return;

            UtilizationPointGroupViewModel group = new(Guid.NewGuid().ToString("N"), name!.Trim());
            ViewModel.Groups.Add(group);
            SetActiveGroup(group);
            UpdateSidebarSelection();
            SaveCurrentSettings();
        }

        private void OnRenameGroupClicked(object sender, RoutedEventArgs e)
        {
            UtilizationPointGroupViewModel? group = FindGroupFromTag(sender);
            if (group == null) return;

            string? name = PromptInline("Renomear grupo", "Digite o novo nome.", group.Name);
            if (string.IsNullOrWhiteSpace(name)) return;

            group.Name = name!.Trim();
            ViewModel.OnActiveGroupChanged();
            SaveCurrentSettings();
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
            ResolveRuleReferences(clone);
            clone.RefreshSummaries();
            SetActiveGroup(clone);
            UpdateSidebarSelection();
            SaveCurrentSettings();
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

            UpdateSidebarSelection();
            SaveCurrentSettings();
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
                Name = "Novo ponto",
                MinMeters = 0.0,
                MaxMeters = 0.5,
            };
            ViewModel.ActiveGroup.Rules.Add(rule);
            RefreshRuleStatus(rule);
            ViewModel.ActiveGroup.RefreshSummaries();
            ViewModel.OnActiveGroupChanged();
            HookRuleEvents(rule);
            SaveCurrentSettings();
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
            SaveCurrentSettings();
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
            SaveCurrentSettings();
        }

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

                UpdateSidebarSelection();
                SaveCurrentSettings();
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

        private void OnToggleContinuousModeClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.IsContinuousMode = !ViewModel.IsContinuousMode;
            ViewModel.StatusMessage = ViewModel.IsContinuousMode
                ? "Modo contínuo ativado: cada execução reabre a seleção."
                : "Modo contínuo desativado.";
        }

        private void OnUseSelectionClicked(object sender, RoutedEventArgs e)
        {
            if (!EnsureActiveGroupReadyForInsertion(out UtilizationPointGroup? domainGroup)) return;

            ViewModel.IsBusy = true;
            ViewModel.StatusMessage = "Executando inserção sobre a seleção atual do Revit…";
            _insertEvent.Raise(
                this,
                domainGroup!,
                ViewModel.SelectedLevel?.ElementId,
                useCurrentSelection: true);
        }

        private void OnPickAndInsertClicked(object sender, RoutedEventArgs e)
        {
            if (!EnsureActiveGroupReadyForInsertion(out UtilizationPointGroup? domainGroup)) return;

            ViewModel.IsBusy = true;
            ViewModel.StatusMessage = "Selecione os elementos no Revit. Pressione ENTER ou ESC para finalizar.";
            _insertEvent.Raise(
                this,
                domainGroup!,
                ViewModel.SelectedLevel?.ElementId,
                useCurrentSelection: false);
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
                ViewModel.Groups.Add(BuildDefaultBathroomGroup());
                HookGroupEvents(ViewModel.Groups[0]);
            }

            SetActiveGroup(ViewModel.Groups[0]);
            UpdateSidebarSelection();
        }

        private void SaveCurrentSettings()
        {
            UtilizationPointProfilesDto profiles = new();
            for (int i = 0; i < ViewModel.Groups.Count; i++)
            {
                profiles.Groups.Add(ViewModel.Groups[i].ToDto());
            }
            _settingsStore.Save(profiles);
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

        private void UpdateSidebarSelection()
        {
            // Visual selection is implicit via DataTemplate styling; we
            // simply ensure the active group is visible.
        }

        private void HookGroupEvents(UtilizationPointGroupViewModel group)
        {
            group.PropertyChanged += OnGroupChanged;
            group.Rules.CollectionChanged += (_, _) => OnGroupChanged(group, new PropertyChangedEventArgs(nameof(group.Rules)));
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
                // Não chamar group.RefreshSummaries() aqui: este handler está
                // inscrito em group.PropertyChanged, e RefreshSummaries dispara
                // OnPropertyChanged nas seis propriedades de sumário do próprio
                // group — voltariam pra cá em recursão infinita (StackOverflow).
                // O group já se auto-atualiza quando Rules muda via o
                // CollectionChanged interno do construtor.
                ViewModel.OnActiveGroupChanged();
                SaveCurrentSettings();
            }
        }

        private void OnRuleChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressActiveGroupChange) return;
            if (sender is not UtilizationPointRuleViewModel rule) return;
            RefreshRuleStatus(rule);
            if (ViewModel.ActiveGroup != null)
            {
                ViewModel.ActiveGroup.RefreshSummaries();
                ViewModel.OnActiveGroupChanged();
            }
            SaveCurrentSettings();
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
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                rule.Status = UtilizationPointRuleStatus.NameMissing;
                return;
            }

            if (rule.SelectedFamilyType == null)
            {
                if (!string.IsNullOrWhiteSpace(rule.SavedFamilyName) || !string.IsNullOrWhiteSpace(rule.SavedTypeName))
                    rule.Status = UtilizationPointRuleStatus.FamilyTypeNotFoundInDocument;
                else
                    rule.Status = UtilizationPointRuleStatus.FamilyTypeMissing;
                return;
            }

            if (rule.MaxMeters < rule.MinMeters)
            {
                rule.Status = UtilizationPointRuleStatus.HeightRangeInvalid;
                return;
            }

            rule.Status = UtilizationPointRuleStatus.Ok;
        }

        private bool EnsureActiveGroupReadyForInsertion(out UtilizationPointGroup? group)
        {
            group = null;
            if (ViewModel.ActiveGroup == null)
            {
                ViewModel.StatusMessage = "Selecione um grupo ativo.";
                return false;
            }

            UtilizationPointGroupDto dto = ViewModel.ActiveGroup.ToDto();
            UtilizationPointGroup domain = UtilizationPointProfilesMapper.ToDomain(dto);

            ValidateUtilizationPointGroupUseCase validator = new();
            UtilizationPointGroupValidationResult validation = validator.Execute(domain);
            if (!validation.IsValid)
            {
                ViewModel.StatusMessage = "O grupo ativo precisa ter pelo menos uma regra válida (nome, tipo e faixa de altura).";
                return false;
            }

            group = domain;
            return true;
        }

        private string BuildExecutionStatus(InsertionSummaryDto summary)
        {
            return $"Última execução: {summary.PointsInserted} inseridos ({summary.PointsConnected} conectados), " +
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

        private static UtilizationPointGroupViewModel BuildDefaultBathroomGroup()
        {
            UtilizationPointGroupViewModel group = new(Guid.NewGuid().ToString("N"), "Banheiro");
            group.Rules.Add(new UtilizationPointRuleViewModel
            {
                Name = "Chuveiro",
                MinMeters = 1.9,
                MaxMeters = 2.2,
            });
            group.Rules.Add(new UtilizationPointRuleViewModel
            {
                Name = "Vaso sanitário",
                MinMeters = 0.10,
                MaxMeters = 0.30,
            });
            group.Rules.Add(new UtilizationPointRuleViewModel
            {
                Name = "Ducha higiênica",
                MinMeters = 0.30,
                MaxMeters = 0.50,
            });
            group.Rules.Add(new UtilizationPointRuleViewModel
            {
                Name = "Lavatório",
                MinMeters = 0.50,
                MaxMeters = 0.80,
            });
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
