using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FamiliesImporterHub.UI
{
    public class PipeConverterViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<PipingSystemOption> Systems { get; } = new();
        public ObservableCollection<PipeTypeOption> PipeTypes { get; } = new();
        public ObservableCollection<double> Diameters { get; } = new();
        public ObservableCollection<LevelOption> Levels { get; } = new();

        private PipingSystemOption? _selectedSystem;
        public PipingSystemOption? SelectedSystem
        {
            get => _selectedSystem;
            set => SetField(ref _selectedSystem, value);
        }

        private PipeTypeOption? _selectedPipeType;
        public PipeTypeOption? SelectedPipeType
        {
            get => _selectedPipeType;
            set
            {
                if (SetField(ref _selectedPipeType, value))
                {
                    PipeTypeChanged?.Invoke();
                }
            }
        }

        private double? _selectedDiameterMm;
        public double? SelectedDiameterMm
        {
            get => _selectedDiameterMm;
            set => SetField(ref _selectedDiameterMm, value);
        }

        private LevelOption? _selectedLevel;
        public LevelOption? SelectedLevel
        {
            get => _selectedLevel;
            set => SetField(ref _selectedLevel, value);
        }

        private double _offsetMm;
        public double OffsetMm
        {
            get => _offsetMm;
            set => SetField(ref _offsetMm, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetField(ref _isActive, value))
                {
                    OnPropertyChanged(nameof(ToggleButtonText));
                }
            }
        }

        public string ToggleButtonText => IsActive
            ? "Desativar inserção de tubos"
            : "Ativar inserção de tubos";

        public event System.Action? PipeTypeChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    public class PipingSystemOption
    {
        public PipingSystemOption(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }
        public string Name { get; }

        public override string ToString() => Name;
    }

    public class PipeTypeOption
    {
        public PipeTypeOption(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }
        public string Name { get; }

        public override string ToString() => Name;
    }

    public class LevelOption
    {
        public LevelOption(int id, string name, double elevationFeet)
        {
            Id = id;
            Name = name;
            ElevationFeet = elevationFeet;
        }

        public int Id { get; }
        public string Name { get; }
        public double ElevationFeet { get; }

        public override string ToString() => Name;
    }
}
