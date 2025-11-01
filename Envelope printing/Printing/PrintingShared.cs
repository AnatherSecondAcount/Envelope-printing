using System.Printing;

namespace Envelope_printing
{
 public static class Units
 {
 public const double PxPerMm =96.0 /25.4;
 public static double DiuToMm(double diu) => (diu /96.0) *25.4;
 public static double MmToDiu(double mm) => (mm /25.4) *96.0;
 }

 public class PageSizeOption
 {
 public PageMediaSize Media { get; }
 public double WidthMm { get; }
 public double HeightMm { get; }
 public string Display { get; }
 public PageSizeOption(PageMediaSize media)
 {
 Media = media;
 if (media.Width.HasValue && media.Height.HasValue)
 {
 WidthMm = Units.DiuToMm(media.Width.Value);
 HeightMm = Units.DiuToMm(media.Height.Value);
 }
 else { WidthMm =210; HeightMm =297; }
 var name = media.PageMediaSizeName?.ToString() ?? "Custom";
 Display = $"{name} ({WidthMm:0.#}?{HeightMm:0.#} μμ)";
 }
 }
}
