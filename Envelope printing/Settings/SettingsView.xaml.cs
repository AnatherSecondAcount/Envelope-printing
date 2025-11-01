using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace Envelope_printing
{
 public partial class SettingsView : UserControl
 {
 public SettingsView()
 {
 InitializeComponent();
 DataContext = new SettingsVM();
 }

 public Task TriggerUpdateCheckAsync()
 {
 return Updates != null ? Updates.RunCheckAsync() : Task.CompletedTask;
 }
 }

 internal class SettingsVM : INotifyPropertyChanged
 {
 private bool _isDarkTheme;
 public bool IsDarkTheme
 {
 get => _isDarkTheme;
 set { if (_isDarkTheme == value) return; _isDarkTheme = value; OnPropertyChanged(); ApplyTheme(); OnPropertyChanged(nameof(IsLightTheme)); }
 }
 public bool IsLightTheme
 {
 get => !IsDarkTheme;
 set { IsDarkTheme = !value; }
 }

 private void ApplyTheme()
 {
 var app = Application.Current;
 if (app?.Resources == null) return;
 // our merged dictionaries: [0] MinimalisticStyles (light base), [1] placeholder for theme
 if (app.Resources.MergedDictionaries.Count <2) return;
 var themeSlot = app.Resources.MergedDictionaries[1];
 themeSlot.MergedDictionaries.Clear();
 if (IsDarkTheme)
 {
 themeSlot.MergedDictionaries.Add(new ResourceDictionary { Source = new System.Uri("/Styles/DarkTheme.xaml", System.UriKind.Relative) });
 }
 }

 public event PropertyChangedEventHandler PropertyChanged;
 private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
 }
}
