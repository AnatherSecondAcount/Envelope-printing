using EnvelopePrinter.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Envelope_printing
{
    public class RecipientEditorViewModel : INotifyPropertyChanged
    {
        #region Поля и сервисы

        private readonly DataService _dataService;
        private ObservableCollection<RecipientViewModel> _recipientsCollection;
        private RecipientViewModel _selectedRecipient;
        private ICollectionView _recipientsView; // представление с учётом сортировки/фильтрации

        // Поля для состояния поиска
        private string _searchText;
        private bool _isSearchVisible;
        private string _matchInfo;
        private List<RecipientViewModel> _currentMatches = new();
        private int _currentMatchIndex = -1;

        #endregion

        #region События для связи с View (UI)

        public event Func<EditRecipientViewModel, bool?> ShowEditDialogRequested;
        public event Func<string> RequestExportExcelPath;
        public event Func<string> RequestImportExcelPath;
        public event Func<string> RequestBackupPath;
        public event Func<string> RequestRestorePath;
        public event Action<RecipientViewModel> ScrollToRecipientRequested;
        public event Action EnsureGridFocusRequested;

        #endregion

        #region Свойства для привязки к XAML

        public ObservableCollection<RecipientViewModel> RecipientsCollection
        {
            get => _recipientsCollection;
            set
            {
                _recipientsCollection = value;
                OnPropertyChanged();
                // Обновляем/создаём представление
                _recipientsView = CollectionViewSource.GetDefaultView(_recipientsCollection);
                OnPropertyChanged(nameof(RecipientsView));
            }
        }

        // Представление с текущей сортировкой для корректного перемещения
        public ICollectionView RecipientsView => _recipientsView ??= CollectionViewSource.GetDefaultView(RecipientsCollection);

        public RecipientViewModel SelectedRecipient
        {
            get => _selectedRecipient;
            set
            {
                _selectedRecipient = value;
                OnPropertyChanged();
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
            set { _searchText = value; OnPropertyChanged(); PerformSearch(); }
        }
        public string MatchInfo
        {
            get => _matchInfo;
            set { _matchInfo = value; OnPropertyChanged(); }
        }

        #endregion

        #region Команды

        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ExportToExcelCommand { get; }
        public ICommand ImportFromExcelCommand { get; }
        public ICommand BackupCommand { get; }
        public ICommand RestoreCommand { get; }
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

            AddCommand = new RelayCommand(AddRecipient);
            EditCommand = new RelayCommand(EditRecipient, CanEditOrDeleteRecipient);
            DeleteCommand = new RelayCommand(DeleteRecipient, CanEditOrDeleteRecipient);

            ExportToExcelCommand = new RelayCommand(ExportToExcel);
            ImportFromExcelCommand = new RelayCommand(ImportFromExcel);
            BackupCommand = new RelayCommand(BackupDatabase);
            RestoreCommand = new RelayCommand(RestoreDatabase);

            ShowSearchCommand = new RelayCommand(_ => IsSearchVisible = true);
            CloseSearchCommand = new RelayCommand(CloseSearch);
            NextMatchCommand = new RelayCommand(NavigateToNextMatch, _ => _currentMatches.Any());
            PreviousMatchCommand = new RelayCommand(NavigateToPreviousMatch, _ => _currentMatches.Any());
            MoveUpCommand = new RelayCommand(MoveUp, _ => CanMoveUp());
            MoveDownCommand = new RelayCommand(MoveDown, _ => CanMoveDown());
            SelectPreviousRowCommand = new RelayCommand(_ => SelectRelative(-1), _ => RecipientsCollection != null && RecipientsCollection.Any());
            SelectNextRowCommand = new RelayCommand(_ => SelectRelative(1), _ => RecipientsCollection != null && RecipientsCollection.Any());
        }

        private void LoadData()
        {
            var recipientsInDb = _dataService.GetAllRecipients();
            var viewModels = recipientsInDb.Select(model => new RecipientViewModel(model));
            RecipientsCollection = new ObservableCollection<RecipientViewModel>(viewModels);
            SelectedRecipient = RecipientsCollection.FirstOrDefault();
        }
        private bool CanEditOrDeleteRecipient(object obj) => SelectedRecipient != null;

        #region Логика основных команд (CRUD)

        private void AddRecipient(object obj)
        {
            var newRecipientModel = new Recipient();
            var vm = new EditRecipientViewModel(newRecipientModel, "Добавление нового получателя");
            bool? result = ShowEditDialogRequested?.Invoke(vm);
            if (result == true)
            {
                _dataService.AddRecipient(newRecipientModel);
                RecipientsCollection.Add(new RecipientViewModel(newRecipientModel));
            }
        }

        private void EditRecipient(object obj)
        {
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
                LoadData();
            }
        }

        private void DeleteRecipient(object obj)
        {
            var result = MessageBox.Show($"Вы уверены, что хотите удалить запись '{SelectedRecipient.OrganizationName}'?",
 "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
 if (result == MessageBoxResult.Yes)
 {
 _dataService.DeleteRecipient(SelectedRecipient.Model);
 RecipientsCollection.Remove(SelectedRecipient);
 if (RecipientsCollection.Any())
 {
 SelectedRecipient = RecipientsCollection.First();
 ScrollToRecipientRequested?.Invoke(SelectedRecipient);
 EnsureGridFocusRequested?.Invoke();
 }
 }
 }

 // Selection navigation by relative offset using the current view (with sort)
 private void SelectRelative(int delta)
 {
 if (RecipientsCollection == null || RecipientsCollection.Count ==0) return;
 // работаем по отображаемому представлению
 var view = RecipientsView;
 var current = SelectedRecipient;
 if (current == null)
 {
 // если ничего не выбрано, выбрать первый элемент отображаемого списка
 view.MoveCurrentToFirst();
 SelectedRecipient = view.CurrentItem as RecipientViewModel;
 ScrollToRecipientRequested?.Invoke(SelectedRecipient);
 EnsureGridFocusRequested?.Invoke();
 return;
 }
 view.MoveCurrentTo(current);
 int index = view.CurrentPosition;
 int nextIndex = Math.Clamp(index + delta,0, view.Cast<object>().Count() -1);
 view.MoveCurrentToPosition(nextIndex);
 SelectedRecipient = view.CurrentItem as RecipientViewModel;
 ScrollToRecipientRequested?.Invoke(SelectedRecipient);
 EnsureGridFocusRequested?.Invoke();
 }

 // Поиск
 private void PerformSearch()
 {
 // Сначала сбрасываем подсветку у всех
 foreach (var r in RecipientsCollection) r.IsMatch = false;
 _currentMatches.Clear();

 if (string.IsNullOrWhiteSpace(SearchText)) { UpdateMatchInfo(); return; }

 var s = SearchText.ToLower();
 _currentMatches = RecipientsCollection.Where(vm =>
 (vm.OrganizationName?.ToLower() ?? "").Contains(s) ||
 (vm.AddressLine1?.ToLower() ?? "").Contains(s) ||
 (vm.PostalCode?.ToLower() ?? "").Contains(s) ||
 (vm.City?.ToLower() ?? "").Contains(s) ||
 (vm.Region?.ToLower() ?? "").Contains(s) ||
 (vm.Country?.ToLower() ?? "").Contains(s)
 ).ToList();

 foreach (var m in _currentMatches) m.IsMatch = true;

 _currentMatchIndex = _currentMatches.Any() ?0 : -1;
 if (_currentMatchIndex ==0) SelectAndScrollToCurrentMatch();

 UpdateMatchInfo();
 }
 private void NavigateToNextMatch(object obj)
 { if (!_currentMatches.Any()) return; _currentMatchIndex = (_currentMatchIndex +1) % _currentMatches.Count; SelectAndScrollToCurrentMatch(); UpdateMatchInfo(); }
 private void NavigateToPreviousMatch(object obj)
 { if (!_currentMatches.Any()) return; _currentMatchIndex = (_currentMatchIndex -1 + _currentMatches.Count) % _currentMatches.Count; SelectAndScrollToCurrentMatch(); UpdateMatchInfo(); }
 private void SelectAndScrollToCurrentMatch()
 { SelectedRecipient = _currentMatches[_currentMatchIndex]; ScrollToRecipientRequested?.Invoke(SelectedRecipient); EnsureGridFocusRequested?.Invoke(); }
 private void CloseSearch(object obj) { IsSearchVisible = false; SearchText = string.Empty; }
 private void UpdateMatchInfo()
 { MatchInfo = !_currentMatches.Any() ? (string.IsNullOrWhiteSpace(SearchText) ? "" : "Нет совпадений") : $"{_currentMatchIndex +1} из {_currentMatches.Count}"; }

 // Перестановка с учётом текущего порядка
 private bool CanMoveUp()
 {
 if (SelectedRecipient == null || RecipientsCollection == null) return false;
 var view = RecipientsView; view.MoveCurrentTo(SelectedRecipient); return view.CurrentPosition >0;
 }
 private void MoveUp(object obj)
 {
 var view = RecipientsView; view.MoveCurrentTo(SelectedRecipient); int index = view.CurrentPosition; if (index <=0) return;
 var ordered = view.Cast<RecipientViewModel>().ToList(); var item = ordered[index]; var prev = ordered[index -1];
 int iItem = RecipientsCollection.IndexOf(item); int iPrev = RecipientsCollection.IndexOf(prev);
 if (iItem > iPrev) RecipientsCollection.Move(iItem, iPrev); else RecipientsCollection.Move(iPrev, iItem);
 SelectedRecipient = prev; ScrollToRecipientRequested?.Invoke(SelectedRecipient); EnsureGridFocusRequested?.Invoke();
 }
 private bool CanMoveDown()
 {
 if (SelectedRecipient == null || RecipientsCollection == null) return false;
 var view = RecipientsView; view.MoveCurrentTo(SelectedRecipient); return view.CurrentPosition < view.Cast<object>().Count() -1;
 }
 private void MoveDown(object obj)
 {
 var view = RecipientsView; view.MoveCurrentTo(SelectedRecipient); int index = view.CurrentPosition; if (index >= view.Cast<object>().Count() -1) return;
 var ordered = view.Cast<RecipientViewModel>().ToList(); var item = ordered[index]; var next = ordered[index +1];
 int iItem = RecipientsCollection.IndexOf(item); int iNext = RecipientsCollection.IndexOf(next);
 if (iItem < iNext) RecipientsCollection.Move(iItem, iNext); else RecipientsCollection.Move(iNext, iItem);
 SelectedRecipient = next; ScrollToRecipientRequested?.Invoke(SelectedRecipient); EnsureGridFocusRequested?.Invoke();
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

 #region INotifyPropertyChanged

 public event PropertyChangedEventHandler PropertyChanged;
 protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
 => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
 #endregion
 }
}