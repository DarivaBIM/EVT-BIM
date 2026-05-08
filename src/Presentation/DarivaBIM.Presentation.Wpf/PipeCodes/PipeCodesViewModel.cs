using System.Collections.Generic;
using System.ComponentModel;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.PipeCodes
{
    /// <summary>
    /// View model raiz da janela "Codificar Tubos". Expõe as quatro caixinhas
    /// (uma por <see cref="TigrePipeStatus"/>), o cabeçalho com a situação do
    /// shared parameter e o estado dos botões. Os comandos (scan/apply/clear/
    /// ensure) ficam no code-behind da janela porque tocam ExternalEvents do
    /// Revit.
    /// </summary>
    public sealed class PipeCodesViewModel : ObservableObject
    {
        public PipeCodesViewModel()
        {
            NoMatchSection = new PipeCodesSectionViewModel(
                TigrePipeStatus.NoMatch,
                "Sem correspondência",
                "Sem casamento no catálogo Tigre. 'Inserir' não tem efeito aqui — útil só pra apagar valores legados.");

            DivergentSection = new PipeCodesSectionViewModel(
                TigrePipeStatus.Divergent,
                "Códigos divergentes",
                "Tubos com Tigre: Código gravado, mas com valor diferente do que o catálogo casaria hoje.");

            MissingSection = new PipeCodesSectionViewModel(
                TigrePipeStatus.Missing,
                "Sem código (com correspondência)",
                "Tubos vazios que casam com o catálogo — prontos para receber o código.");

            OkSection = new PipeCodesSectionViewModel(
                TigrePipeStatus.Ok,
                "Códigos corretos",
                "Tubos já com o código correto. Mantenha selecionados aqui só se quiser regravar/apagar.");

            NoMatchSection.PropertyChanged += OnSectionPropertyChanged;
            DivergentSection.PropertyChanged += OnSectionPropertyChanged;
            MissingSection.PropertyChanged += OnSectionPropertyChanged;
            OkSection.PropertyChanged += OnSectionPropertyChanged;
        }

        public PipeCodesSectionViewModel NoMatchSection { get; }
        public PipeCodesSectionViewModel DivergentSection { get; }
        public PipeCodesSectionViewModel MissingSection { get; }
        public PipeCodesSectionViewModel OkSection { get; }

        // ---------- Cabeçalho com a situação do shared parameter ----------

        private int _catalogCount;
        public int CatalogCount
        {
            get => _catalogCount;
            set
            {
                if (SetField(ref _catalogCount, value))
                    OnPropertyChanged(nameof(CatalogCountText));
            }
        }

        public string CatalogCountText => $"Catálogo Tigre: {CatalogCount} item(ns)";

        private int _pipesTotal;
        public int PipesTotal
        {
            get => _pipesTotal;
            set
            {
                if (SetField(ref _pipesTotal, value))
                {
                    OnPropertyChanged(nameof(PipesTotalText));
                    OnPropertyChanged(nameof(CanCreateParameter));
                }
            }
        }

        public string PipesTotalText => $"Total de tubos: {PipesTotal}";

        private int _pipesWithParameter;
        public int PipesWithParameter
        {
            get => _pipesWithParameter;
            set
            {
                if (SetField(ref _pipesWithParameter, value))
                    OnPropertyChanged(nameof(ParameterStatusText));
            }
        }

        private int _pipesWithoutParameter;
        public int PipesWithoutParameter
        {
            get => _pipesWithoutParameter;
            set
            {
                if (SetField(ref _pipesWithoutParameter, value))
                    OnPropertyChanged(nameof(ParameterStatusText));
            }
        }

        public string ParameterStatusText =>
            $"Com parâmetro Tigre: Código: {PipesWithParameter}    ·    Sem parâmetro: {PipesWithoutParameter}";

        private bool _parameterIsBound;
        public bool ParameterIsBound
        {
            get => _parameterIsBound;
            set
            {
                if (SetField(ref _parameterIsBound, value))
                {
                    OnPropertyChanged(nameof(ParameterIsNotBound));
                    OnPropertyChanged(nameof(CanCreateParameter));
                    OnPropertyChanged(nameof(CanApply));
                    OnPropertyChanged(nameof(CanClear));
                }
            }
        }

        public bool ParameterIsNotBound => !ParameterIsBound;

        // ---------- Estado dos botões ----------

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetField(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanCreateParameter));
                    OnPropertyChanged(nameof(CanApply));
                    OnPropertyChanged(nameof(CanClear));
                }
            }
        }

        public bool CanCreateParameter => !IsBusy && ParameterIsNotBound && PipesTotal > 0;

        public bool CanApply => !IsBusy && ParameterIsBound && HasAnySelection;

        public bool CanClear => !IsBusy && ParameterIsBound && HasAnySelection;

        private bool _hasAnySelection;
        public bool HasAnySelection
        {
            get => _hasAnySelection;
            private set
            {
                if (SetField(ref _hasAnySelection, value))
                {
                    OnPropertyChanged(nameof(CanApply));
                    OnPropertyChanged(nameof(CanClear));
                }
            }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        // ---------- Operações ----------

        /// <summary>
        /// Substitui o conteúdo das quatro seções com base no resultado do
        /// scan. Chamado tanto na carga inicial quanto após cada operação.
        /// </summary>
        public void ApplyScan(TigreScanResult scan)
        {
            CatalogCount = scan.CatalogCount;
            PipesTotal = scan.PipesTotal;
            PipesWithParameter = scan.PipesWithParameter;
            PipesWithoutParameter = scan.PipesWithoutParameter;
            ParameterIsBound = scan.ParameterIsBound;

            FillSection(NoMatchSection, scan.Groups, TigrePipeStatus.NoMatch);
            FillSection(DivergentSection, scan.Groups, TigrePipeStatus.Divergent);
            FillSection(MissingSection, scan.Groups, TigrePipeStatus.Missing);
            FillSection(OkSection, scan.Groups, TigrePipeStatus.Ok);

            // Após cada scan a seleção é zerada — IDs antigos podem nem
            // existir mais no documento.
            HasAnySelection = false;

            if (!string.IsNullOrEmpty(scan.ErrorMessage))
                StatusMessage = scan.ErrorMessage!;
        }

        public IReadOnlyList<long> CollectSelectedIds()
        {
            List<long> ids = new();
            CollectFrom(NoMatchSection, ids);
            CollectFrom(DivergentSection, ids);
            CollectFrom(MissingSection, ids);
            CollectFrom(OkSection, ids);
            return ids;
        }

        private void OnSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PipeCodesSectionViewModel.SelectedCount))
                return;

            int total = NoMatchSection.SelectedCount
                      + DivergentSection.SelectedCount
                      + MissingSection.SelectedCount
                      + OkSection.SelectedCount;
            HasAnySelection = total > 0;
        }

        private static void FillSection(
            PipeCodesSectionViewModel section,
            IReadOnlyList<TigreScanGroup> groups,
            TigrePipeStatus status)
        {
            section.Groups.Clear();
            foreach (TigreScanGroup g in groups)
            {
                if (g.Status != status)
                    continue;

                section.Groups.Add(new PipeCodesGroupViewModel(
                    g.TypeName,
                    g.DiameterMm,
                    g.Count,
                    g.Status,
                    g.ElementIds,
                    g.MatchedCode));
            }
        }

        private static void CollectFrom(PipeCodesSectionViewModel section, List<long> sink)
        {
            foreach (PipeCodesGroupViewModel g in section.Groups)
            {
                if (!g.IsSelected)
                    continue;
                foreach (long id in g.ElementIds)
                    sink.Add(id);
            }
        }
    }
}
