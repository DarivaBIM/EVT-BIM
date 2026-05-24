using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DarivaBIM.Application.DTOs.Quantifica;

namespace DarivaBIM.Presentation.Wpf.TigreQuantifica
{
    /// <summary>
    /// Façade de uma categoria — agrupa todos os grupos da mesma categoria
    /// (ex.: "Tubulações", "Conexões de tubulação"). Não muta após o
    /// ApplyScan; coleções viram <c>ReadOnlyCollection</c> pra deixar isso
    /// explícito ao bindar XAML.
    /// </summary>
    public sealed class QuantityCategoryViewModel
    {
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
    }
}
