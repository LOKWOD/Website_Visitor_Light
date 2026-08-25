using System;
using System.IO;

namespace LOKWOD.VisitorKey
{
    internal static class AppLog
    {
        private const long MaxLogBytes = 1024 * 1024;
        private static readonly object Sync = new object();

        internal static string Path => System.IO.Path.Combine(SecureSettings.AppDirectory, "visitor-key.log");

        internal static void Write(string message, Exception? exception = null)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(SecureSettings.AppDirectory);
                    RotateIfNeeded();
                    string detail = exception == null ? string.Empty : Environment.NewLine + exception;
                    File.AppendAllText(Path, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}  {message}{detail}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never be able to stop the tray companion.
            }
        }

        private static void RotateIfNeeded()
        {
            var current = new FileInfo(Path);
            if (!current.Exists || current.Length < MaxLogBytes) return;

            string previous = System.IO.Path.Combine(SecureSettings.AppDirectory, "visitor-key.previous.log");
            if (File.Exists(previous)) File.Delete(previous);
            File.Move(Path, previous);
        }
    }
}
