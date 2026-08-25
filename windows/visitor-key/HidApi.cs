using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace LOKWOD.VisitorKey
{
    internal sealed class HidDeviceInfo
    {
        public string Path { get; set; } = string.Empty;
        public ushort UsagePage { get; set; }
        public ushort Usage { get; set; }
        public int InterfaceNumber { get; set; }
    }

    internal static class HidApi
    {
        private const string Dll = "hidapi.dll";

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeDeviceInfo
        {
            public IntPtr Path;
            public ushort VendorId;
            public ushort ProductId;
            public IntPtr SerialNumber;
            public ushort ReleaseNumber;
            public IntPtr ManufacturerString;
            public IntPtr ProductString;
            public ushort UsagePage;
            public ushort Usage;
            public int InterfaceNumber;
            public IntPtr Next;
        }

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int hid_init();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr hid_enumerate(ushort vendorId, ushort productId);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void hid_free_enumeration(IntPtr devices);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr hid_open_path([MarshalAs(UnmanagedType.LPStr)] string path);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void hid_close(IntPtr device);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int hid_write(IntPtr device, byte[] data, UIntPtr length);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int hid_read_timeout(IntPtr device, byte[] data, UIntPtr length, int milliseconds);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int hid_send_feature_report(IntPtr device, byte[] data, UIntPtr length);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int hid_get_feature_report(IntPtr device, byte[] data, UIntPtr length);

        internal static IReadOnlyList<HidDeviceInfo> Enumerate(ushort vendorId, ushort productId)
        {
            if (hid_init() != 0) throw new InvalidOperationException("hidapi could not initialize.");
            var output = new List<HidDeviceInfo>();
            IntPtr head = hid_enumerate(vendorId, productId);
            try
            {
                for (IntPtr current = head; current != IntPtr.Zero;)
                {
                    NativeDeviceInfo native = Marshal.PtrToStructure<NativeDeviceInfo>(current);
                    output.Add(new HidDeviceInfo
                    {
                        Path = Marshal.PtrToStringAnsi(native.Path) ?? string.Empty,
                        UsagePage = native.UsagePage,
                        Usage = native.Usage,
                        InterfaceNumber = native.InterfaceNumber,
                    });
                    current = native.Next;
                }
            }
            finally
            {
                if (head != IntPtr.Zero) hid_free_enumeration(head);
            }
            return output;
        }

        internal static string FindPath(ushort usagePage, int preferredInterface)
        {
            IReadOnlyList<HidDeviceInfo> devices = Enumerate(DarkMount.VendorId, DarkMount.ProductId);
            foreach (HidDeviceInfo item in devices)
            {
                if (item.UsagePage == usagePage) return item.Path;
            }
            foreach (HidDeviceInfo item in devices)
            {
                if (item.InterfaceNumber == preferredInterface) return item.Path;
            }
            throw new InvalidOperationException($"Dark Mount HID interface 0x{usagePage:X4} was not found.");
        }
    }

    internal static class DarkMount
    {
        internal const ushort VendorId = 0x373F;
        internal const ushort ProductId = 0x0001;
        internal const byte TopRightDisplayKey = 0x74;
        internal static readonly SemaphoreSlim DisplayIoGate = new SemaphoreSlim(1, 1);
    }
}
