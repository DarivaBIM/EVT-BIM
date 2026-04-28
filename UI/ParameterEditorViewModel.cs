using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace FamiliesImporterHub.UI
{
    public class ParameterEditorViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CommonParameterOption> Parameters { get; } = new();

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
