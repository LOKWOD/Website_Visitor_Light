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
        private readonly SemaphoreSlim _visitorGate = new SemaphoreSlim(1, 1);
        private readonly System.Windows.Forms.Timer _watchdog = new System.Windows.Forms.Timer();
        private AppSettings _settings;
        private VisitorLightClient? _client;
        private DarkMountKeyListener? _listener;
        private VisitorPopupForm? _popup;
        private int _pollGeneration;
        private int _consecutiveFailures;
        private DateTime _lastPollAttemptUtc = DateTime.UtcNow;

        internal VisitorKeyContext()
        {
            _settings = SecureSettings.Load();
            SynchronizationContext.SetSynchronizationContext(_ui);
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Visitor Light", null, (_, __) => OpenDashboard());
            menu.Items.Add("Test visitor key", null, async (_, __) => await ShowVisitorAsync(new VisitorStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Site = "LOKWOD", Kind = "visit", WorkerConnected = true }));
            menu.Items.Add("Settings", null, (_, __) => Configure());
            menu.Items.Add("Restore original key image", null, (_, __) => RestoreOriginal());
            menu.Items.Add("Open diagnostic log", null, (_, __) => OpenLog());
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

            _watchdog.Interval = 15000;
            _watchdog.Tick += (_, __) => WatchdogTick();
            _watchdog.Start();
            AppLog.Write("LOKWOD Visitor Key started.");

            if (string.IsNullOrWhiteSpace(_settings.ProtectedPassword))
                Configure();
            if (!string.IsNullOrWhiteSpace(_settings.ProtectedPassword))
                Start();
        }

        protected override void ExitThreadCore()
        {
            _stop.Cancel();
            _watchdog.Stop();
            _watchdog.Dispose();
            _popup?.Close();
            _popup?.Dispose();
            _listener?.Dispose();
            Interlocked.Exchange(ref _client, null)?.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            AppLog.Write("LOKWOD Visitor Key stopped.");
            base.ExitThreadCore();
        }

        private void Configure()
        {
            using var form = new SetupForm(_settings);
            if (form.ShowDialog() != DialogResult.OK || form.SavedSettings == null) return;
            _settings = form.SavedSettings;
            ++_pollGeneration;
            Interlocked.Exchange(ref _client, null)?.Dispose();
            Start();
        }

        private void Start()
        {
            try
            {
                if (_listener == null)
                {
                    _listener = new DarkMountKeyListener();
                    _listener.VisitorKeyPressed += (_, __) => Ui(OpenDashboard);
                }
                _client ??= CreateClient();
                int generation = ++_pollGeneration;
                _consecutiveFailures = 0;
                _lastPollAttemptUtc = DateTime.UtcNow;
                _ = PollAsync(generation, _stop.Token);
                AppLog.Write($"Visitor polling generation {generation} started for {_settings.DeviceUrl}.");
            }
            catch (Exception exception)
            {
                _tray.Text = "LOKWOD Visitor Key — connection problem";
                AppLog.Write("Visitor Key could not start polling; the watchdog will retry silently.", exception);
            }
        }

        private async Task PollAsync(int generation, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && generation == _pollGeneration && _client != null)
            {
                try
                {
                    _lastPollAttemptUtc = DateTime.UtcNow;
                    VisitorLightClient? client = _client;
                    if (client == null) return;
                    VisitorStatus status = await client.GetStatusAsync(cancellationToken).ConfigureAwait(false);
                    _consecutiveFailures = 0;
                    if (status.Timestamp > 0 && status.Timestamp != _settings.LastEventTimestamp)
                    {
                        _settings.LastEventTimestamp = status.Timestamp;
                        try { SecureSettings.Save(_settings); }
                        catch (Exception exception) { AppLog.Write("Could not save the newest visitor timestamp.", exception); }
                        await ShowVisitorAsync(status).ConfigureAwait(false);
                    }
                    Ui(() => _tray.Text = status.WorkerConnected ? "LOKWOD Visitor Key — connected" : "LOKWOD Visitor Key — Visitor Light offline");
                }
                catch (OperationCanceledException) { return; }
                catch (Exception exception)
                {
                    int failures = Interlocked.Increment(ref _consecutiveFailures);
                    Ui(() => _tray.Text = "LOKWOD Visitor Key — connection problem");
                    Debug.WriteLine(exception);
                    if (failures == 1 || failures % 10 == 0)
                        AppLog.Write($"Visitor polling failed ({failures} consecutive failures).", exception);
                    if (failures >= 3 && generation == _pollGeneration)
                        ReplaceClient(generation);
                }
                try { await Task.Delay(2000, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        private async Task ShowVisitorAsync(VisitorStatus status)
        {
            Ui(() => ShowPopup(status));

            try { await _visitorGate.WaitAsync(_stop.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            try
            {
                await Task.Run(() =>
                {
                    Exception? displayFailure = null;
                    Exception? lightingFailure = null;

                    try
                    {
                        byte[] image = VisitorIconRenderer.Render(status.Color, status.Site, status.Kind == "affiliate_click");
                        using var display = new DarkMountDisplay();
                        EnsureBackup(display);
                        display.WriteImage(DarkMount.VisitorDisplayKey, image);
                        byte[] verified = display.ReadImage(DarkMount.VisitorDisplayKey);
                        if (!Equal(image, verified)) throw new IOException("The Visitor Key image did not verify after writing.");
                    }
                    catch (Exception exception) { displayFailure = exception; }

                    try
                    {
                        using var lamps = new DarkMountLampArray();
                        lamps.Pulse(status.Color.R, status.Color.G, status.Color.B);
                    }
                    catch (Exception exception) { lightingFailure = exception; }

                    if (displayFailure != null)
                        AppLog.Write("The visitor popup worked, but the display-key image update failed.", displayFailure);
                    if (lightingFailure != null)
                        AppLog.Write("The visitor popup worked, but the keyboard-light flash failed.", lightingFailure);
                    if (displayFailure != null && lightingFailure != null)
                        throw new AggregateException("Both Dark Mount notification paths failed.", displayFailure, lightingFailure);
                }, _stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                // The on-screen alert is deliberately independent of the keyboard.
                // A temporary USB/HID problem must not swallow a visitor notification.
                AppLog.Write("The corner notification was shown, but the keyboard update failed.", exception);
                Ui(() => _tray.Text = "LOKWOD Visitor Key — keyboard reconnecting");
            }
            finally { _visitorGate.Release(); }
        }

        private void ShowPopup(VisitorStatus status)
        {
            try { System.Media.SystemSounds.Asterisk.Play(); }
            catch (Exception exception) { AppLog.Write("The visitor notification sound could not play.", exception); }

            try
            {
                _popup?.Close();
                _popup?.Dispose();
                _popup = new VisitorPopupForm(status);
                _popup.Show();
            }
            catch (Exception exception)
            {
                AppLog.Write("Custom corner notification failed; using the Windows tray notification.", exception);
                _tray.ShowBalloonTip(4000, status.Kind == "affiliate_click" ? "Affiliate click" : "Website visitor", string.IsNullOrWhiteSpace(status.Site) ? "A visitor arrived." : status.Site, ToolTipIcon.Info);
            }
        }

        private static void EnsureBackup(DarkMountDisplay display)
        {
            Directory.CreateDirectory(SecureSettings.AppDirectory);
            if (!File.Exists(SecureSettings.BackupPath))
                File.WriteAllBytes(SecureSettings.BackupPath, display.ReadImage(DarkMount.VisitorDisplayKey));

            if (!File.Exists(SecureSettings.LegacyBackupPath) || File.Exists(SecureSettings.KeyMigrationMarkerPath)) return;
            byte[] original = File.ReadAllBytes(SecureSettings.LegacyBackupPath);
            display.WriteImage(DarkMount.LegacyVisitorDisplayKey, original);
            if (!Equal(original, display.ReadImage(DarkMount.LegacyVisitorDisplayKey)))
                throw new IOException("The original lower Display Key image did not verify after restoring it.");
            File.WriteAllText(SecureSettings.KeyMigrationMarkerPath, DateTimeOffset.Now.ToString("O"));
            AppLog.Write("Moved the visitor image to key 4 and restored the original key 8 image.");
        }

        private void RestoreOriginal()
        {
            try
            {
                if (!File.Exists(SecureSettings.BackupPath)) throw new FileNotFoundException("No original key image backup exists yet.");
                using var display = new DarkMountDisplay();
                display.WriteImage(DarkMount.VisitorDisplayKey, File.ReadAllBytes(SecureSettings.BackupPath));
                _tray.Text = "LOKWOD Visitor Key — original key image restored";
            }
            catch (Exception exception) { MessageBox.Show(exception.Message, "Visitor Key", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void OpenDashboard()
        {
            try { Process.Start(new ProcessStartInfo { FileName = _settings.DeviceUrl, UseShellExecute = true }); }
            catch (Exception exception) { MessageBox.Show(exception.Message, "Visitor Key", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void OpenLog()
        {
            try
            {
                Directory.CreateDirectory(SecureSettings.AppDirectory);
                if (!File.Exists(AppLog.Path)) AppLog.Write("Diagnostic log opened.");
                Process.Start(new ProcessStartInfo { FileName = AppLog.Path, UseShellExecute = true });
            }
            catch (Exception exception) { MessageBox.Show(exception.Message, "Visitor Key", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private VisitorLightClient CreateClient() =>
            new VisitorLightClient(_settings.DeviceUrl, SecureSettings.Unprotect(_settings.ProtectedPassword));

        private void ReplaceClient(int generation)
        {
            if (_stop.IsCancellationRequested || generation != _pollGeneration) return;
            try
            {
                VisitorLightClient replacement = CreateClient();
                VisitorLightClient? previous = Interlocked.Exchange(ref _client, replacement);
                previous?.Dispose();
                Interlocked.Exchange(ref _consecutiveFailures, 0);
                AppLog.Write("Recreated the Visitor Light network client after repeated failures.");
            }
            catch (Exception exception) { AppLog.Write("Could not recreate the Visitor Light network client.", exception); }
        }

        private void WatchdogTick()
        {
            if (_stop.IsCancellationRequested || string.IsNullOrWhiteSpace(_settings.ProtectedPassword)) return;
            if (DateTime.UtcNow - _lastPollAttemptUtc <= TimeSpan.FromSeconds(20)) return;

            AppLog.Write("Polling watchdog restarted the Visitor Light connection.");
            ++_pollGeneration;
            Interlocked.Exchange(ref _client, null)?.Dispose();
            Start();
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
            _ui.Post(_ =>
            {
                try { action(); }
                catch (Exception exception) { AppLog.Write("A tray user-interface action failed.", exception); }
            }, null);
        }
    }
}
