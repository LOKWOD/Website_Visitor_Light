using System;
using System.IO;

namespace LOKWOD.VisitorKey
{
    internal static class CommandLine
    {
        internal static int Run(string[] args)
        {
            try
            {
                if (args[0].Equals("--probe", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Dark Mount interfaces:");
                    foreach (HidDeviceInfo item in HidApi.Enumerate(DarkMount.VendorId, DarkMount.ProductId))
                        Console.WriteLine($"  interface={item.InterfaceNumber} usagePage=0x{item.UsagePage:X4} usage=0x{item.Usage:X4}");
                    using var display = new DarkMountDisplay();
                    byte[] image = display.ReadImage(DarkMount.VisitorDisplayKey);
                    Console.WriteLine($"Visitor Display Key read successfully: {image.Length} JPEG bytes.");
                    try
                    {
                        using var lamps = new DarkMountLampArray();
                        Console.WriteLine($"LampArray read successfully: {lamps.LampCount} lamps.");
                    }
                    catch (InvalidOperationException)
                    {
                        Console.WriteLine("Optional LampArray interface is not enumerated by this Windows 10 installation.");
                    }
                    return 0;
                }
                if (args[0].Equals("--backup", StringComparison.OrdinalIgnoreCase))
                {
                    using var display = new DarkMountDisplay();
                    Directory.CreateDirectory(SecureSettings.AppDirectory);
                    if (!File.Exists(SecureSettings.BackupPath))
                        File.WriteAllBytes(SecureSettings.BackupPath, display.ReadImage(DarkMount.VisitorDisplayKey));
                    Console.WriteLine(SecureSettings.BackupPath);
                    return 0;
                }
                if (args[0].Equals("--read", StringComparison.OrdinalIgnoreCase) && args.Length == 2)
                {
                    using var display = new DarkMountDisplay();
                    string target = Path.GetFullPath(args[1]);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllBytes(target, display.ReadImage(DarkMount.VisitorDisplayKey));
                    Console.WriteLine(target);
                    return 0;
                }
                if (args[0].Equals("--test-key", StringComparison.OrdinalIgnoreCase))
                {
                    using var display = new DarkMountDisplay();
                    byte[] original = display.ReadImage(DarkMount.VisitorDisplayKey);
                    string backup = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "key4-original.jpg"));
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    if (!File.Exists(backup)) File.WriteAllBytes(backup, original);
                    byte[] test = VisitorIconRenderer.Render(System.Drawing.Color.FromArgb(255, 0, 140), "Dish Gal", false);
                    display.WriteImage(DarkMount.VisitorDisplayKey, test);
                    byte[] verified = display.ReadImage(DarkMount.VisitorDisplayKey);
                    if (!Equal(test, verified)) throw new IOException("Test image verification failed.");
                    Console.WriteLine($"Visitor image verified on display key 4. Backup: {backup}");
                    return 0;
                }
                if (args[0].Equals("--migrate-key", StringComparison.OrdinalIgnoreCase))
                {
                    using var display = new DarkMountDisplay();
                    Directory.CreateDirectory(SecureSettings.AppDirectory);
                    if (!File.Exists(SecureSettings.BackupPath))
                        File.WriteAllBytes(SecureSettings.BackupPath, display.ReadImage(DarkMount.VisitorDisplayKey));
                    if (File.Exists(SecureSettings.LegacyBackupPath) && !File.Exists(SecureSettings.KeyMigrationMarkerPath))
                    {
                        byte[] original = File.ReadAllBytes(SecureSettings.LegacyBackupPath);
                        display.WriteImage(DarkMount.LegacyVisitorDisplayKey, original);
                        if (!Equal(original, display.ReadImage(DarkMount.LegacyVisitorDisplayKey)))
                            throw new IOException("The original key 8 image did not verify after restoring it.");
                        File.WriteAllText(SecureSettings.KeyMigrationMarkerPath, DateTimeOffset.Now.ToString("O"));
                    }
                    Console.WriteLine("Visitor notifications now use display key 4, directly above the previous key.");
                    return 0;
                }
                Console.Error.WriteLine("Usage: LOKWODVisitorKey.exe --probe | --backup | --read PATH | --test-key [BACKUP] | --migrate-key");
                return 2;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
            return true;
        }
    }
}
