using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using DarivaBIM.Presentation.Wpf.Common;
using DarivaBIM.Presentation.Wpf.Models;

namespace DarivaBIM.Presentation.Wpf.FloorDrainExtension
{
    /// <summary>
    /// Grupo de caixas sifonadas/secas de um mesmo tipo (Família + Symbol)
    /// detectadas no projeto ativo, com a lista de tipos de tubo compatíveis
    /// que o usuário pode escolher como prolongador para esse tipo de caixa.
    /// Também mantém a lista de instâncias individuais (cada uma com seu
    /// checkbox), permitindo desmarcar caixas específicas sem precisar
    /// excluir o tipo inteiro. IDs são neutros (long) — a conversão para
    /// ElementId ocorre na camada de adapters do Revit.
    /// </summary>
    public class FloorDrainBoxGroupViewModel : ObservableObject
    {
        public FloorDrainBoxGroupViewModel(
            long symbolIdHint,
            string familyName,
            string symbolName,
            double diameterMm)
        {
            SymbolIdHint = symbolIdHint;
            FamilyName = familyName;
            SymbolName = symbolName;
            DiameterMm = diameterMm;

            Instances.CollectionChanged += OnInstancesCollectionChanged;
        }

        /// <summary>
        /// Id (neutro) do <c>FamilySymbol</c> que representa este tipo de
        /// caixa no projeto Revit ativo. Carregado pelo handler de scan e
        /// usado de volta pelo handler de execução para resolver qual
        /// <c>PipeType</c> aplicar em cada caixa.
        /// </summary>
        public long SymbolIdHint { get; }

        public string FamilyName { get; }
        public string SymbolName { get; }
        public double DiameterMm { get; }

        public string DisplayName => string.IsNullOrEmpty(FamilyName)
            ? SymbolName
            : $"{FamilyName} : {SymbolName}";

        public string DiameterLabel => DiameterMm > 0
            ? $"Ø {DiameterMm:0.#} mm"
            : "Ø —";

        public ObservableCollection<PipeTypeOptionViewModel> PipeTypes { get; } = new();

        private PipeTypeOptionViewModel? _selectedPipeType;
        public PipeTypeOptionViewModel? SelectedPipeType
        {
            get => _selectedPipeType;
            set => SetField(ref _selectedPipeType, value);
        }

        public bool HasPipeTypes => PipeTypes.Count > 0;

        /// <summary>
        /// Texto mostrado no lugar do dropdown quando nenhum PipeType do
        /// projeto possui o diâmetro do conector de prolongamento desta caixa.
        /// </summary>
        public string EmptyMessage =>
            "Nenhum tipo de tubo com este diâmetro está disponível neste projeto.";

        /// <summary>Caixas individuais agrupadas neste tipo.</summary>
        public ObservableCollection<FloorDrainBoxInstanceViewModel> Instances { get; } = new();

        /// <summary>Quantidade total de caixas no grupo.</summary>
        public int InstanceCount => Instances.Count;

        /// <summary>Quantas instâncias estão marcadas para receber prolongador.</summary>
        public int SelectedInstanceCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Instances.Count; i++)
                    if (Instances[i].IsSelected) n++;
                return n;
            }
        }

        /// <summary>
        /// Rótulo do contador exibido na linha do grupo (ex.: "3 de 5 caixas").
        /// </summary>
        public string InstanceCountLabel =>
            $"{SelectedInstanceCount} de {InstanceCount} caixa(s)";

        // 3-state: true = todas marcadas, false = nenhuma, null = parcial.
        // Bindável a um CheckBox com IsThreeState=True para o usuário marcar/
        // desmarcar o grupo inteiro de uma vez.
        public bool? IsAllSelected
        {
            get
            {
                int total = Instances.Count;
                if (total == 0) return false;

                int selected = SelectedInstanceCount;
                if (selected == 0) return false;
                if (selected == total) return true;
                return null;
            }
            set
            {
                if (!value.HasValue) return; // estado misto vem só na leitura
                bool target = value.Value;
                _suppressInstanceNotify = true;
                try
                {
                    for (int i = 0; i < Instances.Count; i++)
                        Instances[i].IsSelected = target;
                }
                finally
                {
                    _suppressInstanceNotify = false;
                }
                NotifySelectionCountersChanged();
            }
        }

        public IReadOnlyList<long> GetSelectedInstanceIds()
        {
            List<long> ids = new(Instances.Count);
            for (int i = 0; i < Instances.Count; i++)
            {
                if (Instances[i].IsSelected)
                    ids.Add(Instances[i].ElementId);
            }
            return ids;
        }

        private bool _suppressInstanceNotify;

        private void OnInstancesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (object item in e.OldItems)
                {
                    if (item is FloorDrainBoxInstanceViewModel vm)
                        vm.PropertyChanged -= OnInstancePropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (object item in e.NewItems)
                {
                    if (item is FloorDrainBoxInstanceViewModel vm)
                        vm.PropertyChanged += OnInstancePropertyChanged;
                }
            }
            NotifySelectionCountersChanged();
        }

        private void OnInstancePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressInstanceNotify) return;
            if (e.PropertyName == nameof(FloorDrainBoxInstanceViewModel.IsSelected))
                NotifySelectionCountersChanged();
        }

        private void NotifySelectionCountersChanged()
        {
            OnPropertyChanged(nameof(InstanceCount));
            OnPropertyChanged(nameof(SelectedInstanceCount));
            OnPropertyChanged(nameof(InstanceCountLabel));
            OnPropertyChanged(nameof(IsAllSelected));
        }
    }
}
