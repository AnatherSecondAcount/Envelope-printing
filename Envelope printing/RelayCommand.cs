using System.Windows.Input;

namespace Envelope_printing
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        /// Конструктор команды
        /// <param name="execute">Действие, которое нужно выполнить (например, метод AddRecipient).</param>
        /// <param name="canExecute">Условие, при котором действие доступно (например, метод CanEditRecipient). Если null, команда доступна всегда.</param>

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// Событие, которое WPF использует, чтобы перепроверить, доступна ли команда.
        /// Мы "привязываем" его к глобальному менеджеру команд WPF.
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// Метод, который определяет, может ли команда быть выполнена в данный момент.
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        /// Метод, который выполняет основную логику команды.
        public void Execute(object parameter)
        {
            _execute(parameter);
        }
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}