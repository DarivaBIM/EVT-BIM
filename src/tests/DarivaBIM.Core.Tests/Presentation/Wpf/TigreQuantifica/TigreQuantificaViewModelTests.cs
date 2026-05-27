using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public void Categories_default_IsExpanded_true_after_ApplyScan()
        {
            TigreQuantificaViewModel vm = new();
            vm.ApplyScan(BuildSnapshotWithPipesAndWalls(pipeWithCode: false));

            Assert.NotEmpty(vm.Categories);
            Assert.All(vm.Categories, cat => Assert.True(cat.IsExpanded));
        }

        [Fact]
        public void QuantityCategory_IsExpanded_raises_PropertyChanged_on_toggle()
        {
            TigreQuantificaViewModel vm = new();
            vm.ApplyScan(BuildSnapshotWithPipesAndWalls(pipeWithCode: false));
            QuantityCategoryViewModel category = vm.Categories.First();

            string? lastProperty = null;
            ((INotifyPropertyChanged)category).PropertyChanged += (_, args) => lastProperty = args.PropertyName;

            category.IsExpanded = false;

            Assert.False(category.IsExpanded);
            Assert.Equal(nameof(QuantityCategoryViewModel.IsExpanded), lastProperty);
        }

        [Fact]
        public void AuditFindingViewModel_SelectInRevitCommand_can_execute_when_ElementIds_present_and_callback_injected()
        {
            QuantityAuditFinding finding = new()
            {
                FamilyType = "Tubulações",
                MissingFields = new[] { "Tigre: Código" },
                Severity = AuditSeverity.Red,
                ElementIds = new long[] { 12345L, 67890L },
                IsTigreCodigoMissing = true,
            };

            List<long>? captured = null;
            Action<IReadOnlyCollection<long>> callback = ids => captured = new List<long>(ids);

            AuditFindingViewModel vm = new(finding, callback, corrigirAgora: null);

            Assert.True(vm.CanSelectInRevit);
            Assert.True(vm.SelectInRevitCommand.CanExecute(null));

            vm.SelectInRevitCommand.Execute(null);

            Assert.NotNull(captured);
            Assert.Equal(new long[] { 12345L, 67890L }, captured!.ToArray());
        }

        [Fact]
        public void AuditFindingViewModel_SelectInRevitCommand_cannot_execute_when_ElementIds_empty()
        {
            // ProjectInfo finding — sem elemento Revit pra selecionar.
            QuantityAuditFinding finding = new()
            {
                FamilyType = "Cliente não preenchido",
                Severity = AuditSeverity.Yellow,
                ElementIds = Array.Empty<long>(),
            };
            AuditFindingViewModel vm = new(finding, ids => { }, corrigirAgora: null);

            Assert.False(vm.CanSelectInRevit);
            Assert.False(vm.SelectInRevitCommand.CanExecute(null));
        }

        [Fact]
        public void AuditFindingViewModel_SelectInRevitCommand_cannot_execute_when_callback_null()
        {
            // Headless construction (no Revit available) — CanSelectInRevit
            // false mesmo com IDs preenchidos.
            QuantityAuditFinding finding = new()
            {
                FamilyType = "Tubulações",
                ElementIds = new long[] { 1L },
            };
            AuditFindingViewModel vm = new(finding);

            Assert.False(vm.CanSelectInRevit);
        }

        [Fact]
        public void AuditFindingViewModel_CorrigirAgoraCommand_only_active_for_TigreCodigoMissing_findings()
        {
            // Finding "Tigre: Código ausente" — habilita.
            QuantityAuditFinding tigreCodeFinding = new()
            {
                FamilyType = "Tubulações",
                IsTigreCodigoMissing = true,
                ElementIds = new long[] { 1L },
            };
            AuditFindingViewModel tigreVm = new(tigreCodeFinding, ids => { }, ids => { });
            Assert.True(tigreVm.CanCorrigirAgora);
            Assert.True(tigreVm.IsTigreCodigoMissing);

            // Finding "Fabricante ausente" — Yellow, mesma estrutura mas
            // IsTigreCodigoMissing=false. NÃO deve habilitar CorrigirAgora.
            QuantityAuditFinding fabricanteFinding = new()
            {
                FamilyType = "Conexões",
                IsTigreCodigoMissing = false,
                ElementIds = new long[] { 1L },
            };
            AuditFindingViewModel fabVm = new(fabricanteFinding, ids => { }, ids => { });
            Assert.False(fabVm.CanCorrigirAgora);
        }

        [Fact]
        public void AuditFindingViewModel_CorrigirAgoraCommand_cannot_execute_without_callback()
        {
            QuantityAuditFinding finding = new()
            {
                FamilyType = "Tubulações",
                IsTigreCodigoMissing = true,
                ElementIds = new long[] { 1L },
            };
            AuditFindingViewModel vm = new(finding);
            Assert.False(vm.CanCorrigirAgora);
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
