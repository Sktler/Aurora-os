using System;
using System.Runtime.InteropServices;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Enables the Windows 11 Mica backdrop material on a window, via the same DWM API
    /// File Explorer and Windows Settings use - no NuGet package, just two P/Invoke calls.
    /// Safely no-ops on Windows 10 or older Windows 11 builds that don't support it:
    /// DwmSetWindowAttribute just returns a failure HRESULT there, which this wraps in a
    /// try/catch rather than letting bubble up, so Aurora just keeps its normal flat dark
    /// background on unsupported systems - nothing visibly breaks either way.
    /// </summary>
    public static class MicaHelper
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_MAINWINDOW = 2; // Mica

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void ApplyMica(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                int backdrop = DWMSBT_MAINWINDOW;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
            catch
            {
                // Unsupported OS/build - no Mica, no crash, nothing for the user to notice.
            }
        }
    }
}
