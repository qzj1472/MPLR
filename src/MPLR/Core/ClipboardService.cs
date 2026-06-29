using System.Runtime.InteropServices;
using System.Text;

namespace MPLR.Core;

public static class ClipboardService
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;

    public static async Task<bool> SetTextAsync(string value)
    {
        Task<bool> task = Task.Run(() => TrySetText(value));
        Task completed = await Task.WhenAny(task, Task.Delay(500));
        return completed == task && await task;
    }

    private static bool TrySetText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (int i = 0; i < 6; i++)
        {
            if (TrySetTextOnce(value))
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return false;
    }

    private static bool TrySetTextOnce(string value)
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        IntPtr handle = IntPtr.Zero;
        try
        {
            if (!EmptyClipboard())
            {
                return false;
            }

            byte[] bytes = Encoding.Unicode.GetBytes(value + '\0');
            handle = GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            IntPtr target = GlobalLock(handle);
            if (target == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                Marshal.Copy(bytes, 0, target, bytes.Length);
            }
            finally
            {
                _ = GlobalUnlock(handle);
            }

            if (SetClipboardData(CfUnicodeText, handle) == IntPtr.Zero)
            {
                return false;
            }

            handle = IntPtr.Zero;
            return true;
        }
        finally
        {
            _ = CloseClipboard();
            if (handle != IntPtr.Zero)
            {
                _ = GlobalFree(handle);
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
