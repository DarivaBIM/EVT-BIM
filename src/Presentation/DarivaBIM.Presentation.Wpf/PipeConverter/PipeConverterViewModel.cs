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
    ///
    /// The new workflow is staged:
    ///   1) select a CAD link (<see cref="SelectedCadLinkId"/> is set);
    ///   2) configure system/type/diameter/level/layer/mode;
    ///   3) create markers (placeholders rendered in magenta) either by
    ///      single-line pick (<see cref="IsActive"/>) or by batch
    ///      (per-layer unifilar or auto-detect bifilar);
    ///   4) review/adjust markers in the view;
    ///   5) convert markers to real pipes.
    /// </summary>
    public class PipeConverterViewModel : ObservableObject
    {
        public ObservableCollection<PipingSystemOptionViewModel> Systems { get; } = new();
        public ObservableCollection<PipeTypeOptionViewModel> PipeTypes { get; } = new();
        public ObservableCollection<double> Diameters { get; } = new();
        public ObservableCollection<LevelOptionViewModel> Levels { get; } = new();

        /// <summary>
        /// Layers presentes na geometria do vínculo CAD selecionado.
        /// Populado pelo handler que faz o scan da geometria após o pick do CAD.
        /// </summary>
        public ObservableCollection<string> CadLayers { get; } = new();

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

        private bool _useCadElevation;
        /// <summary>
        /// Quando true, o Z dos marcadores é tomado do próprio CAD (e o
        /// nível de referência é usado apenas como host do placeholder).
        /// Quando false, usa <see cref="SelectedLevel"/> + <see cref="OffsetMm"/>.
        /// </summary>
        public bool UseCadElevation
        {
            get => _useCadElevation;
            set
            {
                if (SetField(ref _useCadElevation, value))
                {
                    OnPropertyChanged(nameof(IsLevelInputEnabled));
                }
            }
        }

        /// <summary>
        /// Inverso de <see cref="UseCadElevation"/>. Usado pela UI para
        /// desabilitar o combo de nível e o offset enquanto a cota vier do CAD.
        /// </summary>
        public bool IsLevelInputEnabled => !_useCadElevation;

        // ----- CAD link / layer / mode -----

        private long? _selectedCadLinkId;
        public long? SelectedCadLinkId
        {
            get => _selectedCadLinkId;
            set
            {
                if (SetField(ref _selectedCadLinkId, value))
                {
                    OnPropertyChanged(nameof(HasCadLink));
                }
            }
        }

        private string? _selectedCadLinkName;
        public string? SelectedCadLinkName
        {
            get => _selectedCadLinkName;
            set => SetField(ref _selectedCadLinkName, value);
        }

        public bool HasCadLink => SelectedCadLinkId.HasValue;

        private string? _selectedLayer;
        public string? SelectedLayer
        {
            get => _selectedLayer;
            set => SetField(ref _selectedLayer, value);
        }

        private PipeCadMappingMode _mode = PipeCadMappingMode.Unifilar;
        public PipeCadMappingMode Mode
        {
            get => _mode;
            set
            {
                if (SetField(ref _mode, value))
                {
                    OnPropertyChanged(nameof(IsUnifilar));
                    OnPropertyChanged(nameof(IsBifilar));
                }
            }
        }

        public bool IsUnifilar
        {
            get => _mode == PipeCadMappingMode.Unifilar;
            set
            {
                if (value)
                    Mode = PipeCadMappingMode.Unifilar;
            }
        }

        public bool IsBifilar
        {
            get => _mode == PipeCadMappingMode.Bifilar;
            set
            {
                if (value)
                    Mode = PipeCadMappingMode.Bifilar;
            }
        }

        // ----- Tolerance -----

        private double _tolerancePercent = 50.0;
        /// <summary>
        /// Slider 0..100 que controla o detector bifilar (linhas mais "frouxas"
        /// pareiam tubos mais permissivamente). Vide
        /// <c>BifilarDetectionParameters.FromTolerance</c> para o mapeamento.
        /// Ignorado em modo unifilar.
        /// </summary>
        public double TolerancePercent
        {
            get => _tolerancePercent;
            set
            {
                double clamped = value;
                if (clamped < 0) clamped = 0;
                if (clamped > 100) clamped = 100;
                SetField(ref _tolerancePercent, clamped);
            }
        }

        // ----- Active state / status -----

        private bool _isActive;
        /// <summary>
        /// Modo de "selecionar linhas para criar marcadores" (loop de pick
        /// linha a linha) está ativo.
        /// </summary>
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

        private bool _isBusy;
        /// <summary>
        /// Operação batch (scan/criação em lote) em andamento. Desabilita
        /// botões para evitar reentrada enquanto a transação roda.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetField(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanConvertMarkers));
                }
            }
        }

        private int _activeViewMarkerCount;
        /// <summary>
        /// Quantidade de marcadores (placeholders taggeados) presentes na
        /// vista ativa. Atualizado pelos handlers a cada criação/conversão.
        /// O botão "Converter marcadores em tubos" só habilita quando > 0.
        /// </summary>
        public int ActiveViewMarkerCount
        {
            get => _activeViewMarkerCount;
            set
            {
                if (SetField(ref _activeViewMarkerCount, value))
                {
                    OnPropertyChanged(nameof(CanConvertMarkers));
                }
            }
        }

        public bool CanConvertMarkers => ActiveViewMarkerCount > 0 && !IsBusy;

        private string _statusMessage = "Selecione um vínculo CAD para começar.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public string ToggleButtonText => IsActive
            ? "Encerrar seleção de linhas"
            : "Selecionar linhas → marcadores";

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
