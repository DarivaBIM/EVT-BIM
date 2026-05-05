using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DarivaBIM.Plugin.Ui
{
    /// <summary>
    /// View-model for a single tag chip. Two-way bound to a styled
    /// <c>ToggleButton.IsChecked</c>; <see cref="FamiliesPage"/> subscribes to
    /// <see cref="INotifyPropertyChanged"/> to rerun filtering whenever the
    /// user toggles a chip on or off.
    /// </summary>
    public sealed class TagFilterOption : INotifyPropertyChanged
    {
        private bool _isSelected;

        public TagFilterOption(string description, string key)
        {
            Description = description;
            Key = key;
        }

        public string Description { get; }

        // Normalized description (lowercase, no diacritics) used for matching
        // against family tags. Stored once at construction so filtering doesn't
        // pay normalization cost per family.
        public string Key { get; }

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
    }
}
