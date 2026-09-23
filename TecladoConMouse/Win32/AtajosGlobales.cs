using System;
using System.Collections.Generic;
using System.Windows.Input;
using static TecladoConMouse.Win32.Nativo;

namespace TecladoConMouse.Win32;

internal sealed class AtajosGlobales
{
    readonly IntPtr hwnd;
    readonly Dictionary<int, Action> acciones = new();
    int siguienteId = 1;

    public AtajosGlobales(IntPtr hwnd) => this.hwnd = hwnd;

    public int? Registrar(string atajo, Action accion)
    {
        if (!Interpretar(atajo, out var modificadores, out var vk)) return null;
        int id = siguienteId++;
        if (!RegisterHotKey(hwnd, id, modificadores | MOD_NOREPEAT, vk)) return null;
        acciones[id] = accion;
        return id;
    }

    public void Quitar(int id)
    {
        UnregisterHotKey(hwnd, id);
        acciones.Remove(id);
    }

    public bool Procesar(IntPtr wParam)
    {
        if (!acciones.TryGetValue(wParam.ToInt32(), out var accion)) return false;
        accion();
        return true;
    }

    static bool Interpretar(string texto, out uint modificadores, out uint vk)
    {
        modificadores = 0;
        vk = 0;
        var partes = texto.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0) return false;

        foreach (var parte in partes[..^1])
        {
            switch (parte.ToLowerInvariant())
            {
                case "ctrl": case "control": modificadores |= MOD_CONTROL; break;
                case "alt": modificadores |= MOD_ALT; break;
                case "shift": modificadores |= MOD_SHIFT; break;
                case "win": modificadores |= MOD_WIN; break;
                default: return false;
            }
        }

        var nombreTecla = partes[^1];
        if (nombreTecla.Length == 1 && char.IsDigit(nombreTecla[0])) nombreTecla = "D" + nombreTecla;
        if (!Enum.TryParse<Key>(nombreTecla, ignoreCase: true, out var tecla) || int.TryParse(nombreTecla, out _)) return false;
        vk = (uint)KeyInterop.VirtualKeyFromKey(tecla);
        return vk != 0;
    }
}
