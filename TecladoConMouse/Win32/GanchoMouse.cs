using System;
using System.Runtime.InteropServices;
using System.Windows;
using static TecladoConMouse.Win32.Nativo;

namespace TecladoConMouse.Win32;

internal sealed class GanchoMouse
{
    const int RepeticionInclinacionMs = 280;

    readonly ProcedimientoBajoNivel procedimiento;
    IntPtr gancho;
    long ultimaInclinacion;

    public GanchoMouse() => procedimiento = ProcesarEvento;

    public IntPtr VentanaPropia { get; set; }
    public bool CapturarBotonesLaterales { get; set; }
    public bool CapturarInclinacion { get; set; }

    public event Action<int, bool>? BotonLateral;

    public event Action<int>? Inclinacion;

    public event Action? ClicAfuera;

    public void Instalar()
    {
        if (gancho == IntPtr.Zero) gancho = SetWindowsHookEx(WH_MOUSE_LL, procedimiento, GetModuleHandle(null), 0);
    }

    public void Desinstalar()
    {
        if (gancho == IntPtr.Zero) return;
        UnhookWindowsHookEx(gancho);
        gancho = IntPtr.Zero;
    }

    IntPtr ProcesarEvento(int codigo, IntPtr wParam, IntPtr lParam)
    {
        if (codigo >= 0)
        {
            int mensaje = wParam.ToInt32();
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            if (CapturarBotonesLaterales && mensaje is WM_XBUTTONDOWN or WM_XBUTTONUP)
            {
                int boton = (int)(info.mouseData >> 16);
                bool presionado = mensaje == WM_XBUTTONDOWN;
                Encolar(() => BotonLateral?.Invoke(boton, presionado));
                return new IntPtr(1);
            }

            if (CapturarInclinacion && mensaje == WM_MOUSEHWHEEL)
            {
                long ahora = Environment.TickCount64;
                if (ahora - ultimaInclinacion > RepeticionInclinacionMs)
                {
                    ultimaInclinacion = ahora;
                    int direccion = (short)(info.mouseData >> 16) > 0 ? 1 : -1;
                    Encolar(() => Inclinacion?.Invoke(direccion));
                }
                return new IntPtr(1);
            }

            if (mensaje is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN
                && GetAncestor(WindowFromPoint(info.pt), GA_ROOT) != VentanaPropia)
            {
                Encolar(() => ClicAfuera?.Invoke());
            }
        }
        return CallNextHookEx(gancho, codigo, wParam, lParam);
    }

    static void Encolar(Action accion) => Application.Current.Dispatcher.BeginInvoke(accion);
}
