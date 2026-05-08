using System.Collections.ObjectModel;
using System.ComponentModel;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.PipeCodes
{
    /// <summary>
    /// Caixinha colorida com um conjunto de grupos de tubos no mesmo estado
    /// (Vermelho/Laranja/Amarelo/Verde). Expõe um "Selecionar todos" que
    /// reflete e dirige a seleção das linhas internas.
    /// </summary>
    public sealed class PipeCodesSectionViewModel : ObservableObject
    {
        public PipeCodesSectionViewModel(TigrePipeStatus status, string title, string description)
        {
            Status = status;
            Title = title;
            Description = description;
            Groups.CollectionChanged += (_, e) =>
            {
                if (e.OldItems != null)
                {
                    foreach (object? item in e.OldItems)
                    {
                        if (item is PipeCodesGroupViewModel g)
                            g.PropertyChanged -= OnGroupPropertyChanged;
                    }
                }
                if (e.NewItems != null)
                {
                    foreach (object? item in e.NewItems)
                    {
                        if (item is PipeCodesGroupViewModel g)
                            g.PropertyChanged += OnGroupPropertyChanged;
                    }
                }
                RecomputeAggregates();
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasGroups));
            };
        }

        public bool IsEmpty => Groups.Count == 0;

        public bool HasGroups => Groups.Count > 0;

        public TigrePipeStatus Status { get; }

        /// <summary>Texto curto exibido no cabeçalho (ex.: "Sem correspondência").</summary>
        public string Title { get; }

        /// <summary>Texto explicativo logo abaixo do título.</summary>
        public string Description { get; }

        public ObservableCollection<PipeCodesGroupViewModel> Groups { get; } = new();

        private int _totalPipes;
        public int TotalPipes
        {
            get => _totalPipes;
            private set
            {
                if (SetField(ref _totalPipes, value))
                    OnPropertyChanged(nameof(HeaderCountText));
            }
        }

        public string HeaderCountText => Groups.Count switch
        {
            0 => "—",
            1 => $"1 tipo · {TotalPipes} tubo(s)",
            _ => $"{Groups.Count} tipos · {TotalPipes} tubo(s)",
        };

        private bool _suppressBulkUpdate;

        private bool? _isAllSelected = false;
        /// <summary>
        /// Estado tri-state do "Selecionar todos" do cabeçalho. <c>null</c>
        /// significa indeterminado (parcial).
        /// </summary>
        public bool? IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                // WPF cycla checked → null (indeterminate) → false em
                // checkboxes três-state. Coergimos null → false aqui pra
                // que clicar em "todos marcados" desmarque tudo num único
                // clique, em vez de passar pelo estado intermediário.
                bool? coerced = value ?? false;

                if (!SetField(ref _isAllSelected, coerced))
                    return;

                if (_suppressBulkUpdate)
                    return;

                _suppressBulkUpdate = true;
                foreach (PipeCodesGroupViewModel g in Groups)
                    g.IsSelected = coerced.Value;
                _suppressBulkUpdate = false;
                RecomputeAggregates();
            }
        }

        public int SelectedCount
        {
            get
            {
                int n = 0;
                foreach (PipeCodesGroupViewModel g in Groups)
                {
                    if (g.IsSelected)
                        n += g.Count;
                }
                return n;
            }
        }

        private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PipeCodesGroupViewModel.IsSelected))
                return;

            if (_suppressBulkUpdate)
                return;

            RecomputeAggregates();
        }

        private void RecomputeAggregates()
        {
            int total = 0;
            int selectedGroups = 0;
            foreach (PipeCodesGroupViewModel g in Groups)
            {
                total += g.Count;
                if (g.IsSelected)
                    selectedGroups++;
            }
            TotalPipes = total;
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HeaderCountText));

            bool? newState;
            if (Groups.Count == 0)
                newState = false;
            else if (selectedGroups == 0)
                newState = false;
            else if (selectedGroups == Groups.Count)
                newState = true;
            else
                newState = null;

            // Atualiza sem disparar o setter público (evita re-aplicar a
            // marcação em massa nos filhos e o eco infinito).
            _suppressBulkUpdate = true;
            if (_isAllSelected != newState)
            {
                _isAllSelected = newState;
                OnPropertyChanged(nameof(IsAllSelected));
            }
            _suppressBulkUpdate = false;
        }
    }
}
