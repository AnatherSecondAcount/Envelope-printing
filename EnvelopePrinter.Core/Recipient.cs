using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EnvelopePrinter.Core
{
    public class Recipient : INotifyPropertyChanged
    {
        // 2. Определяем поля для хранения данных
        private int _id;
        private string _organizationName = ""; // Сразу инициализируем
        private string _addressLine1 = "";
        private string _city = "";
        private string _postalCode = "";
        private string _region = ""; // Область/Регион
        private string _country = ""; // Страна

        // 3. Превращаем свойства в "умные" свойства с уведомлением
        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        public string OrganizationName
        {
            get => _organizationName;
            set
            {
                _organizationName = value;
                OnPropertyChanged();
            }
        }

        public string AddressLine1
        {
            get => _addressLine1;
            set
            {
                _addressLine1 = value;
                OnPropertyChanged();
            }
        }

        public string City
        {
            get => _city;
            set
            {
                _city = value;
                OnPropertyChanged();
            }
        }

        public string PostalCode
        {
            get => _postalCode;
            set
            {
                _postalCode = value;
                OnPropertyChanged();
            }
        }

        public string Region
        {
            get => _region;
            set
            {
                _region = value;
                OnPropertyChanged();
            }
        }

        public string Country
        {
            get => _country;
            set
            {
                _country = value;
                OnPropertyChanged();
            }
        }

        // 4. Реализация самого механизма уведомлений
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
