using System.Linq;
using System.Windows.Data;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Presentation.Wpf.PipeCodes;
using Xunit;

namespace DarivaBIM.Core.Tests.Presentation.Wpf.PipeCodes
{
    /// <summary>
    /// Slice 3.5 — valida o subgroup por CategoryName que alimenta o
    /// XAML via <see cref="PipeCodesSectionViewModel.GroupedGroups"/>.
    /// </summary>
    public class PipeCodesSectionViewModelTests
    {
        [Fact]
        public void GroupedGroups_is_configured_with_CategoryName_group_description()
        {
            PipeCodesSectionViewModel section = new(
                TigrePipeStatus.Missing, "Title", "Description");

            Assert.NotNull(section.GroupedGroups);
            Assert.Single(section.GroupedGroups.GroupDescriptions);

            PropertyGroupDescription pgd =
                (PropertyGroupDescription)section.GroupedGroups.GroupDescriptions[0];
            Assert.Equal(
                nameof(PipeCodesGroupViewModel.CategoryName),
                pgd.PropertyName);
        }

        [Fact]
        public void GroupedGroups_separates_items_by_category_name()
        {
            PipeCodesSectionViewModel section = new(
                TigrePipeStatus.Missing, "Title", "Description");

            section.Groups.Add(new PipeCodesGroupViewModel(
                categoryName: "Tubulações",
                familyName: "Tubo Soldável 25mm",
                typeName: "Tubo Soldável 25mm",
                diameterMm: 25,
                count: 3,
                status: TigrePipeStatus.Missing,
                elementIds: new long[] { 1, 2, 3 },
                matchedCode: 10120250));

            section.Groups.Add(new PipeCodesGroupViewModel(
                categoryName: "Conexões de tubo",
                familyName: "Tigre - Joelho 90 Soldável",
                typeName: "JL90-25",
                diameterMm: 25,
                count: 2,
                status: TigrePipeStatus.Missing,
                elementIds: new long[] { 4, 5 },
                matchedCode: 22150251));

            section.Groups.Add(new PipeCodesGroupViewModel(
                categoryName: "Tubulações",
                familyName: "Tubo Série Normal",
                typeName: "Tubo Série Normal",
                diameterMm: 50,
                count: 1,
                status: TigrePipeStatus.Missing,
                elementIds: new long[] { 6 },
                matchedCode: 11030602));

            var groups = section.GroupedGroups.Groups
                .Cast<CollectionViewGroup>()
                .ToList();

            Assert.Equal(2, groups.Count);
            Assert.Contains(groups, g => (string)g.Name == "Tubulações");
            Assert.Contains(groups, g => (string)g.Name == "Conexões de tubo");

            CollectionViewGroup tubulacoes = groups.Single(g => (string)g.Name == "Tubulações");
            Assert.Equal(2, tubulacoes.ItemCount);

            CollectionViewGroup conexoes = groups.Single(g => (string)g.Name == "Conexões de tubo");
            Assert.Equal(1, conexoes.ItemCount);
        }
    }
}
