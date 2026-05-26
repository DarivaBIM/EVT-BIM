using System;
using System.Collections.Generic;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.PipeCodes
{
    /// <summary>
    /// Linha de tubos agrupada por (TipoNome, Diâmetro, Status). Aparece
    /// dentro de uma das quatro caixinhas coloridas da janela. O usuário marca
    /// <see cref="IsSelected"/> e os botões "Inserir/Atualizar" e "Deletar"
    /// usam essa marcação para decidir o que fazer.
    /// </summary>
    public sealed class PipeCodesGroupViewModel : ObservableObject
    {
        public PipeCodesGroupViewModel(
            string categoryName,
            string typeName,
            int? diameterMm,
            int count,
            TigrePipeStatus status,
            IReadOnlyList<long> elementIds,
            int? matchedCode)
        {
            CategoryName = categoryName ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            DiameterMm = diameterMm;
            Count = count;
            Status = status;
            ElementIds = elementIds ?? Array.Empty<long>();
            MatchedCode = matchedCode;
        }

        /// <summary>
        /// Categoria Revit do grupo ("Tubulações", "Conexões de tubo",
        /// "Acessórios de tubulação", "Aparelhos hidrossanitários"). Slice
        /// 3 — usada pelo XAML pra subgroup via CollectionViewSource.
        /// </summary>
        public string CategoryName { get; }

        public string TypeName { get; }

        public int? DiameterMm { get; }

        public int Count { get; }

        public TigrePipeStatus Status { get; }

        public IReadOnlyList<long> ElementIds { get; }

        public int? MatchedCode { get; }

        public string DiameterText => DiameterMm.HasValue
            ? $"{DiameterMm.Value} mm"
            : "—";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }
    }
}
