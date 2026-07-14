using System;
using System.Windows.Input;

namespace DocMgr.ViewModels.Base
{
    // 非泛型版本 (给不带参数的命令使用)
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }

    // 泛型版本 (给带参数的命令使用，如 SaveLocationCommand<Cabinet>)
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T>? _canExecute;

        public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            if (_canExecute == null)
            {
                return true;
            }

            if (parameter is null)
            {
                return typeof(T).IsClass || Nullable.GetUnderlyingType(typeof(T)) != null
                    ? _canExecute((T)parameter!)
                    : false;
            }

            return parameter is T typed && _canExecute(typed);
        }

        public void Execute(object? parameter)
        {
            if (parameter is null && !(typeof(T).IsClass || Nullable.GetUnderlyingType(typeof(T)) != null))
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            _execute((T)parameter!);
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
