using System;
using System.Collections.Generic;
using System.Globalization;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.PipeCodes
{
    /// <summary>
    /// Linha de tubos agrupada por (Família, Tipo, Diâmetro, Status). Aparece
    /// dentro de uma das quatro caixinhas coloridas da janela. O usuário marca
    /// <see cref="IsSelected"/> e os botões "Inserir/Atualizar" e "Deletar"
    /// usam essa marcação para decidir o que fazer.
    /// </summary>
    public sealed class PipeCodesGroupViewModel : ObservableObject
    {
        public PipeCodesGroupViewModel(
            string categoryName,
            string familyName,
            string typeName,
            int? diameterMm,
            int count,
            TigrePipeStatus status,
            IReadOnlyList<long> elementIds,
            int? matchedCode)
        {
            CategoryName = categoryName ?? string.Empty;
            FamilyName = familyName ?? string.Empty;
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

        /// <summary>
        /// Nome da Family Revit. Pra Pipes (system family) é igual ao
        /// TypeName; pra FamilyInstance (fittings/accessories/fixtures) é
        /// o nome da família carregada. Slice 4.1 — usado pelo XAML pra
        /// renderizar "familia · tipo" na coluna ELEMENTO.
        /// </summary>
        public string FamilyName { get; }

        public string TypeName { get; }

        public int? DiameterMm { get; }

        public int Count { get; }

        public TigrePipeStatus Status { get; }

        public IReadOnlyList<long> ElementIds { get; }

        public int? MatchedCode { get; }

        public string DiameterText => DiameterMm.HasValue
            ? $"{DiameterMm.Value} mm"
            : "—";

        /// <summary>
        /// Texto pra coluna ELEMENTO no formato "familia · tipo".
        /// Quando FamilyName == TypeName (caso típico de Pipes, system
        /// family) ou FamilyName é vazio, colapsa pra só TypeName pra evitar
        /// repetição visual "Soldável 25 · Soldável 25". Pra fittings que
        /// têm Family e Type distintos rende "Joelho 90 Soldável · JL90-25".
        /// </summary>
        public string ElementText
        {
            get
            {
                if (string.IsNullOrEmpty(FamilyName))
                    return TypeName;
                if (string.Equals(FamilyName, TypeName, StringComparison.Ordinal))
                    return TypeName;
                return $"{FamilyName} · {TypeName}";
            }
        }

        /// <summary>
        /// Texto da coluna CÓD. SUGERIDO. Mostra o <see cref="MatchedCode"/>
        /// formatado pra exibição; quando o grupo está em
        /// <see cref="TigrePipeStatus.NoMatch"/> (catálogo não casou),
        /// volta "—" pra alinhar com o DiameterText vazio.
        /// </summary>
        public string MatchedCodeText => MatchedCode.HasValue
            ? MatchedCode.Value.ToString(CultureInfo.InvariantCulture)
            : "—";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }
    }
}
