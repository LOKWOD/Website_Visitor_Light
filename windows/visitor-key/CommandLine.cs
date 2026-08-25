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
                    byte[] image = display.ReadImage(DarkMount.TopRightDisplayKey);
                    Console.WriteLine($"Top-right Display Key read successfully: {image.Length} JPEG bytes.");
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
                        File.WriteAllBytes(SecureSettings.BackupPath, display.ReadImage(DarkMount.TopRightDisplayKey));
                    Console.WriteLine(SecureSettings.BackupPath);
                    return 0;
                }
                if (args[0].Equals("--read", StringComparison.OrdinalIgnoreCase) && args.Length == 2)
                {
                    using var display = new DarkMountDisplay();
                    string target = Path.GetFullPath(args[1]);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllBytes(target, display.ReadImage(DarkMount.TopRightDisplayKey));
                    Console.WriteLine(target);
                    return 0;
                }
                if (args[0].Equals("--test-key", StringComparison.OrdinalIgnoreCase))
                {
                    using var display = new DarkMountDisplay();
                    byte[] original = display.ReadImage(DarkMount.TopRightDisplayKey);
                    string backup = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "key8-original.jpg"));
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    if (!File.Exists(backup)) File.WriteAllBytes(backup, original);
                    byte[] test = VisitorIconRenderer.Render(System.Drawing.Color.FromArgb(255, 0, 140), "Dish Gal", false);
                    display.WriteImage(DarkMount.TopRightDisplayKey, test);
                    byte[] verified = display.ReadImage(DarkMount.TopRightDisplayKey);
                    if (!Equal(test, verified)) throw new IOException("Test image verification failed.");
                    Console.WriteLine($"Visitor image verified on the top-right key. Backup: {backup}");
                    return 0;
                }
                Console.Error.WriteLine("Usage: LOKWODVisitorKey.exe --probe | --backup | --read PATH | --test-key [BACKUP]");
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
