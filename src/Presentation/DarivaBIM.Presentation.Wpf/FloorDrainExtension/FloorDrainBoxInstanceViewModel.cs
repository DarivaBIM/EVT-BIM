using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.FloorDrainExtension
{
    /// <summary>
    /// Uma caixa sifonada/seca/ralo individual dentro de um
    /// <see cref="FloorDrainBoxGroupViewModel"/>. Cada instância aparece como
    /// uma linha com checkbox no grupo expandido, permitindo ao usuário
    /// excluir caixas específicas do lote sem desativar o grupo inteiro.
    /// </summary>
    public sealed class FloorDrainBoxInstanceViewModel : ObservableObject
    {
        public FloorDrainBoxInstanceViewModel(long elementId, string displayLabel)
        {
            ElementId = elementId;
            DisplayLabel = displayLabel;
        }

        /// <summary>Id (neutro) do <c>FamilyInstance</c> no Revit.</summary>
        public long ElementId { get; }

        /// <summary>Rótulo mostrado na linha (ex.: "Caixa #1 · ID 123456").</summary>
        public string DisplayLabel { get; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }
    }
}
