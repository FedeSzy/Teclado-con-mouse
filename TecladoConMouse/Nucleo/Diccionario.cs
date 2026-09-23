using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TecladoConMouse.Nucleo;

public sealed class EntradaPalabra
{
    internal EntradaPalabra(string texto, string teclas, double frecuenciaBase)
    {
        Texto = texto;
        Teclas = teclas;
        Recorrido = ColapsarRepetidas(teclas);
        FrecuenciaBase = frecuenciaBase;
    }

    public string Texto { get; }

    public string Teclas { get; }

    public string Recorrido { get; }

    public double FrecuenciaBase { get; }

    public int UsosUsuario { get; internal set; }

    public int UsosImportados { get; internal set; }

    public double Frecuencia => FrecuenciaBase + (UsosUsuario + UsosImportados) * Diccionario.BonusPorUso;

    internal Punto[]? Plantilla;
    internal Punto[]? PlantillaForma;
    internal double LongitudPlantilla;

    static string ColapsarRepetidas(string teclas)
    {
        var sb = new StringBuilder(teclas.Length);
        foreach (char c in teclas)
            if (sb.Length == 0 || sb[^1] != c) sb.Append(c);
        return sb.ToString();
    }
}

public sealed class Diccionario
{
    public const double BonusPorUso = 3000;

    const string PalabrasDeUnaLetra = "aeouy";

    readonly List<EntradaPalabra> entradas = new();
    readonly Dictionary<string, EntradaPalabra> porTexto = new();
    readonly List<EntradaPalabra>?[] grupos = new List<EntradaPalabra>?[DisposicionTeclado.Alfabeto.Length * DisposicionTeclado.Alfabeto.Length];
    string? rutaUsuario;

    public int Cantidad => entradas.Count;

    public IReadOnlyList<EntradaPalabra> Entradas => entradas;

    public static string? Normalizar(string palabra)
    {
        var sb = new StringBuilder(palabra.Length);
        foreach (char original in palabra)
        {
            char c = char.ToLowerInvariant(original) switch
            {
                'á' => 'a', 'é' => 'e', 'í' => 'i', 'ó' => 'o', 'ú' => 'u', 'ü' => 'u',
                var otra => otra,
            };
            if (DisposicionTeclado.Alfabeto.IndexOf(c) < 0) return null;
            sb.Append(c);
        }
        return sb.ToString();
    }

    public void CargarBase(string ruta)
    {
        foreach (var linea in File.ReadLines(ruta, Encoding.UTF8))
        {
            int espacio = linea.LastIndexOf(' ');
            if (espacio <= 0) continue;
            if (!double.TryParse(linea.AsSpan(espacio + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var frecuencia)) continue;
            Agregar(linea[..espacio], frecuencia);
        }
    }

    public void CargarUsuario(string ruta)
    {
        rutaUsuario = ruta;
        foreach (var (palabra, usos) in LeerConteos(ruta))
        {
            var entrada = Buscar(palabra) ?? Agregar(palabra, 0);
            if (entrada is not null) entrada.UsosUsuario = usos;
        }
    }

    public void CargarVocabulario(string ruta)
    {
        foreach (var (palabra, usos) in LeerConteos(ruta))
        {
            var entrada = Buscar(palabra) ?? Agregar(palabra, 0);
            if (entrada is not null) entrada.UsosImportados = usos;
        }
    }

    public static void GuardarVocabulario(string ruta, IEnumerable<KeyValuePair<string, int>> usos)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllLines(ruta, usos.Select(kv => $"{kv.Key}\t{kv.Value}"), Encoding.UTF8);
    }

    static IEnumerable<(string Palabra, int Usos)> LeerConteos(string ruta)
    {
        if (!File.Exists(ruta)) yield break;
        foreach (var linea in File.ReadLines(ruta, Encoding.UTF8))
        {
            var partes = linea.Split('\t');
            if (partes.Length == 2 && int.TryParse(partes[1], out var usos)) yield return (partes[0], usos);
        }
    }

    public EntradaPalabra? Buscar(string palabra) => porTexto.GetValueOrDefault(palabra.ToLowerInvariant());

    public void Aprender(string palabra)
    {
        var entrada = Buscar(palabra) ?? Agregar(palabra, 0);
        if (entrada is null) return;
        entrada.UsosUsuario++;
        GuardarUsuario();
    }

    public IEnumerable<EntradaPalabra> Candidatas(IReadOnlyList<char> primeras, IReadOnlyList<char> ultimas)
    {
        foreach (char primera in primeras)
            foreach (char ultima in ultimas)
            {
                var grupo = grupos[IndiceGrupo(primera, ultima)];
                if (grupo is null) continue;
                foreach (var entrada in grupo) yield return entrada;
            }
    }

    public List<EntradaPalabra> Completar(string prefijo, int maximo)
    {
        var mejores = new List<EntradaPalabra>(maximo + 1);
        var teclas = Normalizar(prefijo);
        if (string.IsNullOrEmpty(teclas)) return mejores;

        foreach (var entrada in entradas)
        {
            if (!entrada.Teclas.StartsWith(teclas, StringComparison.Ordinal)) continue;
            double frecuencia = entrada.Frecuencia;
            if (mejores.Count == maximo && frecuencia <= mejores[^1].Frecuencia) continue;
            int i = mejores.FindIndex(m => m.Frecuencia < frecuencia);
            mejores.Insert(i < 0 ? mejores.Count : i, entrada);
            if (mejores.Count > maximo) mejores.RemoveAt(maximo);
        }
        return mejores;
    }

    EntradaPalabra? Agregar(string palabra, double frecuencia)
    {
        palabra = palabra.ToLowerInvariant();
        if (porTexto.TryGetValue(palabra, out var existente)) return existente;
        var teclas = Normalizar(palabra);
        if (string.IsNullOrEmpty(teclas)) return null;
        if (teclas.Length == 1 && PalabrasDeUnaLetra.IndexOf(teclas[0]) < 0) return null;

        var entrada = new EntradaPalabra(palabra, teclas, frecuencia);
        entradas.Add(entrada);
        porTexto[palabra] = entrada;
        (grupos[IndiceGrupo(teclas[0], teclas[^1])] ??= new()).Add(entrada);
        return entrada;
    }

    void GuardarUsuario()
    {
        if (rutaUsuario is null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(rutaUsuario)!);
        File.WriteAllLines(rutaUsuario, entradas.Where(e => e.UsosUsuario > 0).Select(e => $"{e.Texto}\t{e.UsosUsuario}"), Encoding.UTF8);
    }

    static int IndiceGrupo(char primera, char ultima) =>
        DisposicionTeclado.Alfabeto.IndexOf(primera) * DisposicionTeclado.Alfabeto.Length + DisposicionTeclado.Alfabeto.IndexOf(ultima);
}
