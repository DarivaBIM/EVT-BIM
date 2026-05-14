using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
                "Tubos sem item equivalente no catálogo Tigre. Útil para revisar legados.");

            DivergentSection = new PipeCodesSectionViewModel(
                TigrePipeStatus.Divergent,
                "Códigos divergentes",
                "Tubos com código gravado diferente do código previsto pelo catálogo.");

            MissingSection = new PipeCodesSectionViewModel(
                TigrePipeStatus.Missing,
                "Prontos para codificar",
                "Tubos sem código, mas com correspondência no catálogo Tigre.");

            OkSection = new PipeCodesSectionViewModel(
                TigrePipeStatus.Ok,
                "Códigos corretos",
                "Tubos já com o código correto. Marque só para regravar ou apagar.");

            NoMatchSection.PropertyChanged += OnSectionPropertyChanged;
            DivergentSection.PropertyChanged += OnSectionPropertyChanged;
            MissingSection.PropertyChanged += OnSectionPropertyChanged;
            OkSection.PropertyChanged += OnSectionPropertyChanged;
        }

        public PipeCodesSectionViewModel NoMatchSection { get; }
        public PipeCodesSectionViewModel DivergentSection { get; }
        public PipeCodesSectionViewModel MissingSection { get; }
        public PipeCodesSectionViewModel OkSection { get; }

        // ---------- Coluna 1: Resumo do projeto ----------

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
                    OnPropertyChanged(nameof(ParameterStatusWarning));
                    OnPropertyChanged(nameof(ParameterStatusOk));
                }
            }
        }

        public string PipesTotalText => $"Tubos no projeto: {PipesTotal}";

        private int _uniqueTypeCount;
        public int UniqueTypeCount
        {
            get => _uniqueTypeCount;
            private set
            {
                if (SetField(ref _uniqueTypeCount, value))
                    OnPropertyChanged(nameof(UniqueTypeCountText));
            }
        }

        public string UniqueTypeCountText => $"Tipos encontrados: {UniqueTypeCount}";

        // ---------- Coluna 2: Parâmetro de código ----------

        private int _pipesWithParameter;
        public int PipesWithParameter
        {
            get => _pipesWithParameter;
            set
            {
                if (SetField(ref _pipesWithParameter, value))
                    OnPropertyChanged(nameof(PipesWithParameterText));
            }
        }

        public string PipesWithParameterText => $"Com parâmetro de código: {PipesWithParameter}";

        private int _pipesWithoutParameter;
        public int PipesWithoutParameter
        {
            get => _pipesWithoutParameter;
            set
            {
                if (SetField(ref _pipesWithoutParameter, value))
                    OnPropertyChanged(nameof(PipesWithoutParameterText));
            }
        }

        public string PipesWithoutParameterText => $"Sem parâmetro de código: {PipesWithoutParameter}";

        private bool _parameterIsBound;
        public bool ParameterIsBound
        {
            get => _parameterIsBound;
            set
            {
                if (SetField(ref _parameterIsBound, value))
                {
                    OnPropertyChanged(nameof(ParameterIsNotBound));
                    OnPropertyChanged(nameof(ParameterStatusOk));
                    OnPropertyChanged(nameof(ParameterStatusWarning));
                    OnPropertyChanged(nameof(CanCreateParameter));
                    OnPropertyChanged(nameof(CanApply));
                    OnPropertyChanged(nameof(CanClear));
                    RefreshContextualStatus();
                }
            }
        }

        public bool ParameterIsNotBound => !ParameterIsBound;

        // ---------- Coluna 3: Status geral ----------

        /// <summary>
        /// "Tudo pronto" — todos os tubos do projeto já expõem o parâmetro
        /// Tigre: Código. Vinculado ao alerta verde da terceira coluna.
        /// </summary>
        public bool ParameterStatusOk => ParameterIsBound && PipesTotal > 0;

        /// <summary>
        /// "Ação necessária" — existem tubos no projeto sem o parâmetro.
        /// Vinculado ao alerta amarelo da terceira coluna.
        /// </summary>
        public bool ParameterStatusWarning => ParameterIsNotBound && PipesTotal > 0;

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
                    RefreshContextualStatus();
                }
            }
        }

        private int _totalSelectedCount;
        /// <summary>
        /// Soma de tubos marcados nas quatro caixinhas. Exposta para que os
        /// botões inferiores possam mostrar o contador entre parênteses
        /// (ex.: "Inserir/Atualizar Códigos (12)").
        /// </summary>
        public int TotalSelectedCount
        {
            get => _totalSelectedCount;
            private set
            {
                if (SetField(ref _totalSelectedCount, value))
                {
                    OnPropertyChanged(nameof(ApplyButtonLabel));
                    OnPropertyChanged(nameof(ClearButtonLabel));
                }
            }
        }

        public string ApplyButtonLabel => TotalSelectedCount > 0
            ? $"Inserir/Atualizar Códigos ({TotalSelectedCount})"
            : "Inserir/Atualizar Códigos";

        public string ClearButtonLabel => TotalSelectedCount > 0
            ? $"Deletar Códigos ({TotalSelectedCount})"
            : "Deletar Códigos";

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

            UniqueTypeCount = scan.Groups
                .Select(g => g.TypeName)
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .Count();

            // Após cada scan a seleção é zerada — IDs antigos podem nem
            // existir mais no documento.
            TotalSelectedCount = 0;
            HasAnySelection = false;

            if (!string.IsNullOrEmpty(scan.ErrorMessage))
                StatusMessage = scan.ErrorMessage!;
            else
                RefreshContextualStatus();
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

        /// <summary>
        /// Atualiza a mensagem do rodapé com base no estado atual: ausência
        /// de parâmetro, ausência de seleção, ou pronto pra aplicar. Chamada
        /// pelo code-behind após cada operação ou diretamente quando muda
        /// algum estado relevante.
        /// </summary>
        public void RefreshContextualStatus()
        {
            if (PipesTotal == 0)
            {
                StatusMessage = "Nenhum tubo encontrado no projeto ativo.";
                return;
            }

            if (!ParameterIsBound)
            {
                StatusMessage = "Crie o parâmetro de código para liberar os botões de inserir/atualizar.";
                return;
            }

            StatusMessage = HasAnySelection
                ? "Revise os itens selecionados antes de inserir, regravar ou apagar códigos."
                : "Marque os tubos que deseja atualizar e clique em Inserir/Atualizar Códigos.";
        }

        private void OnSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PipeCodesSectionViewModel.SelectedCount))
                return;

            int total = NoMatchSection.SelectedCount
                      + DivergentSection.SelectedCount
                      + MissingSection.SelectedCount
                      + OkSection.SelectedCount;
            TotalSelectedCount = total;
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
