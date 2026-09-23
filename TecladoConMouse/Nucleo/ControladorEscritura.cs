using System;
using System.Collections.Generic;
using System.Linq;

namespace TecladoConMouse.Nucleo;

public interface ISalidaTexto
{
    void Escribir(string texto);
    void Borrar(int cantidad);
    void Enter();
}

public enum EstadoMayuscula { Apagada, UnaVez, Fija }

public sealed record Sugerencia(string Etiqueta, bool Resaltada, Action Ejecutar);

public sealed class ControladorEscritura
{
    public const int MaxSugerencias = 4;

    const string PuntuacionPegada = ".,;:?!";
    const string FinDeOracion = ".?!";

    enum ModoMayusculas { Minusculas, Inicial, Mayusculas }

    sealed class PalabraEscrita
    {
        public required List<EntradaPalabra> Opciones { get; init; }
        public required ModoMayusculas Modo { get; init; }
        public required string Enviado { get; set; }
        public int Indice { get; set; }
    }

    readonly Diccionario diccionario;
    readonly Decodificador decodificador;
    readonly ISalidaTexto salida;

    string enCurso = "";
    List<EntradaPalabra> completados = new();
    bool espacioAutomatico;
    PalabraEscrita? ultima;

    public ControladorEscritura(Diccionario diccionario, Decodificador decodificador, ISalidaTexto salida)
    {
        this.diccionario = diccionario;
        this.decodificador = decodificador;
        this.salida = salida;
    }

    public event Action? Cambio;

    public EstadoMayuscula Mayuscula { get; private set; }

    public IReadOnlyList<Sugerencia> Sugerencias { get; private set; } = Array.Empty<Sugerencia>();

    public void Deslizar(IReadOnlyList<Punto> gesto)
    {
        var encontradas = decodificador.Decodificar(gesto, MaxSugerencias);
        if (encontradas.Count == 0) return;

        ConfirmarUltima();
        if (enCurso.Length > 0)
        {
            CerrarPalabraEnCurso();
            salida.Escribir(" ");
        }

        var modo = TomarModo();
        var texto = AplicarModo(encontradas[0].Entrada.Texto, modo) + " ";
        salida.Escribir(texto);
        ultima = new PalabraEscrita { Opciones = encontradas.Select(c => c.Entrada).ToList(), Modo = modo, Enviado = texto };
        espacioAutomatico = true;
        MostrarOpciones();
    }

    public void TocarLetra(string letra)
    {
        ConfirmarUltima();
        var texto = Mayuscula == EstadoMayuscula.Apagada ? letra : letra.ToUpperInvariant();
        if (Mayuscula == EstadoMayuscula.UnaVez) Mayuscula = EstadoMayuscula.Apagada;
        salida.Escribir(texto);
        enCurso += texto;
        espacioAutomatico = false;
        completados = diccionario.Completar(enCurso, MaxSugerencias);
        MostrarCompletados();
    }

    public void TocarSimbolo(string simbolo)
    {
        ConfirmarUltima();
        var desconocida = CerrarPalabraEnCurso();
        if (espacioAutomatico && PuntuacionPegada.Contains(simbolo))
        {
            salida.Borrar(1);
            salida.Escribir(simbolo + " ");
            if (FinDeOracion.Contains(simbolo) && Mayuscula == EstadoMayuscula.Apagada) Mayuscula = EstadoMayuscula.UnaVez;
        }
        else
        {
            salida.Escribir(simbolo);
            espacioAutomatico = false;
        }
        OfrecerGuardar(desconocida);
    }

    public void Espacio()
    {
        ConfirmarUltima();
        var desconocida = CerrarPalabraEnCurso();
        if (espacioAutomatico) espacioAutomatico = false;
        else salida.Escribir(" ");
        OfrecerGuardar(desconocida);
    }

    public void Aceptar()
    {
        if (ultima is null && enCurso.Length > 0 && completados.Count > 0) ElegirCompletado(0);
        else Espacio();
    }

    public void Borrar()
    {
        espacioAutomatico = false;
        if (ultima is not null)
        {
            salida.Borrar(ultima.Enviado.Length);
            if (ultima.Modo == ModoMayusculas.Inicial && Mayuscula == EstadoMayuscula.Apagada) Mayuscula = EstadoMayuscula.UnaVez;
            ultima = null;
            LimpiarSugerencias();
            return;
        }

        salida.Borrar(1);
        if (enCurso.Length > 0)
        {
            enCurso = enCurso[..^1];
            completados = diccionario.Completar(enCurso, MaxSugerencias);
            MostrarCompletados();
        }
        else LimpiarSugerencias();
    }

    public void Enter()
    {
        ConfirmarUltima();
        CerrarPalabraEnCurso();
        salida.Enter();
        espacioAutomatico = false;
        LimpiarSugerencias();
    }

    public void AlternarMayuscula()
    {
        Mayuscula = Mayuscula switch
        {
            EstadoMayuscula.Apagada => EstadoMayuscula.UnaVez,
            EstadoMayuscula.UnaVez => EstadoMayuscula.Fija,
            _ => EstadoMayuscula.Apagada,
        };
        Cambio?.Invoke();
    }

    public void Rotar(int direccion)
    {
        if (ultima is null || ultima.Opciones.Count < 2) return;
        int n = ultima.Opciones.Count;
        Reemplazar(((ultima.Indice + direccion) % n + n) % n);
    }

    public void ReiniciarContexto()
    {
        ConfirmarUltima();
        enCurso = "";
        completados = new();
        espacioAutomatico = false;
        LimpiarSugerencias();
    }

    void ElegirCompletado(int indice)
    {
        if (indice >= completados.Count) return;
        var modo = ModoDe(enCurso);
        var opciones = completados;
        salida.Borrar(enCurso.Length);
        enCurso = "";
        completados = new();

        var texto = AplicarModo(opciones[indice].Texto, modo) + " ";
        salida.Escribir(texto);
        ultima = new PalabraEscrita { Opciones = opciones, Modo = modo, Enviado = texto, Indice = indice };
        espacioAutomatico = true;
        MostrarOpciones();
    }

    void Reemplazar(int indice)
    {
        if (ultima is null || indice >= ultima.Opciones.Count || indice == ultima.Indice) return;
        var texto = AplicarModo(ultima.Opciones[indice].Texto, ultima.Modo) + " ";
        int comun = 0;
        while (comun < texto.Length && comun < ultima.Enviado.Length && texto[comun] == ultima.Enviado[comun]) comun++;
        salida.Borrar(ultima.Enviado.Length - comun);
        salida.Escribir(texto[comun..]);
        ultima.Enviado = texto;
        ultima.Indice = indice;
        MostrarOpciones();
    }

    void ConfirmarUltima()
    {
        if (ultima is null) return;
        diccionario.Aprender(ultima.Opciones[ultima.Indice].Texto);
        ultima = null;
    }

    string? CerrarPalabraEnCurso()
    {
        var palabra = enCurso.ToLowerInvariant();
        enCurso = "";
        completados = new();
        if (palabra.Length < 2 || Diccionario.Normalizar(palabra) is null) return null;
        if (diccionario.Buscar(palabra) is null) return palabra;
        diccionario.Aprender(palabra);
        return null;
    }

    ModoMayusculas TomarModo()
    {
        var modo = Mayuscula switch
        {
            EstadoMayuscula.Fija => ModoMayusculas.Mayusculas,
            EstadoMayuscula.UnaVez => ModoMayusculas.Inicial,
            _ => ModoMayusculas.Minusculas,
        };
        if (Mayuscula == EstadoMayuscula.UnaVez) Mayuscula = EstadoMayuscula.Apagada;
        return modo;
    }

    static ModoMayusculas ModoDe(string escrito)
    {
        if (escrito.Length > 1 && escrito.All(c => !char.IsLower(c))) return ModoMayusculas.Mayusculas;
        if (escrito.Length > 0 && char.IsUpper(escrito[0])) return ModoMayusculas.Inicial;
        return ModoMayusculas.Minusculas;
    }

    static string AplicarModo(string palabra, ModoMayusculas modo) => modo switch
    {
        ModoMayusculas.Mayusculas => palabra.ToUpperInvariant(),
        ModoMayusculas.Inicial => char.ToUpperInvariant(palabra[0]) + palabra[1..],
        _ => palabra,
    };

    void MostrarOpciones()
    {
        var escrita = ultima!;
        Sugerencias = escrita.Opciones
            .Select((e, i) => new Sugerencia(AplicarModo(e.Texto, escrita.Modo), i == escrita.Indice, () => Reemplazar(i)))
            .ToList();
        Cambio?.Invoke();
    }

    void MostrarCompletados()
    {
        var modo = ModoDe(enCurso);
        Sugerencias = completados
            .Select((e, i) => new Sugerencia(AplicarModo(e.Texto, modo), i == 0, () => ElegirCompletado(i)))
            .ToList();
        Cambio?.Invoke();
    }

    void OfrecerGuardar(string? palabra)
    {
        if (palabra is null)
        {
            LimpiarSugerencias();
            return;
        }
        Sugerencias = new[] { new Sugerencia($"＋ guardar «{palabra}»", false, () => { diccionario.Aprender(palabra); LimpiarSugerencias(); }) };
        Cambio?.Invoke();
    }

    void LimpiarSugerencias()
    {
        Sugerencias = Array.Empty<Sugerencia>();
        Cambio?.Invoke();
    }
}
