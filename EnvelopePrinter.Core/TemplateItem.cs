using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace EnvelopePrinter.Core
{
    public class TemplateItem : INotifyPropertyChanged
    {
        private int _id;
        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

        private double _positionX;
        public double PositionX { get => _positionX; set { _positionX = value; OnPropertyChanged(); } }

        private double _positionY;
        public double PositionY { get => _positionY; set { _positionY = value; OnPropertyChanged(); } }

        private double _width;
        public double Width { get => _width; set { _width = value; OnPropertyChanged(); } }

        private double _height;
        public double Height { get => _height; set { _height = value; OnPropertyChanged(); } }

        private string _fontFamily = "Arial";
        public string FontFamily { get => _fontFamily; set { _fontFamily = value; OnPropertyChanged(); } }

        private int _fontSize = 12;
        public int FontSize { get => _fontSize; set { _fontSize = value; OnPropertyChanged(); } }

        private bool _isItalic;
        public bool IsItalic { get => _isItalic; set { _isItalic = value; OnPropertyChanged(); } }

        private string _contentBindingPath = "";
        public string ContentBindingPath { get => _contentBindingPath; set { _contentBindingPath = value; OnPropertyChanged(); } }

        private string _staticText = "";
        public string StaticText { get => _staticText; set { _staticText = value; OnPropertyChanged(); } }

        private string _name = "";
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private bool _isImage;
        public bool IsImage { get => _isImage; set { _isImage = value; OnPropertyChanged(); } }

        private string _imagePath = "";
        public string ImagePath { get => _imagePath; set { _imagePath = value; OnPropertyChanged(); } }

        // Appearance
        private string _background = "Transparent";
        public string Background { get => _background; set { _background = value; OnPropertyChanged(); } }

        private string _borderBrush = "Transparent";
        public string BorderBrush { get => _borderBrush; set { _borderBrush = value; OnPropertyChanged(); } }

        private double _borderThickness = 0;
        public double BorderThickness { get => _borderThickness; set { _borderThickness = value; OnPropertyChanged(); } }

        private double _cornerRadius = 0;
        public double CornerRadius { get => _cornerRadius; set { _cornerRadius = value; OnPropertyChanged(); } }

        private string _fontWeight = "Normal";
        public string FontWeight { get => _fontWeight; set { _fontWeight = value; OnPropertyChanged(); } }

        private string _horizontalAlignment = "Left";
        public string HorizontalAlignment { get => _horizontalAlignment; set { _horizontalAlignment = value; OnPropertyChanged(); } }

        private string _verticalAlignment = "Top";
        public string VerticalAlignment { get => _verticalAlignment; set { _verticalAlignment = value; OnPropertyChanged(); } }

        private string _stretch = "Uniform";
        public string Stretch { get => _stretch; set { _stretch = value; OnPropertyChanged(); } }

        // New: text/visual
        private string _foreground = "Black";
        public string Foreground { get => _foreground; set { _foreground = value; OnPropertyChanged(); } }

        private string _textAlignment = "Left";
        public string TextAlignment { get => _textAlignment; set { _textAlignment = value; OnPropertyChanged(); } }

        private double _padding = 0;
        public double Padding { get => _padding; set { _padding = value; OnPropertyChanged(); } }

        private double _opacity = 1.0;
        public double Opacity { get => _opacity; set { _opacity = value; OnPropertyChanged(); } }

        // Rotation (not persisted yet to avoid DB migration)
        private double _rotationDegrees = 0;
        public double RotationDegrees { get => _rotationDegrees; set { _rotationDegrees = value; OnPropertyChanged(); } }

        // ZIndex for layering
        private int _zIndex = 0;
        public int ZIndex { get => _zIndex; set { _zIndex = value < 0 ? 0 : value; OnPropertyChanged(); } }

        // Внешний ключ
        public int TemplateId { get; set; }
        [ForeignKey("TemplateId")]
        public virtual Template Template { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}