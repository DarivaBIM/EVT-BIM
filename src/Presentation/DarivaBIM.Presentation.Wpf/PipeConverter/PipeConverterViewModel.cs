using System;
using System.Collections.Generic;
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
        /// Quando true, o nível de referência dos marcadores é o nível do
        /// próprio vínculo CAD (auto-preenchido em <see cref="SelectedLevel"/>
        /// e bloqueado para edição). O offset continua editável e somado
        /// normalmente sobre esse nível. Quando false, o usuário escolhe
        /// livremente o nível no dropdown.
        /// </summary>
        public bool UseCadElevation
        {
            get => _useCadElevation;
            set
            {
                if (SetField(ref _useCadElevation, value))
                {
                    OnPropertyChanged(nameof(IsLevelInputEnabled));
                    if (_useCadElevation) TryApplyCadLinkLevel();
                }
            }
        }

        /// <summary>
        /// True quando o usuário pode escolher o nível manualmente no combo.
        /// False quando o nível vem do vínculo CAD (checkbox marcada).
        /// O input de offset NÃO depende disso — ele continua editável
        /// independentemente do checkbox.
        /// </summary>
        public bool IsLevelInputEnabled => !_useCadElevation;

        private long? _cadLinkLevelId;
        /// <summary>
        /// Id do nível ao qual o vínculo CAD selecionado está associado no
        /// Revit. Setado pelo handler que faz o pick do CAD. Quando
        /// <see cref="UseCadElevation"/> está true, este id é usado para
        /// auto-selecionar a opção correspondente em <see cref="SelectedLevel"/>.
        /// </summary>
        public long? CadLinkLevelId
        {
            get => _cadLinkLevelId;
            set
            {
                if (SetField(ref _cadLinkLevelId, value) && _useCadElevation)
                {
                    TryApplyCadLinkLevel();
                }
            }
        }

        private void TryApplyCadLinkLevel()
        {
            if (!_cadLinkLevelId.HasValue) return;
            foreach (LevelOptionViewModel level in Levels)
            {
                if (level.Id == _cadLinkLevelId.Value)
                {
                    SelectedLevel = level;
                    return;
                }
            }
        }

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
                    OnPropertyChanged(nameof(ToggleButtonText));
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

        /// <summary>
        /// Opções de tolerância exibidas no ComboBox da UI. Cinco níveis
        /// discretos em vez de um slider contínuo: na prática só uns poucos
        /// "presets" fazem diferença prática no resultado do detector.
        /// </summary>
        public ObservableCollection<BifilarToleranceOption> ToleranceOptions { get; } = new()
        {
            new BifilarToleranceOption(BifilarToleranceLevel.VeryLow,  "Muito baixa"),
            new BifilarToleranceOption(BifilarToleranceLevel.Low,      "Baixa"),
            new BifilarToleranceOption(BifilarToleranceLevel.Medium,   "Média"),
            new BifilarToleranceOption(BifilarToleranceLevel.High,     "Alta"),
            new BifilarToleranceOption(BifilarToleranceLevel.VeryHigh, "Muito alta"),
        };

        private BifilarToleranceOption? _selectedToleranceOption;
        public BifilarToleranceOption? SelectedToleranceOption
        {
            get => _selectedToleranceOption;
            set
            {
                if (SetField(ref _selectedToleranceOption, value))
                {
                    OnPropertyChanged(nameof(ToleranceLevel));
                    OnPropertyChanged(nameof(TolerancePercent));
                    OnPropertyChanged(nameof(ToleranceLevelIndex));
                    OnPropertyChanged(nameof(SelectedToleranceLabel));
                }
            }
        }

        /// <summary>
        /// Posição (0..4) do nível atual no Slider da UI. Setter clampa
        /// e seleciona a opção correspondente em <see cref="ToleranceOptions"/>.
        /// </summary>
        public int ToleranceLevelIndex
        {
            get => (int)ToleranceLevel;
            set
            {
                int clamped = value < 0 ? 0 : value >= ToleranceOptions.Count ? ToleranceOptions.Count - 1 : value;
                if (clamped < 0 || clamped >= ToleranceOptions.Count) return;
                SelectedToleranceOption = ToleranceOptions[clamped];
            }
        }

        /// <summary>
        /// Rótulo do nível atual (ex.: "Média"). Exibido acima do Slider para
        /// dar feedback imediato de qual posição está selecionada.
        /// </summary>
        public string SelectedToleranceLabel =>
            _selectedToleranceOption?.DisplayName ?? string.Empty;

        /// <summary>
        /// Nível de tolerância vigente (default: Média).
        /// </summary>
        public BifilarToleranceLevel ToleranceLevel =>
            _selectedToleranceOption?.Level ?? BifilarToleranceLevel.Medium;

        /// <summary>
        /// Percentual derivado do nível selecionado (0/25/50/75/100). É o que
        /// o adapter consome via <c>BifilarDetectionParameters.FromTolerance</c>.
        /// </summary>
        public double TolerancePercent => ToleranceLevel switch
        {
            BifilarToleranceLevel.VeryLow => 0.0,
            BifilarToleranceLevel.Low => 25.0,
            BifilarToleranceLevel.Medium => 50.0,
            BifilarToleranceLevel.High => 75.0,
            BifilarToleranceLevel.VeryHigh => 100.0,
            _ => 50.0,
        };

        public void SetToleranceLevel(BifilarToleranceLevel level)
        {
            foreach (BifilarToleranceOption opt in ToleranceOptions)
            {
                if (opt.Level == level)
                {
                    SelectedToleranceOption = opt;
                    return;
                }
            }
        }

        // ----- Bend angle constraints -----

        // Quando true (default), o detector NÃO aplica snap nos bends das
        // polylines geradoras de marcadores — preserva a geometria original
        // do CAD. Quando false, os 4 ângulos abaixo (os que estiverem
        // marcados) viram um conjunto de "ângulos permitidos"; bends fora
        // da janela de ±15° de algum permitido ficam intactos. Bends muito
        // próximos de 0° (|bend|<15°) viram retas — isto independe deste
        // flag, exceto quando ele está true (a rota é totalmente neutra).
        private bool _allowAnyBendAngle = true;
        public bool AllowAnyBendAngle
        {
            get => _allowAnyBendAngle;
            set
            {
                if (SetField(ref _allowAnyBendAngle, value))
                {
                    OnPropertyChanged(nameof(AreSpecificBendAnglesEnabled));
                }
            }
        }

        // Habilita/desabilita os 4 checkboxes de ângulos específicos no XAML.
        // Quando "qualquer ângulo" está marcado, eles aparecem opacos e
        // bloqueados — comunicação visual de que não fazem efeito.
        public bool AreSpecificBendAnglesEnabled => !_allowAnyBendAngle;

        private bool _allowBend22_5 = true;
        public bool AllowBend22_5 { get => _allowBend22_5; set => SetField(ref _allowBend22_5, value); }

        private bool _allowBend45 = true;
        public bool AllowBend45 { get => _allowBend45; set => SetField(ref _allowBend45, value); }

        private bool _allowBend60 = true;
        public bool AllowBend60 { get => _allowBend60; set => SetField(ref _allowBend60, value); }

        private bool _allowBend90 = true;
        public bool AllowBend90 { get => _allowBend90; set => SetField(ref _allowBend90, value); }

        /// <summary>
        /// Lista de ângulos permitidos (em graus) construída a partir dos
        /// checkboxes. Vazia quando "qualquer ângulo" está marcado — o
        /// adapter interpreta como "não aplicar snap".
        /// </summary>
        public IReadOnlyList<double> AllowedBendAnglesDeg
        {
            get
            {
                if (_allowAnyBendAngle) return Array.Empty<double>();
                List<double> list = new(4);
                if (_allowBend22_5) list.Add(22.5);
                if (_allowBend45) list.Add(45.0);
                if (_allowBend60) list.Add(60.0);
                if (_allowBend90) list.Add(90.0);
                return list;
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

        public string ToggleButtonText => (IsActive, Mode) switch
        {
            (true, _) => "Encerrar seleção de linhas",
            (false, PipeCadMappingMode.Bifilar) => "Selecionar parede → marcador",
            _ => "Selecionar linhas → marcadores",
        };

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
