using System;
using System.Runtime.InteropServices;

namespace TecladoConMouse.Win32;

internal static class Nativo
{
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_NOACTIVATE = 0x08000000L;
    public const long WS_EX_TOOLWINDOW = 0x00000080L;

    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int MA_NOACTIVATE = 3;
    public const int WM_HOTKEY = 0x0312;

    public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint GA_ROOT = 2;

    public const int WH_MOUSE_LL = 14;
    public const int WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204, WM_MBUTTONDOWN = 0x0207;
    public const int WM_XBUTTONDOWN = 0x020B, WM_XBUTTONUP = 0x020C, WM_MOUSEHWHEEL = 0x020E;

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x2, KEYEVENTF_UNICODE = 0x4;
    public const ushort VK_BACK = 0x08, VK_RETURN = 0x0D;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr ProcedimientoBajoNivel(int codigo, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr ventana, int indice);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern IntPtr SetWindowLongPtr(IntPtr ventana, int indice, IntPtr valor);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr ventana, int id, uint modificadores, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr ventana, int id);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT punto);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT punto, uint opciones);

    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr ventana, out RECT rectangulo);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr ventana, IntPtr insertarDespuesDe, int x, int y, int ancho, int alto, uint opciones);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT punto);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr ventana, uint opciones);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint cantidad, INPUT[] entradas, int tamanio);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int tipoGancho, ProcedimientoBajoNivel procedimiento, IntPtr modulo, uint hilo);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr gancho);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr gancho, int codigo, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? nombreModulo);
}
