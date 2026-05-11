using System.Collections.ObjectModel;
using DarivaBIM.Presentation.Wpf.Common;
using DarivaBIM.Presentation.Wpf.Models;

namespace DarivaBIM.Presentation.Wpf.FloorDrainExtension
{
    /// <summary>
    /// Grupo de caixas sifonadas/secas de um mesmo tipo (Família + Symbol)
    /// detectadas no projeto ativo, com a lista de tipos de tubo compatíveis
    /// que o usuário pode escolher como prolongador para esse tipo de caixa.
    /// IDs são neutros (long) — a conversão para ElementId ocorre na camada
    /// de adapters do Revit.
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
    }
}
