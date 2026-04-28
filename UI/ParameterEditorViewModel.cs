using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;
using FamiliesImporterHub.Infrastructure;

namespace FamiliesImporterHub.UI
{
    public class ParameterEditorViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CommonParameterOption> Parameters { get; } = new();

        public ObservableCollection<DisciplineFilterItem> DisciplineFilters { get; } = new();

        public ParameterEditorViewModel()
        {
            // Ordem dos checkboxes na UI.
            Discipline[] order =
            {
                Discipline.Hidraulica,
                Discipline.Eletrica,
                Discipline.Mecanica,
                Discipline.CombateIncendio,
                Discipline.Estrutura,
                Discipline.Arquitetura,
                Discipline.ModelosGenericos,
            };

            foreach (Discipline d in order)
            {
                DisciplineFilterItem item = new(d, DisplayName(d), isChecked: true);
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
                        foreach (DisciplineFilterItem d in DisciplineFilters)
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
            if (e.PropertyName != nameof(DisciplineFilterItem.IsChecked))
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

        public IReadOnlyList<Discipline> SelectedDisciplines =>
            DisciplineFilters.Where(d => d.IsChecked).Select(d => d.Discipline).ToList();

        private int _selectedCount;
        public int SelectedCount
        {
            get => _selectedCount;
            set
            {
                if (SetField(ref _selectedCount, value))
                    OnPropertyChanged(nameof(SelectionSummary));
            }
        }

        public string SelectionSummary => SelectedCount switch
        {
            0 => "Nenhum elemento selecionado.",
            1 => "1 elemento selecionado.",
            _ => $"{SelectedCount} elementos selecionados.",
        };

        private CommonParameterOption? _selectedParameter;
        public CommonParameterOption? SelectedParameter
        {
            get => _selectedParameter;
            set
            {
                if (SetField(ref _selectedParameter, value))
                {
                    OnPropertyChanged(nameof(ValueTypeHint));
                    OnPropertyChanged(nameof(IsParameterSelected));
                    OnPropertyChanged(nameof(ValidationMessage));
                }
            }
        }

        public bool IsParameterSelected => _selectedParameter != null;

        public string ValueTypeHint => _selectedParameter == null
            ? "Selecione um parâmetro."
            : $"Tipo: {DescribeStorageType(_selectedParameter.StorageType)}.";

        private string _value = string.Empty;
        public string Value
        {
            get => _value;
            set
            {
                if (SetField(ref _value, value))
                    OnPropertyChanged(nameof(ValidationMessage));
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

                return _selectedParameter.StorageType switch
                {
                    StorageType.Integer when !int.TryParse(_value, out _)
                        => "Valor inválido — esperado número inteiro.",
                    StorageType.Double when !double.TryParse(
                        _value.Replace(",", "."),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out _)
                        => "Valor inválido — esperado número decimal.",
                    _ => string.Empty,
                };
            }
        }

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
            set => SetField(ref _isSelectionActive, value);
        }

        // Mensagem destacada em vermelho quando os elementos selecionados não
        // compartilham nenhum parâmetro editável em comum.
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

        private static string DescribeStorageType(StorageType t) => t switch
        {
            StorageType.String => "Texto",
            StorageType.Integer => "Número inteiro",
            StorageType.Double => "Número decimal",
            StorageType.ElementId => "Referência (ElementId)",
            _ => "Outro",
        };

        private static string DisplayName(Discipline d) => d switch
        {
            Discipline.Hidraulica => "Hidráulica",
            Discipline.Eletrica => "Elétrica",
            Discipline.Mecanica => "Mecânica",
            Discipline.CombateIncendio => "Combate a incêndio",
            Discipline.Estrutura => "Estrutura",
            Discipline.Arquitetura => "Arquitetura",
            Discipline.ModelosGenericos => "Modelos genéricos",
            _ => d.ToString(),
        };
    }

    public class DisciplineFilterItem : INotifyPropertyChanged
    {
        public DisciplineFilterItem(Discipline discipline, string displayName, bool isChecked)
        {
            Discipline = discipline;
            DisplayName = displayName;
            _isChecked = isChecked;
        }

        public Discipline Discipline { get; }
        public string DisplayName { get; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                    return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class CommonParameterOption
    {
        public CommonParameterOption(string name, StorageType storageType, bool isInstance)
        {
            Name = name;
            StorageType = storageType;
            IsInstance = isInstance;
        }

        public string Name { get; }
        public StorageType StorageType { get; }
        public bool IsInstance { get; }

        public string DisplayName => IsInstance
            ? Name
            : $"{Name} (tipo)";

        public override string ToString() => DisplayName;
    }
}
