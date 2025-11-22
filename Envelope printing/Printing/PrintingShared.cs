using System.Printing;
using System.Text.RegularExpressions;

namespace Envelope_printing
{
    public static class Units
    {
        public const double PxPerMm = 96.0 / 25.4;
        public static double DiuToMm(double diu) => (diu / 96.0) * 25.4;
        public static double MmToDiu(double mm) => (mm / 25.4) * 96.0;
    }
    // PageSizeOption moved to PageSizeOption.cs
}
