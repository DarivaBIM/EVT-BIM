using System.Collections.Generic;
using System.Collections.ObjectModel;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.TigreQuantifica
{
    /// <summary>
    /// Slice 4.3.B F3 — wrapper de filtro entre a janela XAML e
    /// <see cref="QuantityCategoryViewModel"/>. Não mexemos no
    /// <c>QuantityCategoryViewModel</c> diretamente (lock do agente
    /// slice-45 — refactor da tabela em paralelo); em vez disso o
    /// <see cref="TigreQuantificaViewModel"/> mantém uma coleção paralela
    /// de <see cref="CategoryDisplayViewModel"/> que reflete a busca
    /// substring por <c>SearchText</c> em cada grupo.
    ///
    /// <para>
    /// Cada instância carrega:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Referência à categoria fonte (pra expor Category, Unit, GroupCount, etc).</description></item>
    /// <item><description><c>FilteredGroups</c> — projeção dos grupos da categoria que casaram o filtro.</description></item>
    /// <item><description><c>IsVisible</c> — false quando o filtro deixou a categoria sem grupos.</description></item>
    /// </list>
    /// <para>
    /// <see cref="QuantityCategoryViewModel.IsExpanded"/> é exposto via
    /// passthrough <see cref="IsExpanded"/> pra preservar o estado de
    /// expansão entre re-filtros (slice 4.2 F5).
    /// </para>
    /// </summary>
    public sealed class CategoryDisplayViewModel : ObservableObject
    {
        private readonly QuantityCategoryViewModel _source;
        private bool _isVisible = true;

        public CategoryDisplayViewModel(
            QuantityCategoryViewModel source,
            IEnumerable<QuantityGroupViewModel> filteredGroups)
        {
            _source = source;
            Groups = new ObservableCollection<QuantityGroupViewModel>(filteredGroups);
        }

        public QuantityCategoryViewModel Source => _source;

        // ---------------- Passthroughs ----------------

        public string Category => _source.Category;
        public string Unit => _source.Unit;

        /// <summary>
        /// Contagem dos grupos VISÍVEIS após o filtro — distinta de
        /// <c>Source.GroupCount</c> (que conta todos). XAML usa isso pra
        /// "N grupos" do header refletir o filtro corrente.
        /// </summary>
        public int GroupCount => Groups.Count;

        public bool IsExpanded
        {
            get => _source.IsExpanded;
            set => _source.IsExpanded = value;
        }

        // ---------------- Estado filtrado ----------------

        /// <summary>
        /// Grupos sobreviventes do filtro. Nome propositalmente igual ao
        /// <see cref="QuantityCategoryViewModel.Groups"/> pra o
        /// <c>CategoryCardTemplate</c> continuar funcionando sem mexer
        /// (lock do agente slice-45). Quando <see cref="IsVisible"/> é
        /// false esta coleção pode estar vazia mas o card inteiro fica
        /// Collapsed via Style do ItemContainer no <c>ItemsControl</c>.
        /// </summary>
        public ObservableCollection<QuantityGroupViewModel> Groups { get; }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetField(ref _isVisible, value);
        }
    }
}
