using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace LOKWOD.VisitorKey
{
    internal sealed class DarkMountLampArray : IDisposable
    {
        private readonly IntPtr _device;
        private readonly int _lampCount;

        internal DarkMountLampArray()
        {
            string path = HidApi.FindPath(0x0059, 3);
            _device = HidApi.hid_open_path(path);
            if (_device == IntPtr.Zero) throw new IOException("The Dark Mount lighting interface could not be opened.");
            byte[] attributes = GetFeature(1, 22);
            _lampCount = BinaryPrimitives.ReadUInt16LittleEndian(attributes.AsSpan(0, 2));
            if (_lampCount <= 0 || _lampCount > 1024) throw new IOException("The Dark Mount returned an invalid lamp count.");
        }

        public void Dispose()
        {
            try { Release(); } catch { }
            HidApi.hid_close(_device);
        }

        internal int LampCount => _lampCount;

        internal void Pulse(byte red, byte green, byte blue)
        {
            SetFeature(6, new byte[] { 0 }, 1);
            try
            {
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    SetRange(0, _lampCount - 1, red, green, blue);
                    Thread.Sleep(180);
                    SetRange(0, _lampCount - 1, 0, 0, 0);
                    Thread.Sleep(120);
                }
            }
            finally
            {
                Release();
            }
        }

        private void Release() => SetFeature(6, new byte[] { 1 }, 1);

        private void SetRange(int first, int last, byte red, byte green, byte blue)
        {
            byte[] payload = new byte[8];
            payload[0] = 1;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1, 2), checked((ushort)first));
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(3, 2), checked((ushort)last));
            payload[5] = red;
            payload[6] = green;
            payload[7] = blue;
            SetFeature(5, payload, 8);
        }

        private byte[] GetFeature(byte reportId, int payloadSize)
        {
            byte[] report = new byte[payloadSize + 1];
            report[0] = reportId;
            int read = HidApi.hid_get_feature_report(_device, report, (UIntPtr)report.Length);
            if (read != report.Length) throw new IOException($"Lighting report {reportId} was incomplete ({read}/{report.Length}).");
            byte[] payload = new byte[payloadSize];
            Buffer.BlockCopy(report, 1, payload, 0, payloadSize);
            return payload;
        }

        private void SetFeature(byte reportId, byte[] payload, int payloadSize)
        {
            if (payload.Length > payloadSize) throw new ArgumentOutOfRangeException(nameof(payload));
            byte[] report = new byte[payloadSize + 1];
            report[0] = reportId;
            Buffer.BlockCopy(payload, 0, report, 1, payload.Length);
            int written = HidApi.hid_send_feature_report(_device, report, (UIntPtr)report.Length);
            if (written != report.Length) throw new IOException($"Lighting report {reportId} was incomplete ({written}/{report.Length}).");
        }
    }
}
