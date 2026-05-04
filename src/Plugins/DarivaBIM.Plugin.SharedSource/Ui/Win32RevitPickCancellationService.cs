using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DarivaBIM.Revit.Abstractions.Hosting;

namespace DarivaBIM.Plugin.Ui
{
    /// <summary>
    /// Default <see cref="IRevitPickCancellationService"/> implementation:
    /// brings the Revit window to the foreground and synthesises ESC via
    /// <c>user32.dll</c>. Required because a modeless WPF window with
    /// <c>Topmost=true</c> can swallow the keystroke before Revit sees it,
    /// leaving an in-flight <c>PickObject</c> stuck.
    /// </summary>
    internal sealed class Win32RevitPickCancellationService : IRevitPickCancellationService
    {
        private const byte VK_ESCAPE = 0x1B;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public void CancelPendingPick()
        {
            try
            {
                IntPtr revitHandle = Process.GetCurrentProcess().MainWindowHandle;
                if (revitHandle != IntPtr.Zero)
                {
                    SetForegroundWindow(revitHandle);
                }

                keybd_event(VK_ESCAPE, 0, 0, UIntPtr.Zero);
                keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch
            {
                // Se o sistema bloquear, o loop sai no proximo pick/ESC manual.
            }
        }
    }
}
