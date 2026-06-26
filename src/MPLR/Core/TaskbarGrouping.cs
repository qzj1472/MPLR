using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MPLR.Core;

internal static class TaskbarGrouping
{
    private const string AppUserModelId = "qzj1472.MPLR";
    private static readonly Guid IPropertyStoreId = new("00000138-0000-0000-C000-000000000046");
    private static readonly Guid AppUserModelFormatId = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");

    public static void SetCurrentProcessAppId()
    {
        _ = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
    }

    public static bool SetPreviewWindowAssociation(IntPtr handle, Process process)
    {
        if (handle == IntPtr.Zero || process.HasExited || !IsMplrChildPreviewWindow(handle, process))
        {
            return false;
        }

        Guid propertyStoreId = IPropertyStoreId;
        int result = SHGetPropertyStoreForWindow(handle, ref propertyStoreId, out IPropertyStore? propertyStore);
        if (result != 0 || propertyStore == null)
        {
            return false;
        }

        try
        {
            return SetStringValue(propertyStore, AppUserModelProperty.Id, AppUserModelId) &&
                   SetStringValue(propertyStore, AppUserModelProperty.RelaunchCommand, Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty) &&
                   SetStringValue(propertyStore, AppUserModelProperty.RelaunchDisplayNameResource, AppConfig.DisplayName) &&
                   SetStringValue(propertyStore, AppUserModelProperty.RelaunchIconResource, $"{Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty},0") &&
                   propertyStore.Commit() == 0;
        }
        finally
        {
            Marshal.ReleaseComObject(propertyStore);
        }
    }

    private static bool SetStringValue(IPropertyStore propertyStore, AppUserModelProperty property, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        PropertyKey propertyKey = CreatePropertyKey(property);
        PropVariant propVariant = PropVariant.FromString(value);
        try
        {
            return propertyStore.SetValue(ref propertyKey, ref propVariant) == 0;
        }
        finally
        {
            _ = PropVariantClear(ref propVariant);
        }
    }

    private static PropertyKey CreatePropertyKey(AppUserModelProperty property)
    {
        return new PropertyKey()
        {
            FormatId = AppUserModelFormatId,
            PropertyId = (int)property,
        };
    }

    private static bool IsMplrChildPreviewWindow(IntPtr handle, Process process)
    {
        if (!GetWindowThreadProcessId(handle, out int windowProcessId) || windowProcessId != process.Id)
        {
            return false;
        }

        int? parentProcessId = Interop.GetParentProcessId(process.Id);
        return parentProcessId == Environment.ProcessId;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? propertyStore);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);

    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId", SetLastError = true)]
    private static extern uint GetWindowThreadProcessIdNative(IntPtr hWnd, out int lpdwProcessId);

    private static bool GetWindowThreadProcessId(IntPtr handle, out int processId)
    {
        uint threadId = GetWindowThreadProcessIdNative(handle, out processId);
        return threadId != 0;
    }

    private enum AppUserModelProperty
    {
        RelaunchCommand = 2,
        RelaunchIconResource = 3,
        RelaunchDisplayNameResource = 4,
        Id = 5,
    }

    [ComImport]
    [Guid("00000138-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint propertyCount);

        [PreserveSig]
        int GetAt(uint propertyIndex, out PropertyKey key);

        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);

        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant value);

        [PreserveSig]
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;

        public int PropertyId;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        public ushort VariantType;

        [FieldOffset(8)]
        public IntPtr Pointer;

        public static PropVariant FromString(string value)
        {
            return new PropVariant()
            {
                VariantType = 31,
                Pointer = Marshal.StringToCoTaskMemUni(value),
            };
        }
    }
}
