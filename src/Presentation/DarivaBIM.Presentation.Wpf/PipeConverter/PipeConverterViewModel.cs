using System.Collections.ObjectModel;
using DarivaBIM.Presentation.Wpf.Common;
using DarivaBIM.Presentation.Wpf.Models;

namespace DarivaBIM.Presentation.Wpf.PipeConverter
{
    /// <summary>
    /// View model for the PipeCADMapper window. Lives in Presentation.Wpf so
    /// it is reusable across Revit versions and free of RevitAPI dependencies.
    /// IDs flowing through the bound options are plain <see cref="long"/>;
    /// conversion to/from <c>ElementId</c> happens in the Plugin/Adapter layer.
    /// </summary>
    public class PipeConverterViewModel : ObservableObject
    {
        public ObservableCollection<PipingSystemOptionViewModel> Systems { get; } = new();
        public ObservableCollection<PipeTypeOptionViewModel> PipeTypes { get; } = new();
        public ObservableCollection<double> Diameters { get; } = new();
        public ObservableCollection<LevelOptionViewModel> Levels { get; } = new();

        private PipingSystemOptionViewModel? _selectedSystem;
        public PipingSystemOptionViewModel? SelectedSystem
        {
            get => _selectedSystem;
            set => SetField(ref _selectedSystem, value);
        }

        private PipeTypeOptionViewModel? _selectedPipeType;
        public PipeTypeOptionViewModel? SelectedPipeType
        {
            get => _selectedPipeType;
            set
            {
                if (SetField(ref _selectedPipeType, value))
                {
                    RefreshDiameters();
                }
            }
        }

        private double? _selectedDiameterMm;
        public double? SelectedDiameterMm
        {
            get => _selectedDiameterMm;
            set => SetField(ref _selectedDiameterMm, value);
        }

        private LevelOptionViewModel? _selectedLevel;
        public LevelOptionViewModel? SelectedLevel
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

        private string _statusMessage = "Ferramenta desativada.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public string ToggleButtonText => IsActive
            ? "Desativar inserção de tubos"
            : "Ativar inserção de tubos";

        private void RefreshDiameters()
        {
            double? previous = SelectedDiameterMm;

            Diameters.Clear();

            if (SelectedPipeType == null)
            {
                SelectedDiameterMm = null;
                return;
            }

            foreach (double d in SelectedPipeType.AvailableDiametersMm)
            {
                Diameters.Add(d);
            }

            if (previous.HasValue && Diameters.Contains(previous.Value))
            {
                SelectedDiameterMm = previous;
            }
            else if (Diameters.Count > 0)
            {
                SelectedDiameterMm = Diameters[0];
            }
            else
            {
                SelectedDiameterMm = null;
            }
        }
    }
}
