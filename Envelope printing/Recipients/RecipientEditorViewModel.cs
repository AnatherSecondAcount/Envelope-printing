using EnvelopePrinter.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace Envelope_printing
{
    public class RecipientEditorViewModel : INotifyPropertyChanged
    {
        #region Поля и сервисы

        private readonly DataService _dataService;
        private ObservableCollection<RecipientViewModel> _recipientsCollection;
        private RecipientViewModel _selectedRecipient;

        // Поля для состояния поиска
        private string _searchText;
        private bool _isSearchVisible;
        private string _matchInfo;
        private List<RecipientViewModel> _currentMatches = new List<RecipientViewModel>();
        private int _currentMatchIndex = -1;

        #endregion

        #region События для связи с View (UI)

        public event Func<EditRecipientViewModel, bool?> ShowEditDialogRequested;
        public event Func<string> RequestExportExcelPath;
        public event Func<string> RequestImportExcelPath;
        public event Func<string> RequestBackupPath;
        public event Func<string> RequestRestorePath;
        public event Action<RecipientViewModel> ScrollToRecipientRequested;

        #endregion

        #region Свойства для привязки к XAML

        public ObservableCollection<RecipientViewModel> RecipientsCollection
        {
            get => _recipientsCollection;
            set { _recipientsCollection = value; OnPropertyChanged(); }
        }
        public RecipientViewModel SelectedRecipient
        {
            get => _selectedRecipient;
            set
            {
                _selectedRecipient = value;
                OnPropertyChanged();
                // Уведомляем команды, что их доступность могла измениться
                (MoveUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MoveDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
        public bool IsSearchVisible
        {
            get => _isSearchVisible;
            set { _isSearchVisible = value; OnPropertyChanged(); }
        }
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                PerformSearch(); // Запускаем поиск при каждом изменении текста
            }
        }
        public string MatchInfo
        {
            get => _matchInfo;
            set { _matchInfo = value; OnPropertyChanged(); }
        }

        #endregion

        #region Команды

        // Основные команды (CRUD)
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        // Команды из меню "Файл"
        public ICommand ExportToExcelCommand { get; }
        public ICommand ImportFromExcelCommand { get; }
        public ICommand BackupCommand { get; }
        public ICommand RestoreCommand { get; }

        // Команды поиска
        public ICommand ShowSearchCommand { get; }
        public ICommand CloseSearchCommand { get; }
        public ICommand NextMatchCommand { get; }
        public ICommand PreviousMatchCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand SelectPreviousRowCommand { get; }
        public ICommand SelectNextRowCommand { get; }

        #endregion

        public RecipientEditorViewModel()
        {
            _dataService = new DataService();
            LoadData();

            // Инициализация команд
            AddCommand = new RelayCommand(AddRecipient);
            EditCommand = new RelayCommand(EditRecipient, CanEditOrDeleteRecipient);
            DeleteCommand = new RelayCommand(DeleteRecipient, CanEditOrDeleteRecipient);

            ExportToExcelCommand = new RelayCommand(ExportToExcel);
            ImportFromExcelCommand = new RelayCommand(ImportFromExcel);
            BackupCommand = new RelayCommand(BackupDatabase);
            RestoreCommand = new RelayCommand(RestoreDatabase);

            ShowSearchCommand = new RelayCommand(p => IsSearchVisible = true);
            CloseSearchCommand = new RelayCommand(CloseSearch);
            NextMatchCommand = new RelayCommand(NavigateToNextMatch, p => _currentMatches.Any());
            PreviousMatchCommand = new RelayCommand(NavigateToPreviousMatch, p => _currentMatches.Any());
            MoveUpCommand = new RelayCommand(MoveUp, _ => CanMoveUp());
            MoveDownCommand = new RelayCommand(MoveDown, _ => CanMoveDown());
            SelectPreviousRowCommand = new RelayCommand(_ => SelectRelative(-1), _ => RecipientsCollection != null && RecipientsCollection.Any());
            SelectNextRowCommand = new RelayCommand(_ => SelectRelative(1), _ => RecipientsCollection != null && RecipientsCollection.Any());
        }

        private void LoadData()
        {
            var recipientsInDb = _dataService.GetAllRecipients();
            // Превращаем каждую модель Recipient в ее ViewModel-обертку RecipientViewModel
            var viewModels = recipientsInDb.Select(model => new RecipientViewModel(model));
            RecipientsCollection = new ObservableCollection<RecipientViewModel>(viewModels);
        }

        private bool CanEditOrDeleteRecipient(object obj)
        {
            return SelectedRecipient != null;
        }

        #region Логика основных команд (CRUD)

        private void AddRecipient(object obj)
        {
            var newRecipientModel = new Recipient(); // Создаем пустую модель данных
            var vm = new EditRecipientViewModel(newRecipientModel, "Добавление нового получателя");

            bool? result = ShowEditDialogRequested?.Invoke(vm);

            if (result == true)
            {
                _dataService.AddRecipient(newRecipientModel);
                // В коллекцию добавляем не саму модель, а ее ViewModel-обертку
                RecipientsCollection.Add(new RecipientViewModel(newRecipientModel));
            }
        }

        private void EditRecipient(object obj)
        {
            // Создаем копию модели данных, чтобы не изменять оригинал до нажатия "OK"
            var recipientCopy = new Recipient
            {
                Id = SelectedRecipient.Model.Id,
                OrganizationName = SelectedRecipient.Model.OrganizationName,
                AddressLine1 = SelectedRecipient.Model.AddressLine1,
                PostalCode = SelectedRecipient.Model.PostalCode,
                City = SelectedRecipient.Model.City,
                Region = SelectedRecipient.Model.Region,
                Country = SelectedRecipient.Model.Country
            };

            var vm = new EditRecipientViewModel(recipientCopy, "Редактирование получателя");
            bool? result = ShowEditDialogRequested?.Invoke(vm);

            if (result == true)
            {
                _dataService.UpdateRecipient(recipientCopy);

                // Обновляем данные в оригинальной модели.
                // Так как RecipientViewModel пробрасывает свойства из Model и реализует INotifyPropertyChanged,
                // то UI не обновится. Нужно обновить саму модель и вызвать OnPropertyChanged для свойств в RecipientViewModel.
                // Проще всего перезагрузить данные.
                LoadData();
            }
        }

        private void DeleteRecipient(object obj)
        {
            var result = MessageBox.Show($"Вы уверены, что хотите удалить запись '{SelectedRecipient.OrganizationName}'?",
                                         "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                // Передаем в сервис саму модель данных
                _dataService.DeleteRecipient(SelectedRecipient.Model);
                // А из коллекции удаляем ViewModel-обертку
                RecipientsCollection.Remove(SelectedRecipient);
            }
        }

        // Навигация по выбору (без перемещения в списке)
        private void SelectRelative(int delta)
        {
            if (RecipientsCollection == null || RecipientsCollection.Count == 0) return;
            int current = SelectedRecipient != null ? RecipientsCollection.IndexOf(SelectedRecipient) : -1;
            int next = current + delta;
            if (next < 0) next = 0;
            if (next >= RecipientsCollection.Count) next = RecipientsCollection.Count - 1;
            SelectedRecipient = RecipientsCollection[next];
            ScrollToRecipientRequested?.Invoke(SelectedRecipient);
        }

        #endregion

        #region Логика команд Файл-Меню

        private void ExportToExcel(object obj)
        {
            string destinationPath = RequestExportExcelPath?.Invoke();
            if (string.IsNullOrEmpty(destinationPath)) return;
            try
            {
                _dataService.ExportToExcel(destinationPath);
                MessageBox.Show("Данные успешно экспортированы!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportFromExcel(object obj)
        {
            string sourcePath = RequestImportExcelPath?.Invoke();
            if (string.IsNullOrEmpty(sourcePath)) return;
            var result = MessageBox.Show("Добавить данные из файла Excel в базу?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                _dataService.ImportFromExcel(sourcePath);
                MessageBox.Show("Данные успешно импортированы!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при импорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackupDatabase(object obj)
        {
            string destinationPath = RequestBackupPath?.Invoke();
            if (string.IsNullOrEmpty(destinationPath)) return;
            try
            {
                _dataService.BackupDatabase(destinationPath);
                MessageBox.Show("Резервная копия успешно создана!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании копии: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreDatabase(object obj)
        {
            string sourcePath = RequestRestorePath?.Invoke();
            if (string.IsNullOrEmpty(sourcePath)) return;
            var result = MessageBox.Show("ВНИМАНИЕ! Текущая база будет заменена.\nПродолжить восстановление?",
                                         "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                _dataService.RestoreDatabase(sourcePath);
                MessageBox.Show("База данных успешно восстановлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при восстановлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Логика Поиска

        private void PerformSearch()
        {
            // Сначала сбрасываем подсветку у всех
            foreach (var recipient in RecipientsCollection) recipient.IsMatch = false;
            _currentMatches.Clear();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                UpdateMatchInfo();
                return;
            }

            var searchTextLower = SearchText.ToLower();
            _currentMatches = RecipientsCollection.Where(vm =>
                (vm.OrganizationName?.ToLower() ?? "").Contains(searchTextLower) ||
                (vm.AddressLine1?.ToLower() ?? "").Contains(searchTextLower) ||
                (vm.PostalCode?.ToLower() ?? "").Contains(searchTextLower) ||
                (vm.City?.ToLower() ?? "").Contains(searchTextLower) ||
                (vm.Region?.ToLower() ?? "").Contains(searchTextLower) ||
                (vm.Country?.ToLower() ?? "").Contains(searchTextLower)
            ).ToList();

            foreach (var match in _currentMatches) match.IsMatch = true;

            _currentMatchIndex = _currentMatches.Any() ? 0 : -1;
            if (_currentMatchIndex == 0) SelectAndScrollToCurrentMatch();

            UpdateMatchInfo();
        }

        private void NavigateToNextMatch(object obj)
        {
            if (!_currentMatches.Any()) return;
            _currentMatchIndex = (_currentMatchIndex + 1) % _currentMatches.Count;
            SelectAndScrollToCurrentMatch();
            UpdateMatchInfo();
        }

        private void NavigateToPreviousMatch(object obj)
        {
            if (!_currentMatches.Any()) return;
            _currentMatchIndex = (_currentMatchIndex - 1 + _currentMatches.Count) % _currentMatches.Count;
            SelectAndScrollToCurrentMatch();
            UpdateMatchInfo();
        }

        private void SelectAndScrollToCurrentMatch()
        {
            SelectedRecipient = _currentMatches[_currentMatchIndex];
            ScrollToRecipientRequested?.Invoke(SelectedRecipient);
        }

        private void CloseSearch(object obj)
        {
            IsSearchVisible = false;
            SearchText = ""; // Присвоение пустой строки автоматически вызовет PerformSearch() и сбросит результаты
        }

        private void UpdateMatchInfo()
        {
            if (!_currentMatches.Any())
            {
                MatchInfo = string.IsNullOrWhiteSpace(SearchText) ? "" : "Нет совпадений";
            }
            else
            {
                MatchInfo = $"{_currentMatchIndex + 1} из {_currentMatches.Count}";
            }
        }

        #endregion

        #region Помощники для перестановки в списке

        private bool CanMoveUp()
        {
            if (SelectedRecipient == null || RecipientsCollection == null) return false;
            int currentIndex = RecipientsCollection.IndexOf(SelectedRecipient);
            return currentIndex > 0; // Можно двигаться вверх, если это не первый элемент
        }

        private void MoveUp(object obj)
        {
            if (!CanMoveUp()) return;
            int currentIndex = RecipientsCollection.IndexOf(SelectedRecipient);
            RecipientsCollection.Move(currentIndex, currentIndex - 1);
        }

        private bool CanMoveDown()
        {
            if (SelectedRecipient == null || RecipientsCollection == null) return false;
            int currentIndex = RecipientsCollection.IndexOf(SelectedRecipient);
            return currentIndex < RecipientsCollection.Count - 1; // Можно двигаться вниз, если это не последний
        }

        private void MoveDown(object obj)
        {
            if (!CanMoveDown()) return;
            int currentIndex = RecipientsCollection.IndexOf(SelectedRecipient);
            RecipientsCollection.Move(currentIndex, currentIndex + 1);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}