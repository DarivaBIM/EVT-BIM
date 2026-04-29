using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DarivaBIM.Presentation.Wpf.Common
{
    /// <summary>
    /// Minimal INotifyPropertyChanged base class. Reusable WPF view models
    /// inherit from this to avoid duplicating the SetField/OnPropertyChanged
    /// boilerplate.
    /// </summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
