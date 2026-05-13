using System;
using System.Collections.ObjectModel;
using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.UtilizationPoints
{
    /// <summary>
    /// Grupo de configuração (ex.: Banheiro, Cozinha). Encapsula a coleção
    /// observável de regras e expõe sumários para a UI (quantidade de regras,
    /// quantidade de tipos ausentes).
    /// </summary>
    public class UtilizationPointGroupViewModel : ObservableObject
    {
        public UtilizationPointGroupViewModel(string id, string name)
        {
            Id = id;
            _name = name ?? string.Empty;
            Rules.CollectionChanged += (_, _) => RefreshSummaries();
        }

        public string Id { get; }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (SetField(ref _name, value))
                {
                    OnPropertyChanged(nameof(InitialLetter));
                }
            }
        }

        public string InitialLetter => string.IsNullOrWhiteSpace(Name)
            ? "?"
            : Name.Trim()[..1].ToUpperInvariant();

        // Marcado pelo InsertionViewModel quando o grupo se torna ativo,
        // permitindo que o card do sidebar mude o background sem precisar de
        // converter ou MultiBinding referenciando a janela inteira.
        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetField(ref _isActive, value);
        }

        public ObservableCollection<UtilizationPointRuleViewModel> Rules { get; } = new();

        public int RulesCount => Rules.Count;
        public int MissingTypesCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Rules.Count; i++)
                {
                    if (Rules[i].Status == UtilizationPointRuleStatus.FamilyTypeNotFoundInDocument
                        || Rules[i].Status == UtilizationPointRuleStatus.FamilyTypeMissing)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public bool HasMissingTypes => MissingTypesCount > 0;

        public string RulesCountLabel => $"{RulesCount} {(RulesCount == 1 ? "regra" : "regras")}";
        public string MissingTypesLabel => HasMissingTypes ? $"{MissingTypesCount} ausente" : string.Empty;

        public int ValidRulesCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Rules.Count; i++)
                {
                    if (Rules[i].IsOk) count++;
                }
                return count;
            }
        }

        public void RefreshSummaries()
        {
            OnPropertyChanged(nameof(RulesCount));
            OnPropertyChanged(nameof(RulesCountLabel));
            OnPropertyChanged(nameof(MissingTypesCount));
            OnPropertyChanged(nameof(MissingTypesLabel));
            OnPropertyChanged(nameof(HasMissingTypes));
            OnPropertyChanged(nameof(ValidRulesCount));
        }

        public UtilizationPointGroupDto ToDto()
        {
            UtilizationPointGroupDto dto = new()
            {
                Id = Id,
                Name = Name,
            };
            for (int i = 0; i < Rules.Count; i++)
            {
                dto.Rules.Add(Rules[i].ToDto());
            }
            return dto;
        }

        public static UtilizationPointGroupViewModel FromDto(UtilizationPointGroupDto dto)
        {
            UtilizationPointGroupViewModel vm = new(
                string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("N") : dto.Id,
                dto.Name ?? string.Empty);

            for (int i = 0; i < dto.Rules.Count; i++)
            {
                vm.Rules.Add(UtilizationPointRuleViewModel.FromDto(dto.Rules[i]));
            }
            return vm;
        }
    }
}
