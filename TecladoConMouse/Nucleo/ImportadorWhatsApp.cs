using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TecladoConMouse.Nucleo;

public static class ImportadorWhatsApp
{
    const char MarcaAutomatica = '‎';

    static readonly Regex EncabezadoIphone = new(@"^‎?\[[^\]]+\] ([^:]+): (.*)$", RegexOptions.Compiled);
    static readonly Regex EncabezadoAndroid = new(@"^‎?\d{1,2}/\d{1,2}/\d{2,4},? \d{1,2}:\d{2}[^-]{0,8}- ([^:]+): (.*)$", RegexOptions.Compiled);
    static readonly Regex Enlaces = new(@"(https?://|www\.)\S+|\S+@\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex Palabras = new(@"[a-záéíóúüñ]+", RegexOptions.Compiled);
    static readonly Regex LetrasEstiradas = new(@"(.)\1{2,}", RegexOptions.Compiled);

    public static Dictionary<string, int> Autores(IEnumerable<string> lineas)
    {
        var autores = new Dictionary<string, int>();
        foreach (var linea in lineas)
            if (Encabezado(linea) is (string autor, _)) autores[autor] = autores.GetValueOrDefault(autor) + 1;
        return autores;
    }

    public static IEnumerable<string> MensajesDe(IEnumerable<string> lineas, string autor)
    {
        string? actual = null;
        foreach (var linea in lineas)
        {
            if (Encabezado(linea) is (string quien, string texto))
            {
                if (actual is not null) yield return actual;
                actual = quien == autor ? texto : null;
            }
            else if (actual is not null) actual += "\n" + linea;
        }
        if (actual is not null) yield return actual;
    }

    public static Dictionary<string, int> ContarPalabras(IEnumerable<string> mensajes)
    {
        var conteo = new Dictionary<string, int>();
        foreach (var mensaje in mensajes)
        {
            int marca = mensaje.IndexOf(MarcaAutomatica);
            var texto = marca < 0 ? mensaje : mensaje[..marca];
            texto = Enlaces.Replace(texto.ToLowerInvariant(), " ");
            foreach (Match m in Palabras.Matches(texto))
            {
                var palabra = LetrasEstiradas.Replace(m.Value, "$1");
                conteo[palabra] = conteo.GetValueOrDefault(palabra) + 1;
            }
        }
        return conteo;
    }

    public static List<KeyValuePair<string, int>> Vocabulario(Dictionary<string, int> conteo, Diccionario diccionario, int minimoNuevas)
    {
        return conteo
            .Where(kv => diccionario.Buscar(kv.Key) is not null || (kv.Value >= minimoNuevas && kv.Key.Length >= 2))
            .OrderByDescending(kv => kv.Value)
            .ToList();
    }

    static (string Autor, string Texto)? Encabezado(string linea)
    {
        var m = EncabezadoIphone.Match(linea);
        if (!m.Success) m = EncabezadoAndroid.Match(linea);
        return m.Success ? (m.Groups[1].Value, m.Groups[2].Value) : null;
    }
}
