using System;
using System.IO;
using System.Threading;

namespace LOKWOD.VisitorKey
{
    internal sealed class DarkMountKeyListener : IDisposable
    {
        private readonly IntPtr _device;
        private readonly Thread _thread;
        private volatile bool _running = true;
        internal event EventHandler? TopRightPressed;

        internal DarkMountKeyListener()
        {
            string path = HidApi.FindPath(0xFF00, 2);
            _device = HidApi.hid_open_path(path);
            if (_device == IntPtr.Zero) throw new IOException("The Dark Mount display-key interface could not be opened.");
            _thread = new Thread(ReadLoop) { IsBackground = true, Name = "Dark Mount display-key listener" };
            _thread.Start();
        }

        public void Dispose()
        {
            _running = false;
            _thread.Join(700);
            HidApi.hid_close(_device);
        }

        private void ReadLoop()
        {
            var frame = new byte[64];
            while (_running)
            {
                int read = HidApi.hid_read_timeout(_device, frame, (UIntPtr)frame.Length, 250);
                if (read != 64) continue;
                if (frame[5] == 0x11 && frame[6] == 0x02 && frame[7] == DarkMount.TopRightDisplayKey && frame[9] == 0x01)
                    TopRightPressed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
