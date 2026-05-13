using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DarivaBIM.Plugin.Ui
{
    /// <summary>
    /// Helpers para mexer no chrome nativo das janelas. Hoje só expõe
    /// <see cref="DisableMinimize(Window)"/>: o WPF não permite esconder
    /// só o botão de minimizar do chrome nativo via XAML — só dá pra
    /// matar tudo (NoResize) ou nada (CanResize). Como queremos manter
    /// maximizar/redimensionar, P/Invoke é o único caminho.
    /// </summary>
    internal static class WindowChromeHelper
    {
        private const int GWL_STYLE = -16;
        private const int WS_MINIMIZEBOX = 0x00020000;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        /// <summary>
        /// Tira o botão de minimizar do chrome nativo sem mexer em
        /// maximize/resize. Deve ser chamado depois do hwnd existir
        /// (no SourceInitialized ou no Loaded).
        /// </summary>
        public static void DisableMinimize(Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            long current = IntPtr.Size == 8
                ? GetWindowLongPtr64(hwnd, GWL_STYLE).ToInt64()
                : GetWindowLong32(hwnd, GWL_STYLE);

            long updated = current & ~WS_MINIMIZEBOX;
            if (updated == current) return;

            if (IntPtr.Size == 8)
                SetWindowLongPtr64(hwnd, GWL_STYLE, new IntPtr(updated));
            else
                SetWindowLong32(hwnd, GWL_STYLE, (int)updated);
        }
    }
}
