using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace LOKWOD.VisitorKey
{
    internal sealed class DarkMountDisplay : IDisposable
    {
        private const int PacketSize = 64;
        private const int DataOffset = 7;
        private const int CrcOffset = 62;
        private const int HeaderSize = 9;
        private const int ReadChunk = 54;
        private const int WriteChunk = 49;
        private const int MaxImageBytes = 32 * 1024;
        private readonly IntPtr _device;
        private byte _sequence = 0x30;
        private byte _session;

        internal DarkMountDisplay()
        {
            string path = HidApi.FindPath(0xFF00, 2);
            _device = HidApi.hid_open_path(path);
            if (_device == IntPtr.Zero) throw new IOException("The Dark Mount vendor interface could not be opened. Close IO Center and try again.");
            Drain();
            OpenSession();
        }

        public void Dispose()
        {
            if (_device == IntPtr.Zero) return;
            if (_session != 0)
            {
                try { Exchange(0x01, 0x02, Array.Empty<byte>(), _session); } catch { }
                _session = 0;
            }
            HidApi.hid_close(_device);
        }

        internal byte[] ReadImage(byte keyId)
        {
            ValidateKey(keyId);
            byte[] header = ImageRequest(0x03, keyId, 0, new[] { (byte)HeaderSize });
            if (header.Length < HeaderSize) throw new IOException("The display-key image header was incomplete.");
            int total = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
            ushort width = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4, 2));
            ushort height = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6, 2));
            byte format = header[8];
            if (total <= HeaderSize || total > MaxImageBytes || width != 120 || height != 120 || format != 3)
                throw new IOException($"Unexpected display image header ({total} bytes, {width}x{height}, format {format}).");

            using var output = new MemoryStream(total - HeaderSize);
            int offset = HeaderSize;
            while (offset < total)
            {
                int count = Math.Min(ReadChunk, total - offset);
                byte[] chunk = ImageRequest(0x03, keyId, offset, new[] { (byte)count });
                if (chunk.Length == 0) throw new IOException($"No display image data at offset {offset}.");
                output.Write(chunk, 0, Math.Min(count, chunk.Length));
                offset += Math.Min(count, chunk.Length);
            }
            return output.ToArray();
        }

        internal void WriteImage(byte keyId, byte[] jpeg)
        {
            ValidateKey(keyId);
            if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8 || jpeg[^2] != 0xFF || jpeg[^1] != 0xD9)
                throw new ArgumentException("The display image must be a complete JPEG.", nameof(jpeg));
            int total = HeaderSize + jpeg.Length;
            if (total > MaxImageBytes) throw new ArgumentException("The display image exceeds the safe 32 KB limit.", nameof(jpeg));

            byte[] blob = new byte[total];
            BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(0, 4), total);
            BinaryPrimitives.WriteUInt16LittleEndian(blob.AsSpan(4, 2), 120);
            BinaryPrimitives.WriteUInt16LittleEndian(blob.AsSpan(6, 2), 120);
            blob[8] = 3;
            Buffer.BlockCopy(jpeg, 0, blob, HeaderSize, jpeg.Length);

            for (int offset = 0; offset < blob.Length; offset += WriteChunk)
            {
                int count = Math.Min(WriteChunk, blob.Length - offset);
                byte[] chunk = new byte[count];
                Buffer.BlockCopy(blob, offset, chunk, 0, count);
                ImageRequest(0x02, keyId, offset, chunk);
            }
        }

        private byte[] ImageRequest(byte operation, byte keyId, int offset, byte[] tail)
        {
            byte[] payload = new byte[6 + tail.Length];
            payload[0] = keyId;
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(2, 4), offset);
            Buffer.BlockCopy(tail, 0, payload, 6, tail.Length);
            return Exchange(0x20, operation, payload, _session);
        }

        private void OpenSession()
        {
            int nonce = RandomNumberGenerator.GetInt32(10000, 100000);
            byte[] payload = new byte[5];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), nonce);
            payload[4] = 2;
            byte[] response = Exchange(0x01, 0x01, payload, 0);
            if (response.Length < 7 || BinaryPrimitives.ReadInt32LittleEndian(response.AsSpan(0, 4)) != nonce || response[4] == 0)
                throw new IOException("The Dark Mount rejected the display session.");
            _session = response[4];
        }

        private byte[] Exchange(byte group, byte command, byte[] payload, byte session)
        {
            if (!Allowed(group, command)) throw new InvalidOperationException($"Dark Mount command {group:X2}/{command:X2} is not allowlisted.");
            byte sequence = NextSequence();
            byte[] packet = new byte[PacketSize];
            packet[0] = checked((byte)(6 + payload.Length));
            packet[2] = session;
            packet[4] = sequence;
            packet[5] = group;
            packet[6] = command;
            Buffer.BlockCopy(payload, 0, packet, DataOffset, payload.Length);
            ushort crc = Crc16(packet.AsSpan(0, CrcOffset));
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(CrcOffset, 2), crc);

            byte[] report = new byte[PacketSize + 1];
            Buffer.BlockCopy(packet, 0, report, 1, PacketSize);
            int written = HidApi.hid_write(_device, report, (UIntPtr)report.Length);
            if (written != report.Length) throw new IOException($"The Dark Mount report was incomplete ({written}/{report.Length}).");

            DateTime deadline = DateTime.UtcNow.AddSeconds(2);
            byte[] frame = new byte[PacketSize];
            while (DateTime.UtcNow < deadline)
            {
                int read = HidApi.hid_read_timeout(_device, frame, (UIntPtr)frame.Length, 150);
                if (read < 0) throw new IOException("The Dark Mount stopped responding.");
                if (read != PacketSize || frame[0] < 6 || frame[0] >= CrcOffset) continue;
                ushort expected = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(CrcOffset, 2));
                if (Crc16(frame.AsSpan(0, CrcOffset)) != expected) continue;
                if (frame[4] != sequence || frame[5] != group || frame[6] != command) continue;
                if (frame[3] != 0) throw new IOException($"The Dark Mount rejected command {group:X2}/{command:X2} (status {frame[3]}).");
                int end = Math.Min(CrcOffset, frame[0] + 1);
                byte[] result = new byte[Math.Max(0, end - DataOffset)];
                Buffer.BlockCopy(frame, DataOffset, result, 0, result.Length);
                return result;
            }
            throw new TimeoutException($"No Dark Mount response to {group:X2}/{command:X2}.");
        }

        private void Drain()
        {
            byte[] buffer = new byte[PacketSize];
            while (HidApi.hid_read_timeout(_device, buffer, (UIntPtr)buffer.Length, 0) > 0) { }
        }

        private byte NextSequence()
        {
            _sequence++;
            if (_sequence == 0) _sequence = 1;
            return _sequence;
        }

        private static bool Allowed(byte group, byte command) =>
            (group == 0x01 && (command == 0x01 || command == 0x02)) ||
            (group == 0x20 && (command == 0x02 || command == 0x03));

        private static void ValidateKey(byte keyId)
        {
            if (keyId < 0x6D || keyId > 0x74) throw new ArgumentOutOfRangeException(nameof(keyId));
        }

        private static ushort Crc16(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            foreach (byte value in data)
            {
                crc ^= value;
                for (int bit = 0; bit < 8; bit++)
                    crc = (ushort)(((crc & 1) != 0) ? ((crc >> 1) ^ 0xA001) : (crc >> 1));
            }
            return crc;
        }
    }
}
