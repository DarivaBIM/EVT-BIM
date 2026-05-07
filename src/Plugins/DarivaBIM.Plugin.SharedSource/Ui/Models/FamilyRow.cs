using System.Collections.Generic;
using DarivaBIM.Plugin.Ui.Models;

namespace DarivaBIM.Plugin.Ui
{
    /// <summary>
    /// Linha do grid virtualizado da galeria de famílias. Cada linha agrupa
    /// até <c>ComputeItemsPerRow</c> cartões; a virtualização da
    /// <c>ItemsControl</c> opera por linha, não por cartão.
    /// </summary>
    public sealed class FamilyRow
    {
        public FamilyRow(IReadOnlyList<FamilyCardViewModel> cards)
        {
            Cards = cards;
        }

        public IReadOnlyList<FamilyCardViewModel> Cards { get; }
    }
}
