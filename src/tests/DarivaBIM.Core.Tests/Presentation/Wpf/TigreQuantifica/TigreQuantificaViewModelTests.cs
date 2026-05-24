using System.Linq;
using DarivaBIM.Application.DTOs.Quantifica;
using DarivaBIM.Presentation.Wpf.TigreQuantifica;
using Xunit;

namespace DarivaBIM.Core.Tests.Presentation.Wpf.TigreQuantifica
{
    public class TigreQuantificaViewModelTests
    {
        [Fact]
        public void ApplyScan_populates_project_info_categories_and_findings()
        {
            QuantitySnapshot snapshot = BuildSnapshotWithPipesAndWalls(pipeWithCode: false);
            TigreQuantificaViewModel vm = new();

            vm.ApplyScan(snapshot);

            Assert.Equal("Obra Teste", vm.ProjectName);
            Assert.Equal(2, vm.Categories.Count);
            Assert.Equal(2, vm.TotalGroups);
            // 1 tubo (3 elementos) + 1 parede (1 elemento) = 4
            Assert.Equal(4, vm.TotalElementCount);
            Assert.Single(vm.Findings);
        }

        [Fact]
        public void ApplyScan_with_error_clears_categories_and_findings()
        {
            TigreQuantificaViewModel vm = new();
            vm.ApplyScan(BuildSnapshotWithPipesAndWalls(pipeWithCode: false));
            Assert.NotEmpty(vm.Categories);

            QuantitySnapshot errorSnapshot = new()
            {
                ErrorMessage = "Abra um projeto Revit (.rvt) para usar esta ferramenta.",
            };

            vm.ApplyScan(errorSnapshot);

            Assert.Empty(vm.Categories);
            Assert.Empty(vm.Findings);
            Assert.Equal(0, vm.TotalGroups);
            Assert.True(vm.HasErrorMessage);
            Assert.Equal("Abra um projeto Revit (.rvt) para usar esta ferramenta.", vm.ErrorMessage);
        }

        [Fact]
        public void PipesNeedCoding_is_true_when_all_pipe_groups_have_null_tigre_code()
        {
            TigreQuantificaViewModel vm = new();
            vm.ApplyScan(BuildSnapshotWithPipesAndWalls(pipeWithCode: false));

            Assert.True(vm.PipesNeedCoding);
        }

        [Fact]
        public void PipesNeedCoding_is_false_when_at_least_one_pipe_has_code()
        {
            TigreQuantificaViewModel vm = new();
            vm.ApplyScan(BuildSnapshotWithPipesAndWalls(pipeWithCode: true));

            Assert.False(vm.PipesNeedCoding);
        }

        [Fact]
        public void PipesNeedCoding_is_false_when_no_pipe_curves_category_in_snapshot()
        {
            QuantitySnapshot snapshot = new()
            {
                ProjectInfo = new ProjectInfoDto { Name = "Sem tubos" },
                Groups = new[]
                {
                    new QuantityGroup
                    {
                        Category = "Paredes",
                        Family = "Parede Básica",
                        Type = "200mm",
                        MeasurementKind = MeasurementKind.AreaSquareMeters,
                        ElementCount = 2,
                        Quantity = 30m,
                        IsPipeCurvesCategory = false,
                    },
                },
            };

            TigreQuantificaViewModel vm = new();
            vm.ApplyScan(snapshot);

            Assert.False(vm.PipesNeedCoding);
        }

        [Fact]
        public void ApplyScan_aggregates_red_and_yellow_finding_counts()
        {
            QuantitySnapshot snapshot = new()
            {
                AuditFindings = new[]
                {
                    new QuantityAuditFinding { Severity = AuditSeverity.Red,    FamilyType = "X" },
                    new QuantityAuditFinding { Severity = AuditSeverity.Red,    FamilyType = "Y" },
                    new QuantityAuditFinding { Severity = AuditSeverity.Yellow, FamilyType = "Z" },
                },
            };

            TigreQuantificaViewModel vm = new();
            vm.ApplyScan(snapshot);

            Assert.Equal(2, vm.RedFindingsCount);
            Assert.Equal(1, vm.YellowFindingsCount);
            Assert.Equal(3, vm.TotalFindingsCount);
        }

        private static QuantitySnapshot BuildSnapshotWithPipesAndWalls(bool pipeWithCode)
        {
            return new QuantitySnapshot
            {
                ProjectInfo = new ProjectInfoDto { Name = "Obra Teste" },
                Groups = new[]
                {
                    new QuantityGroup
                    {
                        Category = "Tubulações",
                        Family = "Tubo Soldável",
                        Type = "PVC 25mm",
                        Diameter = "25 mm",
                        TigreCode = pipeWithCode ? "47013" : null,
                        MeasurementKind = MeasurementKind.LengthMeters,
                        ElementCount = 3,
                        Quantity = 9.5m,
                        IsPipeCurvesCategory = true,
                    },
                    new QuantityGroup
                    {
                        Category = "Paredes",
                        Family = "Parede Básica",
                        Type = "200mm",
                        MeasurementKind = MeasurementKind.AreaSquareMeters,
                        ElementCount = 1,
                        Quantity = 12.3m,
                        IsPipeCurvesCategory = false,
                    },
                },
                AuditFindings = new[]
                {
                    new QuantityAuditFinding
                    {
                        FamilyType = "Tubulações",
                        MissingFields = new[] { "Tigre: Código" },
                        Severity = AuditSeverity.Red,
                    },
                },
            };
        }
    }
}
