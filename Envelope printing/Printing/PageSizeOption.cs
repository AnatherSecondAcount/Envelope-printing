using System.Printing;

namespace Envelope_printing
{
    public class PageSizeOption
    {
        public PageMediaSize Media { get; }
        public double WidthMm { get; }
        public double HeightMm { get; }
        public string Display { get; }
        public string Name { get; }
        public PageSizeOption(PageMediaSize media)
        {
            Media = media;
            double w = Units.DiuToMm(media.Width ?? 0);
            double h = Units.DiuToMm(media.Height ?? 0);
            WidthMm = Math.Min(w, h);
            HeightMm = Math.Max(w, h);
            Name = media.PageMediaSizeName?.ToString() ?? "Custom";
            Display = $"{Name} {WidthMm:0.#} x {HeightMm:0.#} לל"; // ןנטלונ: A4 210 x 297 לל
        }
        public override string ToString() => Display;
    }
}