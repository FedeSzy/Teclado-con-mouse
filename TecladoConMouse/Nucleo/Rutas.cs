using System;
using System.IO;

namespace TecladoConMouse.Nucleo;

public static class Rutas
{
    public static string Carpeta => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TecladoConMouse");
    public static string Configuracion => Path.Combine(Carpeta, "configuracion.json");
    public static string PalabrasUsuario => Path.Combine(Carpeta, "palabras_usuario.txt");
    public static string VocabularioPersonal => Path.Combine(Carpeta, "vocabulario_personal.txt");
}
