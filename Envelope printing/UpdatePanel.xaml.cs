using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Envelope_printing
{
 public partial class UpdatePanel : UserControl
 {
 public UpdatePanel()
 {
 InitializeComponent();
 CurrentVersionText.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "-";
 }

 private async void CheckButton_Click(object sender, RoutedEventArgs e)
 {
 CheckButton.IsEnabled = false;
 StatusText.Text = "Проверка...";
 DownloadProgress.Visibility = Visibility.Collapsed;
 try
 {
 // Заглушка проверки. Здесь может быть запрос к вашему API с последней версией
 await Task.Delay(600);
 var latestVersion = CurrentVersionText.Text; // эмулируем, что обновлений нет
 StatusText.Text = $"Актуальная версия: {latestVersion}";
 }
 catch (Exception ex)
 {
 StatusText.Text = "Ошибка проверки";
 MessageBox.Show(ex.Message, "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
 }
 finally
 {
 CheckButton.IsEnabled = true;
 }
 }

 // Пример симуляции скачивания с прогрессом (в будущем для реальной загрузки)
 private async Task SimulateDownloadAsync(IProgress<int> progress, CancellationToken token)
 {
 for (int i =0; i <=100; i +=5)
 {
 token.ThrowIfCancellationRequested();
 progress?.Report(i);
 await Task.Delay(100, token);
 }
 }
 }
}
