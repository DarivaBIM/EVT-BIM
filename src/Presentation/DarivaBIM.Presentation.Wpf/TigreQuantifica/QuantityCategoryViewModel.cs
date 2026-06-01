using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DarivaBIM.Application.DTOs.Quantifica;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.TigreQuantifica
{
    /// <summary>
    /// Façade de uma categoria — agrupa todos os grupos da mesma categoria
    /// (ex.: "Tubulações", "Conexões de tubulação"). Identidade (Category,
    /// MeasurementKind, Groups) é imutável após o ApplyScan; coleções viram
    /// <c>ReadOnlyCollection</c> pra deixar isso explícito ao bindar XAML.
    /// Só o estado de UI <see cref="IsExpanded"/> é mutável — bindado em
    /// two-way com o <c>Expander</c> que envelopa cada card (Slice 4.2 F5).
    /// </summary>
    public sealed class QuantityCategoryViewModel : ObservableObject
    {
        private bool _isExpanded = true;

        public QuantityCategoryViewModel(
            string category,
            MeasurementKind measurementKind,
            IEnumerable<QuantityGroupViewModel> groups)
        {
            Category = category;
            MeasurementKind = measurementKind;
            Groups = new ReadOnlyCollection<QuantityGroupViewModel>(groups.ToList());
        }

        public string Category { get; }

        public MeasurementKind MeasurementKind { get; }

        public string Unit => MeasurementKind.ToUnitLabel();

        public IReadOnlyList<QuantityGroupViewModel> Groups { get; }

        public int GroupCount => Groups.Count;

        /// <summary>
        /// <c>true</c> se a categoria é <c>OST_PipeCurves</c> (alguma linha
        /// dela). Slice 1.6 F1 lê pra decidir se o banner amarelo aparece.
        /// </summary>
        public bool HasPipeCurvesCategory => Groups.Any(g => g.IsPipeCurvesCategory);

        /// <summary>
        /// Estado de expansão do card no XAML. Default <c>true</c> pra
        /// preservar comportamento anterior — todas categorias abertas no
        /// primeiro render. Setter público pra two-way binding com
        /// <c>Expander.IsExpanded</c>.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetField(ref _isExpanded, value);
        }
    }
}
