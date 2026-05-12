using System;
using System.Collections.ObjectModel;
using System.Linq;
using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.UtilizationPoints
{
    /// <summary>
    /// View model raiz da janela "Inserir Pontos de Utilização". Mantém a
    /// lista observável de grupos, o grupo ativo, o catálogo de tipos de
    /// família do documento, os níveis disponíveis, o nível de referência
    /// selecionado, o estado de execução e o resumo da última execução.
    /// Vive em Presentation.Wpf — Revit-agnóstico.
    /// </summary>
    public class UtilizationPointInsertionViewModel : ObservableObject
    {
        public UtilizationPointInsertionViewModel()
        {
            Groups.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(GroupsCount));
                OnPropertyChanged(nameof(HasGroups));
            };
        }

        public ObservableCollection<UtilizationPointGroupViewModel> Groups { get; } = new();

        public ObservableCollection<FamilyTypeOptionViewModel> FamilyTypes { get; } = new();

        public ObservableCollection<LevelOptionViewModel> Levels { get; } = new();

        public int GroupsCount => Groups.Count;
        public bool HasGroups => Groups.Count > 0;

        private UtilizationPointGroupViewModel? _activeGroup;
        public UtilizationPointGroupViewModel? ActiveGroup
        {
            get => _activeGroup;
            set
            {
                if (SetField(ref _activeGroup, value))
                {
                    OnPropertyChanged(nameof(HasActiveGroup));
                    OnPropertyChanged(nameof(ActiveRules));
                    OnPropertyChanged(nameof(ActiveGroupName));
                    OnPropertyChanged(nameof(ActiveGroupValidRulesLabel));
                    OnPropertyChanged(nameof(ActiveGroupMissingTypesLabel));
                    OnPropertyChanged(nameof(ActiveGroupTypesFoundLabel));
                }
            }
        }

        public bool HasActiveGroup => ActiveGroup != null;

        public ObservableCollection<UtilizationPointRuleViewModel>? ActiveRules => ActiveGroup?.Rules;

        public string ActiveGroupName => ActiveGroup?.Name ?? string.Empty;

        public string ActiveGroupValidRulesLabel
        {
            get
            {
                if (ActiveGroup == null) return "0";
                return ActiveGroup.ValidRulesCount.ToString();
            }
        }

        public string ActiveGroupMissingTypesLabel
        {
            get
            {
                if (ActiveGroup == null) return "0";
                return ActiveGroup.MissingTypesCount.ToString();
            }
        }

        public string ActiveGroupTypesFoundLabel
        {
            get
            {
                if (ActiveGroup == null) return "0";
                int total = ActiveGroup.Rules.Count;
                return Math.Max(total - ActiveGroup.MissingTypesCount, 0).ToString();
            }
        }

        private LevelOptionViewModel? _selectedLevel;
        public LevelOptionViewModel? SelectedLevel
        {
            get => _selectedLevel;
            set => SetField(ref _selectedLevel, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetField(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsIdle));
                }
            }
        }

        public bool IsIdle => !IsBusy;

        private string _statusMessage = "Pronto.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        private bool _isContinuousMode;
        public bool IsContinuousMode
        {
            get => _isContinuousMode;
            set
            {
                if (SetField(ref _isContinuousMode, value))
                {
                    OnPropertyChanged(nameof(ContinuousModeLabel));
                }
            }
        }

        public string ContinuousModeLabel => IsContinuousMode ? "Desativar modo contínuo" : "Ativar modo contínuo";

        private InsertionSummaryDto? _lastSummary;
        public InsertionSummaryDto? LastSummary
        {
            get => _lastSummary;
            set
            {
                if (SetField(ref _lastSummary, value))
                {
                    OnPropertyChanged(nameof(HasLastSummary));
                    OnPropertyChanged(nameof(LastSummaryHeader));
                    OnPropertyChanged(nameof(LastSummaryAnalyzedLabel));
                    OnPropertyChanged(nameof(LastSummaryFreeLabel));
                    OnPropertyChanged(nameof(LastSummaryInsertedLabel));
                    OnPropertyChanged(nameof(LastSummaryWithoutRangeLabel));
                    OnPropertyChanged(nameof(LastSummaryErrorsLabel));
                    OnPropertyChanged(nameof(Messages));
                }
            }
        }

        public bool HasLastSummary => LastSummary != null;

        public string LastSummaryHeader => LastSummary == null
            ? "Sem execução nesta sessão."
            : $"Última execução: {DateTime.Now:HH:mm}";

        public string LastSummaryAnalyzedLabel => $"{LastSummary?.ElementsAnalyzed ?? 0} analisados";
        public string LastSummaryFreeLabel => $"{LastSummary?.FreeConnectorsFound ?? 0} livres";
        public string LastSummaryInsertedLabel => $"{LastSummary?.PointsInserted ?? 0} inseridos";
        public string LastSummaryWithoutRangeLabel => $"{LastSummary?.ConnectorsWithoutRange ?? 0} sem faixa";
        public string LastSummaryErrorsLabel => $"{LastSummary?.Errors ?? 0} erros";

        public ObservableCollection<InsertionMessageDto> Messages { get; } = new();

        public void RefreshMessages()
        {
            Messages.Clear();
            if (LastSummary == null) return;
            for (int i = 0; i < LastSummary.Messages.Count; i++)
            {
                Messages.Add(LastSummary.Messages[i]);
            }
        }

        public void OnActiveGroupChanged()
        {
            OnPropertyChanged(nameof(ActiveGroupValidRulesLabel));
            OnPropertyChanged(nameof(ActiveGroupMissingTypesLabel));
            OnPropertyChanged(nameof(ActiveGroupTypesFoundLabel));
            OnPropertyChanged(nameof(ActiveGroupName));
        }

        public FamilyTypeOptionViewModel? FindFamilyType(string familyName, string typeName)
        {
            if (string.IsNullOrEmpty(familyName) && string.IsNullOrEmpty(typeName))
                return null;
            return FamilyTypes.FirstOrDefault(o =>
                string.Equals(o.FamilyName, familyName, StringComparison.Ordinal)
                && string.Equals(o.TypeName, typeName, StringComparison.Ordinal));
        }
    }
}
