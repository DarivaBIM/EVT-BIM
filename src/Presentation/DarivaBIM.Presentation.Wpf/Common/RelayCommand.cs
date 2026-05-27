using System;
using System.Windows.Input;

namespace DarivaBIM.Presentation.Wpf.Common
{
    /// <summary>
    /// ICommand minimal sem dependência externa (MVVM Toolkit/Prism etc) —
    /// alinha com a regra "sem DI container, sem ext libs em
    /// Presentation.Wpf" do plugin. Suporta versões parametrizadas (
    /// <see cref="RelayCommand{T}"/>) e sem parâmetro (esta classe).
    ///
    /// CanExecuteChanged é encaminhado pro CommandManager do WPF — isso
    /// reavalia CanExecute em todo focus/input change, sem precisar
    /// chamar Raise explicitamente em ViewModel observavel.
    /// </summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    /// <summary>
    /// Versão parametrizada — útil quando o command precisa do payload
    /// do CommandParameter binding (ex.: tag de severidade, id, etc).
    /// </summary>
    public sealed class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

        public void Execute(object? parameter) => _execute((T?)parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
