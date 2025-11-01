using System;

namespace Envelope_printing
{
 public static class PrintUtilities
 {
 // Compute margins (mm) from PageImageableArea parameters given in DIU (device independent units)
 public static (double leftMm, double topMm, double rightMm, double bottomMm) ComputeMarginsFromAreaDiu(
 double pageW, double pageH,
 double originWidth, double originHeight,
 double extentWidth, double extentHeight,
 bool needSwap)
 {
 if (needSwap)
 {
 (originWidth, originHeight) = (originHeight, originWidth);
 (extentWidth, extentHeight) = (extentHeight, extentWidth);
 }

 double rDiu = Math.Max(0, pageW - (originWidth + extentWidth));
 double bDiu = Math.Max(0, pageH - (originHeight + extentHeight));
 double leftMm = Units.DiuToMm(originWidth);
 double topMm = Units.DiuToMm(originHeight);
 double rightMm = Units.DiuToMm(rDiu);
 double bottomMm = Units.DiuToMm(bDiu);
 return (leftMm, topMm, rightMm, bottomMm);
 }
 }
}
