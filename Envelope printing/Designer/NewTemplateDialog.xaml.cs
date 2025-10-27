using System.Windows;

namespace Envelope_printing
{
    public partial class NewTemplateDialog : Window
    {
        public string TemplateName => NameBox.Text;

        public NewTemplateDialog()
        {
            InitializeComponent();
        }

        // --- ДОБАВЬТЕ ЭТОТ МЕТОД ---
        private void NameBox_Loaded(object sender, RoutedEventArgs e)
        {
            // Устанавливаем фокус на поле ввода, когда оно загрузится
            NameBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("Введите название шаблона.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}