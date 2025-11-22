using System.Windows;
using System.Windows.Media.Animation;

namespace Envelope_printing.Animations
{
    // Lightweight GridLength animation (pixel units only) for ColumnDefinition.Width
    public class GridLengthAnimation : AnimationTimeline
    {
        public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From), typeof(GridLength), typeof(GridLengthAnimation));
        public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(GridLength), typeof(GridLengthAnimation));

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }
        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            double from = From.Value;
            double to = To.Value;
            if (animationClock.CurrentProgress == null) return new GridLength(from, GridUnitType.Pixel);
            double progress = animationClock.CurrentProgress.Value;
            double current = from + (to - from) * progress;
            return new GridLength(current, GridUnitType.Pixel);
        }
    }
}
