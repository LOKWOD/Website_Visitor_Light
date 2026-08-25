using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace LOKWOD.VisitorKey
{
    internal sealed class AppSettings
    {
        public string DeviceUrl { get; set; } = "http://lokwod-visitor-light.local";
        public string ProtectedPassword { get; set; } = string.Empty;
        public long LastEventTimestamp { get; set; }
    }

    internal static class SecureSettings
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob { public int Length; public IntPtr Data; }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(ref DataBlob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        internal static string AppDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LOKWOD Visitor Key");
        internal static string SettingsPath => Path.Combine(AppDirectory, "settings.json");
        internal static string BackupPath => Path.Combine(AppDirectory, "key4-original.jpg");
        internal static string LegacyBackupPath => Path.Combine(AppDirectory, "key8-original.jpg");
        internal static string KeyMigrationMarkerPath => Path.Combine(AppDirectory, "key8-restored-for-key4.txt");

        internal static AppSettings Load()
        {
            try
            {
                return File.Exists(SettingsPath)
                    ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings()
                    : new AppSettings();
            }
            catch { return new AppSettings(); }
        }

        internal static void Save(AppSettings settings)
        {
            Directory.CreateDirectory(AppDirectory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }

        internal static string Protect(string value) => Convert.ToBase64String(ProtectBytes(Encoding.UTF8.GetBytes(value)));
        internal static string Unprotect(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : Encoding.UTF8.GetString(UnprotectBytes(Convert.FromBase64String(value)));

        private static byte[] ProtectBytes(byte[] input) => Transform(input, true);
        private static byte[] UnprotectBytes(byte[] input) => Transform(input, false);

        private static byte[] Transform(byte[] input, bool protect)
        {
            var source = new DataBlob { Length = input.Length, Data = Marshal.AllocHGlobal(input.Length) };
            try
            {
                Marshal.Copy(input, 0, source.Data, input.Length);
                DataBlob result;
                bool success = protect
                    ? CryptProtectData(ref source, "LOKWOD Visitor Key", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out result)
                    : CryptUnprotectData(ref source, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out result);
                if (!success) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                try
                {
                    byte[] output = new byte[result.Length];
                    Marshal.Copy(result.Data, output, 0, result.Length);
                    return output;
                }
                finally { LocalFree(result.Data); }
            }
            finally { Marshal.FreeHGlobal(source.Data); }
        }
    }
}
