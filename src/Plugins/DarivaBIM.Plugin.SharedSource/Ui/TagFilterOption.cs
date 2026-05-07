using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using DarivaBIM.Plugin.Ui.Models;

namespace DarivaBIM.Plugin.Ui
{
    /// <summary>
    /// View-model de um chip de filtro de sistema. Há exatamente 14 — um por
    /// <see cref="Sistema"/> do catálogo — e a barra de filtros mostra todos
    /// independente de quantas famílias casam com cada categoria, para que o
    /// usuário descubra a paleta de sistemas suportada antes de entender quais
    /// famílias existem.
    ///
    /// Two-way bound a um <c>ToggleButton.IsChecked</c>; <c>FamiliesPage</c>
    /// se inscreve em <see cref="INotifyPropertyChanged"/> e refaz o filtro a
    /// cada toggle.
    /// </summary>
    public sealed class TagFilterOption : INotifyPropertyChanged
    {
        private bool _isSelected;

        public TagFilterOption(Sistema sistema)
        {
            Sistema = sistema ?? throw new ArgumentNullException(nameof(sistema));
            Description = sistema.Label;
            Key = sistema.Id;
            BackgroundBrush = CreateFrozenBrush(sistema.BgHex);
            ForegroundBrush = CreateFrozenBrush(sistema.ColorHex);
            // Vetor: o mesmo DrawingImage congelado é reusado entre todos os
            // chips do mesmo sistema. Sem decode por tamanho — o WPF rasteriza
            // sob demanda no tamanho lógico do <Image> consumidor.
            Icon = SistemaIconLoader.Load(sistema.IconFileName);
        }

        /// <summary>Sistema canônico associado a este chip.</summary>
        public Sistema Sistema { get; }

        /// <summary>Label exibido (e usado como ToolTip / AutomationProperties.Name).</summary>
        public string Description { get; }

        /// <summary>
        /// Id do <see cref="Sistema"/>. É o que entra em <c>_selectedSistemaIds</c>
        /// na FamiliesPage e o que se compara contra <c>family.SistemaIds</c>.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Pastel da categoria, usado como fundo do chip em todos os estados.
        /// A seleção é sinalizada por uma borda colorida em
        /// <see cref="ForegroundBrush"/>, não pela troca de fundo, para que a
        /// identidade visual do chip permaneça legível independentemente do
        /// estado.
        /// </summary>
        public Brush BackgroundBrush { get; }

        /// <summary>Cor de marca da categoria. Stroke do chip checked.</summary>
        public Brush ForegroundBrush { get; }

        // Mantido para compatibilidade com o ControlTemplate em FamiliesPage.xaml,
        // que tem um TextBlock fallback quando Icon está ausente. Os 14
        // sistemas do catálogo têm vetores garantidos, então o glyph nunca
        // renderiza, mas a property precisa existir para o binding não jogar
        // BindingExpression error no Output.
        public string Glyph => string.Empty;

        public ImageSource? Icon { get; }

        public bool HasIcon => Icon != null;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            SolidColorBrush brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            brush.Freeze();
            return brush;
        }
    }
}
