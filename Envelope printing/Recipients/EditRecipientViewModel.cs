using EnvelopePrinter.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Envelope_printing
{
    public class EditRecipientViewModel(Recipient recipient, string title) : INotifyPropertyChanged
    {
        public string Title { get; set; } = title;
        public Recipient Recipient { get; set; } = recipient;

        // Реализация INotifyPropertyChanged для будущих нужд (например, для валидации)
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
