using System.ComponentModel;

namespace DarivaBIM.Presentation.Wpf.ParameterEditor
{
    public class DisciplineFilterViewModel : INotifyPropertyChanged
    {
        public DisciplineFilterViewModel(ParameterDiscipline discipline, string displayName, bool isChecked)
        {
            Discipline = discipline;
            DisplayName = displayName;
            _isChecked = isChecked;
        }

        public ParameterDiscipline Discipline { get; }
        public string DisplayName { get; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                    return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
