using System.Collections.Generic;
using System.Linq;

namespace TecladoConMouse.Nucleo;

public enum TipoTecla { Letra, Simbolo, Mayuscula, Borrar, Enter, Espacio, Capa }

public sealed record Tecla(TipoTecla Tipo, string Etiqueta, string Texto, double X, double Y, double Ancho = 1, double Alto = 1)
{
    public Punto Centro => new(X + Ancho / 2, Y + Alto / 2);

    public bool Contiene(Punto p) => p.X >= X && p.X < X + Ancho && p.Y >= Y && p.Y < Y + Alto;
}

public static class DisposicionTeclado
{
    public const double Columnas = 10;
    public const double Filas = 4;
    public const string Alfabeto = "abcdefghijklmnñopqrstuvwxyz";

    public static IReadOnlyList<Tecla> Letras { get; } =
        Armar("qwertyuiop", "asdfghjklñ", "zxcvbnm", TipoTecla.Letra, "123", izquierdaFila2: null);

    public static IReadOnlyList<Tecla> Simbolos { get; } =
        Armar("1234567890", "@#$%&-+()/", "¿?¡!\":;", TipoTecla.Simbolo, "abc", izquierdaFila2: "'");

    static readonly Dictionary<char, Punto> Centros =
        Letras.Where(t => t.Tipo == TipoTecla.Letra).ToDictionary(t => t.Texto[0], t => t.Centro);

    public static Punto CentroDe(char letra) => Centros[letra];

    public static IEnumerable<KeyValuePair<char, Punto>> CentrosDeLetras => Centros;

    public static Tecla? TeclaEn(IReadOnlyList<Tecla> capa, Punto p) => capa.FirstOrDefault(t => t.Contiene(p));

    static List<Tecla> Armar(string fila0, string fila1, string fila2, TipoTecla tipo, string etiquetaCapa, string? izquierdaFila2)
    {
        var teclas = new List<Tecla>();
        for (int i = 0; i < fila0.Length; i++) teclas.Add(new Tecla(tipo, fila0[i].ToString(), fila0[i].ToString(), i, 0));
        for (int i = 0; i < fila1.Length; i++) teclas.Add(new Tecla(tipo, fila1[i].ToString(), fila1[i].ToString(), i, 1));

        teclas.Add(izquierdaFila2 is null
            ? new Tecla(TipoTecla.Mayuscula, "⇧", "", 0, 2, 1.5)
            : new Tecla(TipoTecla.Simbolo, izquierdaFila2, izquierdaFila2, 0, 2, 1.5));
        for (int i = 0; i < fila2.Length; i++) teclas.Add(new Tecla(tipo, fila2[i].ToString(), fila2[i].ToString(), 1.5 + i, 2));
        teclas.Add(new Tecla(TipoTecla.Borrar, "⌫", "", 8.5, 2, 1.5));

        teclas.Add(new Tecla(TipoTecla.Capa, etiquetaCapa, "", 0, 3, 1.5));
        teclas.Add(new Tecla(TipoTecla.Simbolo, ",", ",", 1.5, 3));
        teclas.Add(new Tecla(TipoTecla.Espacio, "espacio", " ", 2.5, 3, 5));
        teclas.Add(new Tecla(TipoTecla.Simbolo, ".", ".", 7.5, 3));
        teclas.Add(new Tecla(TipoTecla.Enter, "⏎", "", 8.5, 3, 1.5));
        return teclas;
    }
}
