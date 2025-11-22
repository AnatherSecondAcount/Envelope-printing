using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media; // for VisualTreeHelper

namespace Envelope_printing
{
    public partial class RecipientEditorView : UserControl
    {
        public RecipientEditorView()
        {
            InitializeComponent();
            Loaded += RecipientEditorView_Loaded;
            DataContextChanged += RecipientEditorView_DataContextChanged;
        }

        private void RecipientEditorView_Loaded(object sender, RoutedEventArgs e)
        {
            // Do not force focus to grid; keep natural focus flow.
            FocusSearchIfVisible();
        }

        private void FocusSearchIfVisible()
        {
            if (DataContext is RecipientEditorViewModel vm && vm.IsSearchVisible && SearchTextBox != null)
                SearchTextBox.Focus();
        }

        // Intercept Delete key to trigger VM deletion
        private void RecipientsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (DataContext is RecipientEditorViewModel vm && vm.DeleteCommand?.CanExecute(null) == true)
                {
                    vm.DeleteCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void RecipientEditorView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is RecipientEditorViewModel oldVm)
            {
                UnsubscribeVm(oldVm);
            }
            if (e.NewValue is RecipientEditorViewModel vm)
            {
                SubscribeVm(vm);
                // react to search visibility changes
                vm.PropertyChanged += Vm_PropertyChanged;
            }
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RecipientEditorViewModel.IsSearchVisible))
            {
                Dispatcher.BeginInvoke(new Action(FocusSearchIfVisible));
            }
        }

        private void SubscribeVm(RecipientEditorViewModel vm)
        {
            vm.ShowEditDialogRequested += OnShowEditDialogRequested;
            vm.RequestBackupPath += OnRequestBackupPath;
            vm.RequestRestorePath += OnRequestRestorePath;
            vm.RequestExportExcelPath += OnRequestExportExcelPath;
            vm.RequestImportExcelPath += OnRequestImportExcelPath;
            vm.ScrollToRecipientRequested += OnScrollToRecipientRequested;
            vm.EnsureGridFocusRequested += OnEnsureGridFocusRequested;
        }
        private void UnsubscribeVm(RecipientEditorViewModel vm)
        {
            vm.ShowEditDialogRequested -= OnShowEditDialogRequested;
            vm.RequestBackupPath -= OnRequestBackupPath;
            vm.RequestRestorePath -= OnRequestRestorePath;
            vm.RequestExportExcelPath -= OnRequestExportExcelPath;
            vm.RequestImportExcelPath -= OnRequestImportExcelPath;
            vm.ScrollToRecipientRequested -= OnScrollToRecipientRequested;
            vm.EnsureGridFocusRequested -= OnEnsureGridFocusRequested;
            vm.PropertyChanged -= Vm_PropertyChanged;
        }

        // Диалоги добавления/редактирования
        private bool? OnShowEditDialogRequested(EditRecipientViewModel vm)
        {
            var dialog = new EditRecipientView
            {
                DataContext = vm,
                Owner = Window.GetWindow(this)
            };
            return dialog.ShowDialog();
        }

        private void OnEnsureGridFocusRequested()
        {
            // Do not steal focus from search box if search visible
            if (DataContext is RecipientEditorViewModel vm && vm.IsSearchVisible)
                return;
            RecipientsGrid?.Focus();
        }

        // Путь для бэкапа
        private string OnRequestBackupPath()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Сохранить резервную копию как...",
                Filter = "Файлы базы данных (*.db)|*.db",
                FileName = $"Резервная копия базы {DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db"
            };
            return saveFileDialog.ShowDialog() == true ? saveFileDialog.FileName : null;
        }
        // Путь для восстановления
        private string OnRequestRestorePath()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите файл резервной копии",
                Filter = "Файлы базы данных (*.db)|*.db|Все файлы (*.*)|*.*"
            };
            return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
        }
        // Экспорт в Excel
        private string OnRequestExportExcelPath()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Сохранить как...",
                Filter = "Файл Excel (*.xlsx)|*.xlsx",
                FileName = $"Получатели {DateTime.Now:yyyy-MM-dd}.xlsx"
            };
            return saveFileDialog.ShowDialog() == true ? saveFileDialog.FileName : null;
        }
        // Импорт из Excel
        private string OnRequestImportExcelPath()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите файл Excel для импорта",
                Filter = "Файлы Excel (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*"
            };
            return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
        }

        // Прокрутка DataGrid к найденной записи
        private void OnScrollToRecipientRequested(RecipientViewModel recipient)
        {
            if (recipient != null)
            {
                RecipientsGrid.ScrollIntoView(recipient);
                // Select row visually (SelectedRecipient already set in VM)
            }
        }

        // Обеспечиваем выделение строки по правому клику перед открытием контекстного меню
        private void RecipientsGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not DataGrid dataGrid) return;
            var originalSource = e.OriginalSource as DependencyObject;
            var row = FindVisualParent<DataGridRow>(originalSource);
            if (row != null)
            {
                dataGrid.SelectedItem = row.DataContext;
                if (!row.IsFocused) row.Focus();
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject current = child;
            while (current != null && current is not T)
                current = VisualTreeHelper.GetParent(current);
            return current as T;
        }
    }
}