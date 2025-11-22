using EnvelopePrinter.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Envelope_printing
{
    /// <summary>
    /// ViewModel-обёртка для одного элемента шаблона на холсте.
    /// Предоставляет свойства для привязки и уведомления UI-слоя.
    /// </summary>
    public class TemplateItemViewModel : INotifyPropertyChanged
    {
        public TemplateItem Model { get; }

        // --- флаг выхода за границы ---
        private bool _isOutOfBounds;
        public bool IsOutOfBounds { get => _isOutOfBounds; set { _isOutOfBounds = value; OnPropertyChanged(); } }

        // --- Transform для быстрого перемещения (RenderTransform) ---
        public TranslateTransform Transform { get; } = new TranslateTransform();
        public RotateTransform Rotate { get; } = new RotateTransform();
        public TransformGroup CompositeTransform { get; }

        private static readonly HashSet<string> NamedColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Transparent", "Black", "White", "Gray", "Red", "Blue", "LightBlue", "Green" };
        private static string MapBrushToChoice(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "PALETTE";
            return NamedColors.Contains(s.Trim()) ? s.Trim() : "PALETTE";
        }

        private static Brush SafeParseBrush(string s, Brush fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(s)) return fallback;
                var b = (Brush)new BrushConverter().ConvertFromString(s);
                if (b.CanFreeze) b.Freeze();
                return b;
            }
            catch
            {
                return fallback;
            }
        }

        #region Properties
        public double PositionX { get => Model.PositionX; set { Model.PositionX = value; OnPropertyChanged(); } }
        public double PositionY { get => Model.PositionY; set { Model.PositionY = value; OnPropertyChanged(); } }
        public double Width { get => Model.Width; set { Model.Width = value; OnPropertyChanged(); } }
        public double Height { get => Model.Height; set { Model.Height = value; OnPropertyChanged(); } }
        public string Name { get => Model.Name; set { Model.Name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); } }
        public bool IsImage { get => Model.IsImage; set { Model.IsImage = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(DisplayText)); } }
        public string StaticText { get => Model.StaticText; set { Model.StaticText = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(DisplayText)); } }
        public string ImagePath { get => Model.ImagePath; set { Model.ImagePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasImage)); } }
        public bool HasImage => !string.IsNullOrWhiteSpace(Model.ImagePath);
        public string ContentBindingPath { get => Model.ContentBindingPath; set { Model.ContentBindingPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsBound)); OnPropertyChanged(nameof(DisplayText)); } }
        public bool IsBound => !string.IsNullOrEmpty(ContentBindingPath);
        public string DisplayName => string.IsNullOrEmpty(Name) ? (IsImage ? "Изображение" : (string.IsNullOrEmpty(StaticText) ? "Текст" : StaticText)) : Name;
        public string DisplayText => IsImage ? "" : (IsBound ? $"[{ContentBindingPath}]" : StaticText);

        // Appearance brushes used by visual template
        public Brush Background { get => SafeParseBrush(Model.Background, Brushes.Transparent); set { Model.Background = value?.ToString() ?? "Transparent"; OnPropertyChanged(); OnPropertyChanged(nameof(BackgroundString)); OnPropertyChanged(nameof(BackgroundChoice)); } }
        public Brush BorderBrush { get => SafeParseBrush(Model.BorderBrush, Brushes.Transparent); set { Model.BorderBrush = value?.ToString() ?? "Transparent"; OnPropertyChanged(); OnPropertyChanged(nameof(BorderBrushString)); OnPropertyChanged(nameof(BorderBrushChoice)); } }
        public Brush Foreground { get => SafeParseBrush(Model.Foreground, Brushes.Black); set { Model.Foreground = value?.ToString() ?? "Black"; OnPropertyChanged(); OnPropertyChanged(nameof(ForegroundString)); OnPropertyChanged(nameof(ForegroundChoice)); } }

        // String helpers for editing in UI
        public string BackgroundString { get => Model.Background; set { Model.Background = value ?? "Transparent"; OnPropertyChanged(); OnPropertyChanged(nameof(Background)); OnPropertyChanged(nameof(BackgroundChoice)); } }
        public string BorderBrushString { get => Model.BorderBrush; set { Model.BorderBrush = value ?? "Transparent"; OnPropertyChanged(); OnPropertyChanged(nameof(BorderBrush)); OnPropertyChanged(nameof(BorderBrushChoice)); } }
        public string ForegroundString { get => Model.Foreground; set { Model.Foreground = value ?? "Black"; OnPropertyChanged(); OnPropertyChanged(nameof(Foreground)); OnPropertyChanged(nameof(ForegroundChoice)); } }

        // Simple color choices mapping (used by ComboBoxes)
        public string ForegroundChoice { get => MapBrushToChoice(Model.Foreground); set { if (string.IsNullOrEmpty(value)) return; if (value != "PALETTE") { Model.Foreground = value; OnPropertyChanged(nameof(ForegroundString)); OnPropertyChanged(nameof(Foreground)); } OnPropertyChanged(); } }
        public string BackgroundChoice { get => MapBrushToChoice(Model.Background); set { if (string.IsNullOrEmpty(value)) return; if (value != "PALETTE") { Model.Background = value; OnPropertyChanged(nameof(BackgroundString)); OnPropertyChanged(nameof(Background)); } OnPropertyChanged(); } }
        public string BorderBrushChoice { get => MapBrushToChoice(Model.BorderBrush); set { if (string.IsNullOrEmpty(value)) return; if (value != "PALETTE") { Model.BorderBrush = value; OnPropertyChanged(nameof(BorderBrushString)); OnPropertyChanged(nameof(BorderBrush)); } OnPropertyChanged(); } }

        public double BorderThickness { get => Model.BorderThickness; set { Model.BorderThickness = value; OnPropertyChanged(); } }
        public CornerRadius CornerRadius { get => new CornerRadius(Model.CornerRadius); set { Model.CornerRadius = value.TopLeft; OnPropertyChanged(); } }
        public double CornerRadiusValue { get => Model.CornerRadius; set { Model.CornerRadius = value; OnPropertyChanged(); OnPropertyChanged(nameof(CornerRadius)); } }

        // Font: model stores DIP size; designer canvas is scaled x3.78, so expose DisplayFontSize to neutralize
        public int FontSize { get => Model.FontSize; set { Model.FontSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayFontSize)); } }
        public double DisplayFontSize => FontSize / Units.PxPerMm;
        public string FontFamily { get => Model.FontFamily; set { Model.FontFamily = value ?? "Segoe UI"; OnPropertyChanged(); } }
        public FontWeight FontWeight { get { var s = string.IsNullOrWhiteSpace(Model.FontWeight) ? "Normal" : Model.FontWeight; return (FontWeight)new FontWeightConverter().ConvertFromString(s); } set { Model.FontWeight = value.ToString(); OnPropertyChanged(); } }

        private static TEnum SafeParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            if (Enum.TryParse<TEnum>(value, true, out var res)) return res;
            return fallback;
        }
        public HorizontalAlignment HorizontalAlignment { get => SafeParseEnum(Model.HorizontalAlignment, System.Windows.HorizontalAlignment.Left); set { Model.HorizontalAlignment = value.ToString(); OnPropertyChanged(); OnPropertyChanged(nameof(HorizontalAlignmentString)); } }
        public VerticalAlignment VerticalAlignment { get => SafeParseEnum(Model.VerticalAlignment, System.Windows.VerticalAlignment.Top); set { Model.VerticalAlignment = value.ToString(); OnPropertyChanged(); OnPropertyChanged(nameof(VerticalAlignmentString)); } }
        public Stretch Stretch { get => SafeParseEnum(Model.Stretch, System.Windows.Media.Stretch.Uniform); set { Model.Stretch = value.ToString(); OnPropertyChanged(); OnPropertyChanged(nameof(StretchString)); } }
        public TextAlignment TextAlignment { get => SafeParseEnum(Model.TextAlignment, System.Windows.TextAlignment.Left); set { Model.TextAlignment = value.ToString(); OnPropertyChanged(); OnPropertyChanged(nameof(TextAlignmentString)); } }
        public double Padding { get => Model.Padding; set { Model.Padding = value; OnPropertyChanged(); } }
        public double Opacity { get => Model.Opacity; set { Model.Opacity = value; OnPropertyChanged(); } }
        public double RotationDegrees
        {
            get => Model.RotationDegrees; set
            {
                var rounded = Math.Round(value); // шаг1°
                                                 // нормализуем в диапазон [-180;180)
                while (rounded >= 180) rounded -= 360; while (rounded < -180) rounded += 360;
                Model.RotationDegrees = rounded; Rotate.Angle = rounded; OnPropertyChanged();
            }
        }

        public int ZIndex { get => Model.ZIndex; set { var clamped = Math.Max(0, Math.Min(10, value)); Model.ZIndex = clamped; OnPropertyChanged(); } }
        public bool IsItalic { get => Model.IsItalic; set { Model.IsItalic = value; OnPropertyChanged(); OnPropertyChanged(nameof(FontStyle)); } }
        public FontStyle FontStyle => Model.IsItalic ? FontStyles.Italic : FontStyles.Normal;
        public bool IsBold { get => FontWeight == FontWeights.Bold; set { FontWeight = value ? FontWeights.Bold : FontWeights.Normal; OnPropertyChanged(); } }
        #endregion

        // String bridge properties to bind ComboBoxes that use string ItemsSource
        public string HorizontalAlignmentString { get => Model.HorizontalAlignment; set { Model.HorizontalAlignment = value ?? "Left"; OnPropertyChanged(); OnPropertyChanged(nameof(HorizontalAlignment)); } }
        public string VerticalAlignmentString { get => Model.VerticalAlignment; set { Model.VerticalAlignment = value ?? "Top"; OnPropertyChanged(); OnPropertyChanged(nameof(VerticalAlignment)); } }
        public string TextAlignmentString { get => Model.TextAlignment; set { Model.TextAlignment = value ?? "Left"; OnPropertyChanged(); OnPropertyChanged(nameof(TextAlignment)); } }
        public string StretchString { get => Model.Stretch; set { Model.Stretch = value ?? "Uniform"; OnPropertyChanged(); OnPropertyChanged(nameof(Stretch)); } }

        public TemplateItemViewModel(TemplateItem model)
        {
            Model = model;
            Transform.X = model.PositionX;
            Transform.Y = model.PositionY;
            Rotate.Angle = model.RotationDegrees;
            CompositeTransform = new TransformGroup();
            // Rotate first (around element center via RenderTransformOrigin), then translate to position
            CompositeTransform.Children.Add(Rotate);
            CompositeTransform.Children.Add(Transform);
        }

        public void CheckBounds(double parentWidth, double parentHeight)
        {
            IsOutOfBounds = (PositionX + Width > parentWidth) ||
                            (PositionY + Height > parentHeight) ||
                            PositionX < 0 ||
                            PositionY < 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}