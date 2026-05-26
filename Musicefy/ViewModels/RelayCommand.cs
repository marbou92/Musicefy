using System;
using System.Windows.Input;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Standard ICommand implementation for binding UI actions to ViewModel methods.
    /// Supports optional CanExecute guard and parameter passing.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
            : this(o => execute(), canExecute != null ? new Func<object, bool>(o => canExecute()) : null) { }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}
