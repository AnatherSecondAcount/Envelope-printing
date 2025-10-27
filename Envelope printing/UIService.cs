using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Envelope_printing
{
    // Этот сервис будет "синглтоном" - один экземпляр на все приложение.
    public class UIService : INotifyPropertyChanged
    {
        private static UIService _instance;
        public static UIService Instance => _instance ??= new UIService();

        private bool _isMainNavExpanded = true;
        public bool IsMainNavExpanded
        {
            get => _isMainNavExpanded;
            set { _isMainNavExpanded = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}