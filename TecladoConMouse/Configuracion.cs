using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TecladoConMouse.Nucleo;

namespace TecladoConMouse;

public sealed class Configuracion
{
    public double AnchoTecla { get; set; } = 46;
    public double AltoTecla { get; set; } = 50;
    public double Opacidad { get; set; } = 1.0;

    public List<string> AtajosAbrir { get; set; } = new() { "Ctrl+Alt+K", "F13" };

    public string AtajoAceptar { get; set; } = "F14";
    public string AtajoBorrar { get; set; } = "F15";
    public string AtajoEnter { get; set; } = "F16";
    public string AtajoSiguienteSugerencia { get; set; } = "F17";

    public bool BotonesLaterales { get; set; } = true;

    public bool InclinacionRueda { get; set; } = true;

    public bool OcultarDespuesDeEnter { get; set; } = false;

    public static Configuracion Cargar()
    {
        if (!File.Exists(Rutas.Configuracion))
        {
            var predeterminada = new Configuracion();
            predeterminada.Guardar();
            return predeterminada;
        }
        try
        {
            return JsonSerializer.Deserialize<Configuracion>(File.ReadAllText(Rutas.Configuracion)) ?? new Configuracion();
        }
        catch (JsonException)
        {
            return new Configuracion();
        }
    }

    void Guardar()
    {
        Directory.CreateDirectory(Rutas.Carpeta);
        File.WriteAllText(Rutas.Configuracion, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
