using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LOKWOD.VisitorKey
{
    internal sealed class VisitorPopupForm : Form
    {
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private readonly Timer _closeTimer = new Timer();
        private readonly Color _visitorColor;
        private readonly string _site;
        private readonly bool _affiliate;

        internal VisitorPopupForm(VisitorStatus status)
        {
            _visitorColor = status.Color;
            _site = string.IsNullOrWhiteSpace(status.Site) ? "A visitor arrived" : status.Site;
            _affiliate = string.Equals(status.Kind, "affiliate_click", StringComparison.OrdinalIgnoreCase);

            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(22, 24, 30);
            ClientSize = new Size(372, 112);
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            Opacity = 0.97;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;

            _closeTimer.Interval = _affiliate ? 6500 : 5000;
            _closeTimer.Tick += (_, __) => Close();
            Shown += (_, __) =>
            {
                PlaceInCorner();
                _closeTimer.Start();
            };
            FormClosed += (_, __) =>
            {
                _closeTimer.Stop();
                _closeTimer.Dispose();
            };
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
                return parameters;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var border = new Pen(Color.FromArgb(90, _visitorColor), 2f);
            graphics.DrawPopupRoundedRectangle(border, new RectangleF(1, 1, ClientSize.Width - 3, ClientSize.Height - 3), 14f);
            using var accent = new SolidBrush(_visitorColor);
            graphics.FillPopupRoundedRectangle(accent, new RectangleF(0, 0, 7, ClientSize.Height), 3f);
            graphics.FillEllipse(accent, 24, 24, 64, 64);
            using var inner = new SolidBrush(Color.FromArgb(22, 24, 30));
            graphics.FillEllipse(inner, 31, 31, 50, 50);
            using var person = new SolidBrush(Color.White);
            graphics.FillEllipse(person, 50, 40, 13, 13);
            graphics.FillPopupRoundedRectangle(person, new RectangleF(43, 57, 27, 17), 8f);

            using var titleFont = new Font("Segoe UI Semibold", 12f, FontStyle.Bold, GraphicsUnit.Point);
            using var siteFont = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
            using var noteFont = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            using var titleBrush = new SolidBrush(_visitorColor);
            using var white = new SolidBrush(Color.White);
            using var muted = new SolidBrush(Color.FromArgb(170, 180, 192));

            graphics.DrawString(_affiliate ? "AFFILIATE CLICK" : "WEBSITE VISITOR", titleFont, titleBrush, 105, 18);
            graphics.DrawString(_site, siteFont, white, new RectangleF(105, 47, 246, 28));
            graphics.DrawString("LOKWOD Visitor Light  •  just now", noteFont, muted, 105, 79);
        }

        private void PlaceInCorner()
        {
            Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(area.Right - Width - 18, area.Bottom - Height - 18);
        }
    }

    internal static class PopupGraphicsExtensions
    {
        internal static void DrawPopupRoundedRectangle(this Graphics graphics, Pen pen, RectangleF rectangle, float radius)
        {
            using GraphicsPath path = RoundedPath(rectangle, radius);
            graphics.DrawPath(pen, path);
        }

        internal static void FillPopupRoundedRectangle(this Graphics graphics, Brush brush, RectangleF rectangle, float radius)
        {
            using GraphicsPath path = RoundedPath(rectangle, radius);
            graphics.FillPath(brush, path);
        }

        private static GraphicsPath RoundedPath(RectangleF rectangle, float radius)
        {
            var path = new GraphicsPath();
            float diameter = Math.Min(radius * 2f, Math.Min(rectangle.Width, rectangle.Height));
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
