using System.Collections.ObjectModel;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.FloorDrainExtension
{
    /// <summary>
    /// View model da janela "Adicionar Prolongadores". Mantém o comprimento
    /// solicitado, a coleção de grupos por tipo de caixa (cada um com seu
    /// dropdown de PipeType compatível) e mensagens de status. Vive em
    /// Presentation.Wpf — Revit-agnóstico.
    /// </summary>
    public class FloorDrainExtensionViewModel : ObservableObject
    {
        public ObservableCollection<FloorDrainBoxGroupViewModel> BoxGroups { get; } = new();

        private double _lengthMeters = 0.5;
        public double LengthMeters
        {
            get => _lengthMeters;
            set => SetField(ref _lengthMeters, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetField(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsIdle));
                }
            }
        }

        public bool IsIdle => !IsBusy;

        private string _statusMessage = "Carregando tipos de caixas do projeto…";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public bool HasGroups => BoxGroups.Count > 0;

        /// <summary>
        /// O code-behind chama isso depois de substituir o conteúdo de
        /// <see cref="BoxGroups"/> em bloco para que o binding de
        /// <c>HasGroups</c> reflita o novo estado sem esperar outro
        /// <c>PropertyChanged</c>.
        /// </summary>
        public void OnGroupsChanged()
        {
            OnPropertyChanged(nameof(HasGroups));
        }
    }
}
