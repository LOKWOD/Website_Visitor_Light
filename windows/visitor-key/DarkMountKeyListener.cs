using System;
using System.IO;
using System.Threading;

namespace LOKWOD.VisitorKey
{
    internal sealed class DarkMountKeyListener : IDisposable
    {
        private readonly Thread _thread;
        private volatile bool _running = true;
        internal event EventHandler? VisitorKeyPressed;

        internal DarkMountKeyListener()
        {
            _thread = new Thread(ReadLoop) { IsBackground = true, Name = "Dark Mount display-key listener" };
            _thread.Start();
        }

        public void Dispose()
        {
            _running = false;
            _thread.Join(1500);
        }

        private void ReadLoop()
        {
            var frame = new byte[64];
            IntPtr device = IntPtr.Zero;
            try
            {
                while (_running)
                {
                    try
                    {
                        if (device == IntPtr.Zero)
                        {
                            string path = HidApi.FindPath(0xFF00, 2);
                            device = HidApi.hid_open_path(path);
                            if (device == IntPtr.Zero) throw new IOException("The Dark Mount display-key interface could not be opened.");
                            AppLog.Write("Dark Mount display-key listener connected.");
                        }

                        int read;
                        DarkMount.DisplayIoGate.Wait();
                        try { read = HidApi.hid_read_timeout(device, frame, (UIntPtr)frame.Length, 250); }
                        finally { DarkMount.DisplayIoGate.Release(); }

                        if (read < 0)
                        {
                            AppLog.Write("Dark Mount display-key listener lost its HID connection.");
                            HidApi.hid_close(device);
                            device = IntPtr.Zero;
                            WaitBeforeReconnect();
                            continue;
                        }
                        if (read != 64) continue;
                        if (frame[5] == 0x11 && frame[6] == 0x02 && frame[7] == DarkMount.VisitorDisplayKey && frame[9] == 0x01)
                            VisitorKeyPressed?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception exception)
                    {
                        AppLog.Write("Dark Mount display-key listener will reconnect.", exception);
                        if (device != IntPtr.Zero)
                        {
                            HidApi.hid_close(device);
                            device = IntPtr.Zero;
                        }
                        WaitBeforeReconnect();
                    }
                }
            }
            finally
            {
                if (device != IntPtr.Zero) HidApi.hid_close(device);
            }
        }

        private void WaitBeforeReconnect()
        {
            for (int attempt = 0; attempt < 10 && _running; attempt++) Thread.Sleep(100);
        }
    }
}
