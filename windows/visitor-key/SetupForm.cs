using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LOKWOD.VisitorKey
{
    internal sealed class SetupForm : Form
    {
        private readonly TextBox _url = new TextBox();
        private readonly TextBox _password = new TextBox();
        private readonly Label _status = new Label();
        private readonly Button _save = new Button();
        internal AppSettings? SavedSettings { get; private set; }

        internal SetupForm(AppSettings current)
        {
            Text = "LOKWOD Visitor Key Setup";
            ClientSize = new Size(460, 270);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10f);

            Controls.Add(new Label { Text = "Visitor Light address", Left = 24, Top = 24, Width = 200 });
            _url.SetBounds(24, 49, 410, 30);
            _url.Text = current.DeviceUrl;
            Controls.Add(_url);
            Controls.Add(new Label { Text = "Dashboard password", Left = 24, Top = 91, Width = 200 });
            _password.SetBounds(24, 116, 410, 30);
            _password.UseSystemPasswordChar = true;
            Controls.Add(_password);
            _status.SetBounds(24, 157, 410, 42);
            _status.ForeColor = Color.FromArgb(80, 88, 102);
            _status.Text = "The password is encrypted for your Windows account and never leaves this PC.";
            Controls.Add(_status);
            _save.Text = "Connect and save";
            _save.SetBounds(284, 213, 150, 34);
            _save.Click += async (_, __) => await SaveAsync();
            Controls.Add(_save);
            AcceptButton = _save;
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(_url.Text) || string.IsNullOrEmpty(_password.Text))
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "Enter the Visitor Light address and dashboard password.";
                return;
            }
            _save.Enabled = false;
            _status.ForeColor = Color.FromArgb(80, 88, 102);
            _status.Text = "Connecting…";
            try
            {
                using var client = new VisitorLightClient(_url.Text.Trim(), _password.Text);
                await client.GetStatusAsync(CancellationToken.None);
                SavedSettings = new AppSettings
                {
                    DeviceUrl = _url.Text.Trim().TrimEnd('/'),
                    ProtectedPassword = SecureSettings.Protect(_password.Text),
                };
                SecureSettings.Save(SavedSettings);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception exception)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = exception.Message;
                _save.Enabled = true;
            }
        }
    }
}
