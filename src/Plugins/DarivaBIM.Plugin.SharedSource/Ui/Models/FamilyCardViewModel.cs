using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using DarivaBIM.Application.DTOs.Family;

namespace DarivaBIM.Plugin.Ui.Models
{
    /// <summary>
    /// View-model do card de família na galeria. Empacota a entidade de
    /// domínio (<see cref="FamilyItem"/>) com tudo que o XAML precisa para
    /// renderizar o card sem usar value-converters: badges de sistema já
    /// como brushes, flag IsNew computada por idade do CreatedAt, IsFavorita
    /// vindo do preferences-service, etc.
    ///
    /// Uma instância por família por rebuild de rows. Brushes e BitmapImages
    /// são cacheados (Frozen) por fonte, então a rotatividade não pesa.
    /// </summary>
    public sealed class FamilyCardViewModel : INotifyPropertyChanged
    {
        // CreatedAt mais recente que isso vira badge "NOVO". 30 dias é o
        // padrão de catálogos de família — frequência típica de release
        // de novas famílias da Tigre é mensal.
        private const int NewBadgeDays = 30;

        private bool _isFavorita;

        public FamilyCardViewModel(FamilyItem family, IReadOnlyList<Sistema> sistemas, bool isFavorita)
        {
            Family = family ?? throw new ArgumentNullException(nameof(family));

            // Spec §4.6.1: até 3 bolinhas no rodapé do card. Mais que isso
            // estoura o espaço e compete com o DN/medida à direita.
            SistemaBadges = sistemas
                .Take(3)
                .Select(s => new SistemaBadgeVm(s))
                .ToList();

            IsNew = family.CreatedAt.HasValue &&
                    (DateTime.UtcNow - family.CreatedAt.Value).TotalDays <= NewBadgeDays;

            _isFavorita = isFavorita;
        }

        public FamilyItem Family { get; }
        public IReadOnlyList<SistemaBadgeVm> SistemaBadges { get; }
        public bool IsNew { get; }

        /// <summary>
        /// Flag de favorita do usuário. Two-way bound ao botão de coração no
        /// card. A página é responsável por sincronizar com o
        /// <c>FamilyPreferencesService</c> ao ouvir
        /// <see cref="INotifyPropertyChanged"/>.
        /// </summary>
        public bool IsFavorita
        {
            get => _isFavorita;
            set
            {
                if (_isFavorita == value)
                {
                    return;
                }

                _isFavorita = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Forwards para conveniência de binding no XAML.
        public string Name => Family.Name;
        public string UrlImg => Family.UrlImg;
        public string UpdatedAtLabel => Family.UpdatedAtLabel;
    }

    /// <summary>
    /// View-model de uma "bolinha de sistema" no rodapé do card. Carrega
    /// brushes e ícone resolvidos a partir de um <see cref="Sistema"/>
    /// para que o XAML possa bindar direto sem converters.
    /// </summary>
    public sealed class SistemaBadgeVm
    {
        public SistemaBadgeVm(Sistema sistema)
        {
            Sistema = sistema ?? throw new ArgumentNullException(nameof(sistema));
            Label = sistema.Label;
            BackgroundBrush = CreateFrozenBrush(sistema.BgHex);
            BorderBrush = CreateFrozenBrush(sistema.ColorHex);
            // Vetor: o mesmo DrawingImage congelado é reusado entre todos os
            // badges do mesmo sistema (potencialmente centenas de cards).
            Icon = SistemaIconLoader.Load(sistema.IconFileName);
        }

        public Sistema Sistema { get; }
        public string Label { get; }
        public Brush BackgroundBrush { get; }
        public Brush BorderBrush { get; }
        public ImageSource? Icon { get; }
        public bool HasIcon => Icon != null;

        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            SolidColorBrush brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            brush.Freeze();
            return brush;
        }
    }
}
