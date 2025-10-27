using EnvelopePrinter.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Envelope_printing
{
    /// Это "обертка" над  Recipient.
    /// Она содержит саму модель и дополнительные свойства для отображения в UI,
    /// например, флаг, является ли запись результатом поиска.
    public class RecipientViewModel(Recipient model) : INotifyPropertyChanged
    {
        public Recipient Model { get; } = model;
        private bool _isMatch;

        public bool IsMatch
        {
            get => _isMatch;
            set
            {
                _isMatch = value;
                OnPropertyChanged();
            }
        }

        public int Id => Model.Id;
        public string OrganizationName => Model.OrganizationName;
        public string AddressLine1 => Model.AddressLine1;
        public string City => Model.City;
        public string PostalCode => Model.PostalCode;
        public string Region => Model.Region;
        public string Country => Model.Country;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}