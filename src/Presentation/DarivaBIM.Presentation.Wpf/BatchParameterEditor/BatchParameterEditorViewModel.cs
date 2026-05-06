using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DarivaBIM.Presentation.Wpf.BatchParameterEditor
{
    /// <summary>
    /// Revit-agnostic view-model that drives the Batch Parameter Editor window.
    /// Lives in Presentation.Wpf and depends only on neutral types
    /// (<see cref="ParameterValueKind"/>, <see cref="ParameterDiscipline"/>);
    /// the plugin adapter is responsible for mapping to/from
    /// <c>Autodesk.Revit.DB.StorageType</c> and the adapter-side discipline.
    /// </summary>
    public class BatchParameterEditorViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ParameterOptionViewModel> Parameters { get; } = new();

        public ObservableCollection<DisciplineFilterViewModel> DisciplineFilters { get; } = new();

        public BatchParameterEditorViewModel()
        {
            ParameterDiscipline[] order =
            {
                ParameterDiscipline.Hidraulica,
                ParameterDiscipline.Eletrica,
                ParameterDiscipline.Mecanica,
                ParameterDiscipline.CombateIncendio,
                ParameterDiscipline.Estrutura,
                ParameterDiscipline.Arquitetura,
                ParameterDiscipline.ModelosGenericos,
            };

            foreach (ParameterDiscipline d in order)
            {
                DisciplineFilterViewModel item = new(d, DisplayName(d), isChecked: true);
                item.PropertyChanged += OnDisciplineItemChanged;
                DisciplineFilters.Add(item);
            }
        }

        private bool _suppressDisciplineSync;

        private bool _isAllDisciplinesSelected = true;
        public bool IsAllDisciplinesSelected
        {
            get => _isAllDisciplinesSelected;
            set
            {
                if (SetField(ref _isAllDisciplinesSelected, value))
                {
                    if (_suppressDisciplineSync)
                        return;

                    _suppressDisciplineSync = true;
                    try
                    {
                        foreach (DisciplineFilterViewModel d in DisciplineFilters)
                            d.IsChecked = value;
                    }
                    finally
                    {
                        _suppressDisciplineSync = false;
                    }
                }
            }
        }

        private void OnDisciplineItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DisciplineFilterViewModel.IsChecked))
                return;

            if (_suppressDisciplineSync)
                return;

            _suppressDisciplineSync = true;
            try
            {
                bool allChecked = DisciplineFilters.All(d => d.IsChecked);
                if (_isAllDisciplinesSelected != allChecked)
                {
                    _isAllDisciplinesSelected = allChecked;
                    OnPropertyChanged(nameof(IsAllDisciplinesSelected));
                }
            }
            finally
            {
                _suppressDisciplineSync = false;
            }
        }

        public IReadOnlyList<ParameterDiscipline> SelectedDisciplines =>
            DisciplineFilters.Where(d => d.IsChecked).Select(d => d.Discipline).ToList();

        private int _selectedCount;
        public int SelectedCount
        {
            get => _selectedCount;
            set
            {
                if (SetField(ref _selectedCount, value))
                {
                    OnPropertyChanged(nameof(SelectionSummary));
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(HasNoSelection));
                    OnPropertyChanged(nameof(SelectButtonText));
                    OnPropertyChanged(nameof(CanApply));
                }
            }
        }

        public bool HasSelection => _selectedCount > 0;

        public bool HasNoSelection => _selectedCount == 0;

        public string SelectionSummary => SelectedCount switch
        {
            0 => "Nenhum elemento selecionado.",
            1 => "1 elemento selecionado e pronto para edição.",
            _ => $"{SelectedCount} elementos selecionados e prontos para edição.",
        };

        public string SelectButtonText => HasSelection
            ? "Selecionar elementos (ajustar)"
            : "Selecionar elementos";

        private string _selectionCategoriesSummary = string.Empty;
        public string SelectionCategoriesSummary
        {
            get => _selectionCategoriesSummary;
            set
            {
                if (SetField(ref _selectionCategoriesSummary, value))
                    OnPropertyChanged(nameof(HasSelectionCategoriesSummary));
            }
        }

        public bool HasSelectionCategoriesSummary => !string.IsNullOrEmpty(_selectionCategoriesSummary);

        private ParameterOptionViewModel? _selectedParameter;
        public ParameterOptionViewModel? SelectedParameter
        {
            get => _selectedParameter;
            set
            {
                if (SetField(ref _selectedParameter, value))
                {
                    OnPropertyChanged(nameof(ValueTypeHint));
                    OnPropertyChanged(nameof(IsParameterSelected));
                    OnPropertyChanged(nameof(ValidationMessage));
                    OnPropertyChanged(nameof(CanApply));
                }
            }
        }

        public bool IsParameterSelected => _selectedParameter != null;

        public string ValueTypeHint => _selectedParameter == null
            ? "Selecione um parâmetro."
            : $"Tipo: {DescribeValueKind(_selectedParameter.ValueKind)}.";

        private string _value = string.Empty;
        public string Value
        {
            get => _value;
            set
            {
                if (SetField(ref _value, value))
                {
                    OnPropertyChanged(nameof(ValidationMessage));
                    OnPropertyChanged(nameof(CanApply));
                }
            }
        }

        public string ValidationMessage
        {
            get
            {
                if (_selectedParameter == null)
                    return string.Empty;

                if (string.IsNullOrEmpty(_value))
                    return string.Empty;

                return _selectedParameter.ValueKind switch
                {
                    ParameterValueKind.Integer when !int.TryParse(_value, out _)
                        => "Valor inválido — esperado número inteiro.",
                    ParameterValueKind.Decimal when !double.TryParse(
                        _value.Replace(",", "."),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out _)
                        => "Valor inválido — esperado número decimal.",
                    _ => string.Empty,
                };
            }
        }

        public bool CanApply =>
            HasSelection
            && _selectedParameter != null
            && string.IsNullOrEmpty(ValidationMessage)
            && !_isSelectionActive;

        private string _statusMessage = "Clique em \"Selecionar elementos\" para começar.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        private bool _isSelectionActive;
        public bool IsSelectionActive
        {
            get => _isSelectionActive;
            set
            {
                if (SetField(ref _isSelectionActive, value))
                    OnPropertyChanged(nameof(CanApply));
            }
        }

        private string _noCommonParametersMessage = string.Empty;
        public string NoCommonParametersMessage
        {
            get => _noCommonParametersMessage;
            set
            {
                if (SetField(ref _noCommonParametersMessage, value))
                    OnPropertyChanged(nameof(HasNoCommonParametersMessage));
            }
        }

        public bool HasNoCommonParametersMessage => !string.IsNullOrEmpty(_noCommonParametersMessage);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(name);
            return true;
        }

        private static string DescribeValueKind(ParameterValueKind kind) => kind switch
        {
            ParameterValueKind.Text => "Texto",
            ParameterValueKind.Integer => "Número inteiro",
            ParameterValueKind.Decimal => "Número decimal",
            ParameterValueKind.ElementReference => "Referência (ElementId)",
            _ => "Outro",
        };

        private static string DisplayName(ParameterDiscipline d) => d switch
        {
            ParameterDiscipline.Hidraulica => "Hidráulica",
            ParameterDiscipline.Eletrica => "Elétrica",
            ParameterDiscipline.Mecanica => "Mecânica",
            ParameterDiscipline.CombateIncendio => "Combate a incêndio",
            ParameterDiscipline.Estrutura => "Estrutura",
            ParameterDiscipline.Arquitetura => "Arquitetura",
            ParameterDiscipline.ModelosGenericos => "Modelos genéricos",
            _ => d.ToString(),
        };
    }
}
