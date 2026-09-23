using System.Collections.Generic;
using System.Runtime.InteropServices;
using TecladoConMouse.Nucleo;
using static TecladoConMouse.Win32.Nativo;

namespace TecladoConMouse.Win32;

internal sealed class SalidaTextoWin32 : ISalidaTexto
{
    public void Escribir(string texto)
    {
        var eventos = new List<INPUT>(texto.Length * 2);
        foreach (char c in texto)
        {
            if (c == '\n')
            {
                AgregarTecla(eventos, VK_RETURN);
                continue;
            }
            eventos.Add(EventoTecla(0, c, KEYEVENTF_UNICODE));
            eventos.Add(EventoTecla(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
        }
        Enviar(eventos);
    }

    public void Borrar(int cantidad)
    {
        var eventos = new List<INPUT>(cantidad * 2);
        for (int i = 0; i < cantidad; i++) AgregarTecla(eventos, VK_BACK);
        Enviar(eventos);
    }

    public void Enter()
    {
        var eventos = new List<INPUT>(2);
        AgregarTecla(eventos, VK_RETURN);
        Enviar(eventos);
    }

    static void AgregarTecla(List<INPUT> eventos, ushort vk)
    {
        eventos.Add(EventoTecla(vk, '\0', 0));
        eventos.Add(EventoTecla(vk, '\0', KEYEVENTF_KEYUP));
    }

    static INPUT EventoTecla(ushort vk, char caracter, uint opciones) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, wScan = caracter, dwFlags = opciones } },
    };

    static void Enviar(List<INPUT> eventos)
    {
        if (eventos.Count == 0) return;
        SendInput((uint)eventos.Count, eventos.ToArray(), Marshal.SizeOf<INPUT>());
    }
}
