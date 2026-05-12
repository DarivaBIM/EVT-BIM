using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.UtilizationPoints
{
    /// <summary>
    /// Linha de regra dentro de um <see cref="UtilizationPointGroupViewModel"/>.
    /// Mantém os campos editáveis pela UI (nome, tipo, faixa) e o estado de
    /// validação, sem qualquer dependência de Revit API. As propriedades de
    /// "saved type" servem para mostrar "Tipo não encontrado" quando o
    /// dropdown não consegue resolver a referência persistida.
    /// </summary>
    public class UtilizationPointRuleViewModel : ObservableObject
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (SetField(ref _name, value))
                {
                    OnPropertyChanged(nameof(HasName));
                }
            }
        }

        public bool HasName => !string.IsNullOrWhiteSpace(Name);

        private FamilyTypeOptionViewModel? _selectedFamilyType;
        public FamilyTypeOptionViewModel? SelectedFamilyType
        {
            get => _selectedFamilyType;
            set
            {
                if (SetField(ref _selectedFamilyType, value))
                {
                    OnPropertyChanged(nameof(FamilyDisplayName));
                    OnPropertyChanged(nameof(TypeDisplayName));
                    OnPropertyChanged(nameof(CategoryDisplayName));
                }
            }
        }

        private string _savedFamilyName = string.Empty;
        public string SavedFamilyName
        {
            get => _savedFamilyName;
            set
            {
                if (SetField(ref _savedFamilyName, value))
                {
                    OnPropertyChanged(nameof(FamilyDisplayName));
                }
            }
        }

        private string _savedTypeName = string.Empty;
        public string SavedTypeName
        {
            get => _savedTypeName;
            set
            {
                if (SetField(ref _savedTypeName, value))
                {
                    OnPropertyChanged(nameof(TypeDisplayName));
                }
            }
        }

        private string? _savedCategoryName;
        public string? SavedCategoryName
        {
            get => _savedCategoryName;
            set
            {
                if (SetField(ref _savedCategoryName, value))
                {
                    OnPropertyChanged(nameof(CategoryDisplayName));
                }
            }
        }

        private long? _savedElementId;
        public long? SavedElementId
        {
            get => _savedElementId;
            set => SetField(ref _savedElementId, value);
        }

        private string? _savedUniqueId;
        public string? SavedUniqueId
        {
            get => _savedUniqueId;
            set => SetField(ref _savedUniqueId, value);
        }

        public string FamilyDisplayName => SelectedFamilyType?.FamilyName ?? SavedFamilyName;
        public string TypeDisplayName => SelectedFamilyType?.TypeName ?? SavedTypeName;
        public string? CategoryDisplayName => SelectedFamilyType?.CategoryName ?? SavedCategoryName;

        private double _minMeters;
        public double MinMeters
        {
            get => _minMeters;
            set
            {
                if (SetField(ref _minMeters, value))
                {
                    OnPropertyChanged(nameof(MinMetersText));
                }
            }
        }

        private double _maxMeters;
        public double MaxMeters
        {
            get => _maxMeters;
            set
            {
                if (SetField(ref _maxMeters, value))
                {
                    OnPropertyChanged(nameof(MaxMetersText));
                }
            }
        }

        public string MinMetersText
        {
            get => MinMeters.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            set
            {
                if (TryParseMeters(value, out double parsed))
                    MinMeters = parsed;
                OnPropertyChanged(nameof(MinMetersText));
            }
        }

        public string MaxMetersText
        {
            get => MaxMeters.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            set
            {
                if (TryParseMeters(value, out double parsed))
                    MaxMeters = parsed;
                OnPropertyChanged(nameof(MaxMetersText));
            }
        }

        private UtilizationPointRuleStatus _status = UtilizationPointRuleStatus.Ok;
        public UtilizationPointRuleStatus Status
        {
            get => _status;
            set
            {
                if (SetField(ref _status, value))
                {
                    OnPropertyChanged(nameof(StatusLabel));
                    OnPropertyChanged(nameof(IsOk));
                    OnPropertyChanged(nameof(IsWarning));
                }
            }
        }

        public string StatusLabel => Status switch
        {
            UtilizationPointRuleStatus.Ok => "Ok",
            UtilizationPointRuleStatus.FamilyTypeMissing => "Tipo não informado",
            UtilizationPointRuleStatus.FamilyTypeNotFoundInDocument => "Tipo não encontrado",
            UtilizationPointRuleStatus.HeightRangeInvalid => "Altura inválida",
            UtilizationPointRuleStatus.NameMissing => "Sem nome",
            _ => "—",
        };

        public bool IsOk => Status == UtilizationPointRuleStatus.Ok;
        public bool IsWarning => Status != UtilizationPointRuleStatus.Ok;

        public UtilizationPointRuleDto ToDto()
        {
            return new UtilizationPointRuleDto
            {
                Name = Name ?? string.Empty,
                FamilyName = SelectedFamilyType?.FamilyName ?? SavedFamilyName,
                TypeName = SelectedFamilyType?.TypeName ?? SavedTypeName,
                CategoryName = SelectedFamilyType?.CategoryName ?? SavedCategoryName,
                ElementId = SelectedFamilyType?.ElementId ?? SavedElementId,
                UniqueId = SelectedFamilyType?.UniqueId ?? SavedUniqueId,
                MinMeters = MinMeters,
                MaxMeters = MaxMeters,
            };
        }

        public static UtilizationPointRuleViewModel FromDto(UtilizationPointRuleDto dto)
        {
            return new UtilizationPointRuleViewModel
            {
                Name = dto.Name ?? string.Empty,
                SavedFamilyName = dto.FamilyName ?? string.Empty,
                SavedTypeName = dto.TypeName ?? string.Empty,
                SavedCategoryName = dto.CategoryName,
                SavedElementId = dto.ElementId,
                SavedUniqueId = dto.UniqueId,
                MinMeters = dto.MinMeters,
                MaxMeters = dto.MaxMeters,
            };
        }

        private static bool TryParseMeters(string text, out double value)
        {
            string normalized = (text ?? string.Empty).Trim().Replace(",", ".");
            return double.TryParse(
                normalized,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }
    }
}
