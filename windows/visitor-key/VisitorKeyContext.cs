using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LOKWOD.VisitorKey
{
    internal sealed class VisitorKeyContext : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly WindowsFormsSynchronizationContext _ui = new WindowsFormsSynchronizationContext();
        private AppSettings _settings;
        private VisitorLightClient? _client;
        private DarkMountKeyListener? _listener;
        private int _pollGeneration;

        internal VisitorKeyContext()
        {
            _settings = SecureSettings.Load();
            SynchronizationContext.SetSynchronizationContext(_ui);
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Visitor Light", null, (_, __) => OpenDashboard());
            menu.Items.Add("Test visitor key", null, async (_, __) => await ShowVisitorAsync(new VisitorStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Site = "LOKWOD", Kind = "visit", WorkerConnected = true }));
            menu.Items.Add("Settings", null, (_, __) => Configure());
            menu.Items.Add("Restore original key image", null, (_, __) => RestoreOriginal());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, __) => ExitThread());
            _tray = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Text = "LOKWOD Visitor Key",
                ContextMenuStrip = menu,
                Visible = true,
            };
            _tray.DoubleClick += (_, __) => OpenDashboard();

            if (string.IsNullOrWhiteSpace(_settings.ProtectedPassword))
                Configure();
            if (!string.IsNullOrWhiteSpace(_settings.ProtectedPassword))
                Start();
        }

        protected override void ExitThreadCore()
        {
            _stop.Cancel();
            _listener?.Dispose();
            _client?.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            base.ExitThreadCore();
        }

        private void Configure()
        {
            using var form = new SetupForm(_settings);
            if (form.ShowDialog() != DialogResult.OK || form.SavedSettings == null) return;
            _settings = form.SavedSettings;
            _client?.Dispose();
            _client = null;
            Start();
        }

        private void Start()
        {
            try
            {
                if (_listener == null)
                {
                    _listener = new DarkMountKeyListener();
                    _listener.TopRightPressed += (_, __) => Ui(OpenDashboard);
                }
                _client ??= new VisitorLightClient(_settings.DeviceUrl, SecureSettings.Unprotect(_settings.ProtectedPassword));
                int generation = ++_pollGeneration;
                _ = PollAsync(generation, _stop.Token);
                _tray.ShowBalloonTip(2500, "Visitor Key is running", "The upper-right Display Key now follows your Visitor Light.", ToolTipIcon.Info);
            }
            catch (Exception exception)
            {
                _tray.ShowBalloonTip(4000, "Visitor Key needs attention", exception.Message, ToolTipIcon.Error);
            }
        }

        private async Task PollAsync(int generation, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && generation == _pollGeneration && _client != null)
            {
                try
                {
                    VisitorStatus status = await _client.GetStatusAsync(cancellationToken).ConfigureAwait(false);
                    if (status.Timestamp > 0 && status.Timestamp != _settings.LastEventTimestamp)
                    {
                        _settings.LastEventTimestamp = status.Timestamp;
                        SecureSettings.Save(_settings);
                        await ShowVisitorAsync(status).ConfigureAwait(false);
                    }
                    Ui(() => _tray.Text = status.WorkerConnected ? "LOKWOD Visitor Key — connected" : "LOKWOD Visitor Key — Visitor Light offline");
                }
                catch (OperationCanceledException) { return; }
                catch (Exception exception)
                {
                    Ui(() => _tray.Text = "LOKWOD Visitor Key — connection problem");
                    Debug.WriteLine(exception);
                }
                try { await Task.Delay(2000, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        private async Task ShowVisitorAsync(VisitorStatus status)
        {
            await Task.Run(() =>
            {
                byte[] image = VisitorIconRenderer.Render(status.Color, status.Site, status.Kind == "affiliate_click");
                using (var display = new DarkMountDisplay())
                {
                    EnsureBackup(display);
                    display.WriteImage(DarkMount.TopRightDisplayKey, image);
                    byte[] verified = display.ReadImage(DarkMount.TopRightDisplayKey);
                    if (!Equal(image, verified)) throw new IOException("The Visitor Key image did not verify after writing.");
                }
                try
                {
                    using var lamps = new DarkMountLampArray();
                    lamps.Pulse(status.Color.R, status.Color.G, status.Color.B);
                }
                catch (InvalidOperationException)
                {
                    // Some Windows 10 installations do not enumerate the optional
                    // LampArray collection. The Display Key remains fully functional.
                }
            }).ConfigureAwait(false);

            Ui(() => _tray.ShowBalloonTip(2500, status.Kind == "affiliate_click" ? "Affiliate click" : "Website visitor", string.IsNullOrWhiteSpace(status.Site) ? "A visitor arrived." : status.Site, ToolTipIcon.Info));
        }

        private static void EnsureBackup(DarkMountDisplay display)
        {
            if (File.Exists(SecureSettings.BackupPath)) return;
            Directory.CreateDirectory(SecureSettings.AppDirectory);
            File.WriteAllBytes(SecureSettings.BackupPath, display.ReadImage(DarkMount.TopRightDisplayKey));
        }

        private void RestoreOriginal()
        {
            try
            {
                if (!File.Exists(SecureSettings.BackupPath)) throw new FileNotFoundException("No original key image backup exists yet.");
                using var display = new DarkMountDisplay();
                display.WriteImage(DarkMount.TopRightDisplayKey, File.ReadAllBytes(SecureSettings.BackupPath));
                _tray.ShowBalloonTip(2000, "Visitor Key", "The original upper-right key image was restored.", ToolTipIcon.Info);
            }
            catch (Exception exception) { MessageBox.Show(exception.Message, "Visitor Key", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void OpenDashboard()
        {
            try { Process.Start(new ProcessStartInfo { FileName = _settings.DeviceUrl, UseShellExecute = true }); }
            catch (Exception exception) { MessageBox.Show(exception.Message, "Visitor Key", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
            return true;
        }

        private void Ui(Action action)
        {
            if (_stop.IsCancellationRequested) return;
            _ui.Post(_ => action(), null);
        }
    }
}
