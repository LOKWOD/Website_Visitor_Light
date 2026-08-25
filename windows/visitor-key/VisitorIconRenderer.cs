using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace LOKWOD.VisitorKey
{
    internal static class VisitorIconRenderer
    {
        internal static byte[] Render(Color color, string label, bool affiliate)
        {
            using var upright = new Bitmap(120, 120, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(upright))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.FromArgb(18, 20, 25));
                using var glow = new SolidBrush(color);
                graphics.FillEllipse(glow, 9, 9, 102, 102);
                using var inner = new SolidBrush(Color.FromArgb(225, 15, 17, 21));
                graphics.FillEllipse(inner, 16, 16, 88, 88);
                using var white = new SolidBrush(Color.White);
                graphics.FillEllipse(white, 45, 29, 30, 30);
                graphics.FillRoundedRectangle(white, new RectangleF(32, 62, 56, 32), 15);

                string shortLabel = affiliate ? "$ CLICK" : ShortLabel(label);
                using var font = new Font("Segoe UI", shortLabel.Length > 6 ? 8.5f : 10f, FontStyle.Bold, GraphicsUnit.Point);
                using var textBrush = new SolidBrush(color);
                using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                graphics.DrawString(shortLabel, font, textBrush, new RectangleF(13, 96, 94, 18), format);
            }

            upright.RotateFlip(RotateFlipType.Rotate90FlipNone);
            using var output = new MemoryStream();
            ImageCodecInfo? jpeg = Array.Find(ImageCodecInfo.GetImageEncoders(), item => item.FormatID == ImageFormat.Jpeg.Guid);
            if (jpeg == null) throw new InvalidOperationException("Windows JPEG encoder is unavailable.");
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 88L);
            upright.Save(output, jpeg, parameters);
            return output.ToArray();
        }

        private static string ShortLabel(string label)
        {
            string normalized = (label ?? string.Empty).ToUpperInvariant();
            if (normalized.Contains("DISH")) return "DISHGAL";
            if (normalized.Contains("NAUTICAL")) return "NAUTICAL";
            if (normalized.Contains("SYRACUSE")) return "SYRACUSE";
            if (normalized.Contains("ADVENTURE")) return "ADVENTURE";
            if (normalized.Contains("BEAUTIFUL")) return "BMC";
            if (normalized.Contains("SIMULATION")) return "LITS";
            if (normalized.Contains("APPRAISALS (.ORG)")) return "ARE .ORG";
            if (normalized.Contains("APPRAISALS")) return "ARE .COM";
            if (normalized.Contains("BLAPPOS")) return "BLAPPOS";
            if (normalized.Contains("BIG BUD")) return "BIGBUD";
            if (normalized.Contains("CRYPTO")) return "CRYPTO";
            if (normalized.Contains("LOKWOD")) return "LOKWOD";
            return "VISITOR";
        }

        private static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF rectangle, float radius)
        {
            using var path = new GraphicsPath();
            float diameter = radius * 2;
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            graphics.FillPath(brush, path);
        }
    }
}
