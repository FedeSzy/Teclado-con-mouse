using System.Diagnostics;
using TecladoConMouse.Nucleo;

var diccionario = new Diccionario();
diccionario.CargarBase(Path.Combine(AppContext.BaseDirectory, "es_50k.txt"));
Console.WriteLine($"Diccionario: {diccionario.Cantidad} palabras");

if (args.Contains("ajustar"))
{
    Ajustar(diccionario);
    return 0;
}

if (args.Length == 2 && args[0] == "depurar")
{
    var entrada = diccionario.Buscar(args[1])!;
    foreach (var ruido in new[] { 0.0, 0.25 })
    {
        var trazo = Simulador.Deslizar(entrada.Recorrido, new Random(1), ruido);
        Console.WriteLine($"ruido {ruido}: " + string.Join(", ",
            new Decodificador(diccionario).Decodificar(trazo, 6).Select(c => $"{c.Entrada.Texto} {c.Puntaje:0.00} (f={c.Entrada.Frecuencia})")));
    }
    return 0;
}

if (args.Contains("vocabulario"))
{
    int posicion = Array.IndexOf(args, "vocabulario");
    CompararVocabulario(diccionario, posicion + 1 < args.Length ? args[posicion + 1] : Rutas.VocabularioPersonal);
    return 0;
}

int fallas = PruebasControlador.Correr(diccionario);
fallas += PruebasImportador(diccionario);
Evaluar(new Decodificador(diccionario), diccionario, muestras: 1500, ruido: 0.25, detallado: true);
Evaluar(new Decodificador(diccionario), diccionario, muestras: 1500, ruido: 0.35, detallado: true);
fallas += PruebasPalabras(new Decodificador(diccionario), diccionario);
return fallas == 0 ? 0 : 1;

static int PruebasImportador(Diccionario diccionario)
{
    var chat = new[]
    {
        "[1/2/2024, 10:00:00] Fede Szymsio: Holaaaa como andas?",
        "[1/2/2024, 10:00:05] May 🤎: bien vos",
        "[1/2/2024, 10:00:09] Fede Szymsio: ‎image omitted",
        "[1/2/2024, 10:01:00] Fede Szymsio: mira esto https://ejemplo.com/Casa",
        "segunda linea jaja",
        "[1/2/2024, 10:02:00] Fede Szymsio: todo bien ‎<This message was edited>",
        "01/02/24, 10:03 - Fede Szymsio: jaja",
    };
    var autores = ImportadorWhatsApp.Autores(chat);
    var conteo = ImportadorWhatsApp.ContarPalabras(ImportadorWhatsApp.MensajesDe(chat, "Fede Szymsio"));
    var obtenido = string.Join(" ", conteo.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}:{kv.Value}"));
    const string esperado = "andas:1 bien:1 como:1 esto:1 hola:1 jaja:2 linea:1 mira:1 segunda:1 todo:1";
    bool ok = obtenido == esperado && autores.Count == 2;
    Console.WriteLine($"{(ok ? "OK  " : "FALLA")} importar chat de WhatsApp: {obtenido}{(ok ? "" : $" (esperado {esperado}, autores {autores.Count})")}");

    var vocabulario = ImportadorWhatsApp.Vocabulario(new Dictionary<string, int> { ["hola"] = 1, ["zzqx"] = 2, ["bebu"] = 3 }, diccionario, 3);
    var claves = string.Join(",", vocabulario.Select(kv => kv.Key));
    bool okFiltro = claves == "bebu,hola";
    Console.WriteLine($"{(okFiltro ? "OK  " : "FALLA")} palabras nuevas solo con 3+ usos: {claves}");
    return (ok ? 0 : 1) + (okFiltro ? 0 : 1);
}

static void CompararVocabulario(Diccionario diccionarioBase, string rutaVocabulario)
{
    var personal = new Diccionario();
    personal.CargarBase(Path.Combine(AppContext.BaseDirectory, "es_50k.txt"));
    personal.CargarVocabulario(rutaVocabulario);

    var usadas = personal.Entradas.Where(e => e.UsosImportados > 0 && e.Recorrido.Length >= 2).ToList();
    var acumulada = new double[usadas.Count];
    double total = 0;
    for (int i = 0; i < usadas.Count; i++) acumulada[i] = total += usadas[i].UsosImportados;

    var decodificadorBase = new Decodificador(diccionarioBase);
    var decodificadorPersonal = new Decodificador(personal);
    var azar = new Random(7);
    const int Muestras = 2000;
    int aciertosBase = 0, aciertosPersonal = 0;
    for (int m = 0; m < Muestras; m++)
    {
        int indice = Array.BinarySearch(acumulada, azar.NextDouble() * total);
        var objetivo = usadas[indice < 0 ? ~indice : indice];
        var trazo = Simulador.Deslizar(objetivo.Recorrido, azar, 0.3);
        var conBase = decodificadorBase.Decodificar(trazo, 1);
        var conPersonal = decodificadorPersonal.Decodificar(trazo, 1);
        if (conBase.Count > 0 && conBase[0].Entrada.Texto == objetivo.Texto) aciertosBase++;
        if (conPersonal.Count > 0 && conPersonal[0].Entrada.Texto == objetivo.Texto) aciertosPersonal++;
    }
    Console.WriteLine($"Deslizando tus palabras con la frecuencia con que las usás ({Muestras} trazos simulados):");
    Console.WriteLine($"  sin tu vocabulario: {100.0 * aciertosBase / Muestras:0.0}% primera");
    Console.WriteLine($"  con tu vocabulario: {100.0 * aciertosPersonal / Muestras:0.0}% primera");
}

static int PruebasPalabras(Decodificador decodificador, Diccionario diccionario)
{
    const int Intentos = 100;
    const double MinimoPrimera = 0.8;
    int fallas = 0;
    foreach (var palabra in new[] { "rueda" })
    {
        var entrada = diccionario.Buscar(palabra)!;
        var azar = new Random(99);
        var equivocadas = new Dictionary<string, int>();
        int primeras = 0;
        for (int t = 0; t < Intentos; t++)
        {
            var mejor = decodificador.Decodificar(Simulador.Deslizar(entrada.Recorrido, azar, 0.25), 1);
            if (mejor.Count > 0 && mejor[0].Entrada == entrada) primeras++;
            else if (mejor.Count > 0) equivocadas[mejor[0].Entrada.Texto] = equivocadas.GetValueOrDefault(mejor[0].Entrada.Texto) + 1;
        }
        bool ok = primeras >= Intentos * MinimoPrimera;
        if (!ok) fallas++;
        Console.WriteLine($"{(ok ? "OK  " : "FALLA")} «{palabra}» primera en {primeras}/{Intentos}. En su lugar: "
            + string.Join(", ", equivocadas.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")));
    }
    return fallas;
}

static (double Primera, double Top4) Evaluar(Decodificador decodificador, Diccionario diccionario, int muestras, double ruido, bool detallado)
{
    var azar = new Random(1234);
    var conjunto = diccionario.Entradas.Take(20000).Where(e => e.Recorrido.Length >= 2).ToList();
    var acumulada = new double[conjunto.Count];
    double total = 0;
    for (int i = 0; i < conjunto.Count; i++) acumulada[i] = total += Math.Sqrt(conjunto[i].Frecuencia);

    int primera = 0, top4 = 0;
    var errores = new List<string>();
    var cronometro = Stopwatch.StartNew();
    for (int m = 0; m < muestras; m++)
    {
        int indice = Array.BinarySearch(acumulada, azar.NextDouble() * total);
        var objetivo = conjunto[indice < 0 ? ~indice : indice];
        var resultado = decodificador.Decodificar(Simulador.Deslizar(objetivo.Recorrido, azar, ruido), ControladorEscritura.MaxSugerencias);
        int posicion = resultado.FindIndex(c => c.Entrada == objetivo);
        if (posicion == 0) primera++;
        if (posicion >= 0) top4++;
        else if (errores.Count < 12) errores.Add($"{objetivo.Texto}→{(resultado.Count > 0 ? resultado[0].Entrada.Texto : "∅")}");
    }
    double ms = cronometro.Elapsed.TotalMilliseconds / muestras;
    if (detallado)
    {
        Console.WriteLine($"Ruido {ruido:0.00} teclas: top-1 {100.0 * primera / muestras:0.0}%  top-4 {100.0 * top4 / muestras:0.0}%  ({ms:0.0} ms/deslizamiento)");
        Console.WriteLine("  fuera del top-4: " + string.Join(", ", errores));
    }
    return (100.0 * primera / muestras, 100.0 * top4 / muestras);
}

static void Ajustar(Diccionario diccionario)
{
    var resultados = new List<(double Puntaje, string Parametros)>();
    foreach (var frecuencia in new[] { 0.2, 0.25, 0.35 })
    foreach (var ubicacion in new[] { 0.22, 0.27, 0.35 })
    foreach (var inicio in new[] { 0.25, 0.35, 0.5 })
    foreach (var fin in new[] { 0.45, 0.6, 1.0 })
    {
        var decodificador = new Decodificador(diccionario) { PesoFrecuencia = frecuencia, SigmaUbicacion = ubicacion, SigmaInicio = inicio, SigmaFin = fin };
        var a = Evaluar(decodificador, diccionario, 400, 0.25, false);
        var b = Evaluar(decodificador, diccionario, 400, 0.35, false);
        double puntaje = (a.Primera + b.Primera) / 2;
        var parametros = $"frecuencia={frecuencia} ubicacion={ubicacion} inicio={inicio} fin={fin}";
        resultados.Add((puntaje, parametros));
        Console.WriteLine($"{puntaje:0.0}%  top4 {(a.Top4 + b.Top4) / 2:0.0}%  {parametros}");
    }
    Console.WriteLine("\nMejores:");
    foreach (var r in resultados.OrderByDescending(r => r.Puntaje).Take(5)) Console.WriteLine($"{r.Puntaje:0.0}%  {r.Parametros}");
}

static class Simulador
{
    const double RuidoInicio = 0.15;

    public static List<Punto> Deslizar(string recorrido, Random azar, double ruido)
    {
        var objetivos = recorrido.Select((c, i) =>
        {
            var p = DisposicionTeclado.CentroDe(c);
            double r = i == 0 ? Math.Min(ruido, RuidoInicio) : ruido;
            return new Punto(p.X + Gauss(azar) * r, p.Y + Gauss(azar) * r * 0.8);
        }).ToList();

        var suavizado = RedondearEsquinas(objetivos, 0.3);
        var resultado = new List<Punto>();
        for (int i = 1; i < suavizado.Count; i++)
        {
            var a = suavizado[i - 1];
            var b = suavizado[i];
            int pasos = Math.Max(1, (int)(Punto.Distancia(a, b) / 0.08));
            for (int s = 0; s < pasos; s++)
            {
                var q = a + (b - a) * ((double)s / pasos);
                resultado.Add(new Punto(q.X + Gauss(azar) * 0.02, q.Y + Gauss(azar) * 0.02));
            }
        }
        resultado.Add(suavizado[^1]);
        return resultado;
    }

    static List<Punto> RedondearEsquinas(List<Punto> puntos, double radio)
    {
        var resultado = new List<Punto> { puntos[0] };
        for (int i = 1; i < puntos.Count - 1; i++)
        {
            var (anterior, esquina, siguiente) = (puntos[i - 1], puntos[i], puntos[i + 1]);
            double dEntrada = Punto.Distancia(anterior, esquina), dSalida = Punto.Distancia(esquina, siguiente);
            if (dEntrada < 1e-9 || dSalida < 1e-9) { resultado.Add(esquina); continue; }
            var entrada = esquina + (anterior - esquina) * (Math.Min(radio, dEntrada * 0.25) / dEntrada);
            var salida = esquina + (siguiente - esquina) * (Math.Min(radio, dSalida * 0.25) / dSalida);
            resultado.Add(entrada);
            resultado.Add(entrada * 0.25 + esquina * 0.5 + salida * 0.25);
            resultado.Add(salida);
        }
        resultado.Add(puntos[^1]);
        return resultado;
    }

    static double Gauss(Random azar) =>
        Math.Sqrt(-2 * Math.Log(1 - azar.NextDouble())) * Math.Cos(2 * Math.PI * azar.NextDouble());
}

sealed class SalidaFalsa : ISalidaTexto
{
    public string Texto { get; private set; } = "";
    public void Escribir(string texto) => Texto += texto;
    public void Borrar(int cantidad) => Texto = Texto[..Math.Max(0, Texto.Length - cantidad)];
    public void Enter() => Texto += "\n";
}

static class PruebasControlador
{
    public static int Correr(Diccionario diccionario)
    {
        int fallas = 0;
        void Verificar(string nombre, string esperado, string obtenido)
        {
            bool ok = esperado == obtenido;
            if (!ok) fallas++;
            Console.WriteLine($"{(ok ? "OK  " : "FALLA")} {nombre}: {Mostrar(obtenido)}{(ok ? "" : $" (esperado {Mostrar(esperado)})")}");
        }
        static string Mostrar(string s) => "\"" + s.Replace("\n", "\\n") + "\"";
        static List<Punto> Exacto(string palabra) => palabra.Select(DisposicionTeclado.CentroDe).ToList();

        (ControladorEscritura, SalidaFalsa) Nuevo()
        {
            var salida = new SalidaFalsa();
            return (new ControladorEscritura(diccionario, new Decodificador(diccionario), salida), salida);
        }

        {
            var (c, s) = Nuevo();
            c.Deslizar(Exacto("hola"));
            c.Deslizar(Exacto("mundo"));
            Verificar("dos palabras deslizadas", "hola mundo ", s.Texto);
            c.TocarSimbolo(",");
            Verificar("la coma se pega a la palabra", "hola mundo, ", s.Texto);
            c.Borrar();
            c.Borrar();
            Verificar("borrar después de la coma", "hola mundo", s.Texto);
        }
        {
            var (c, s) = Nuevo();
            c.Deslizar(Exacto("casa"));
            c.Borrar();
            Verificar("borrar una palabra deslizada entera", "", s.Texto);
        }
        {
            var (c, s) = Nuevo();
            c.Deslizar(Exacto("esta"));
            var antes = s.Texto;
            c.Rotar(1);
            Verificar("alternativa reemplaza la palabra", c.Sugerencias.First(x => x.Resaltada).Etiqueta + " ", s.Texto);
            Verificar("…y es distinta a la primera", "True", (antes != s.Texto).ToString());
        }
        {
            var (c, s) = Nuevo();
            c.TocarLetra("q");
            c.TocarLetra("u");
            c.Aceptar();
            Verificar("tocar letras y aceptar sugerencia", "que ", s.Texto);
            c.Espacio();
            Verificar("espacio después de palabra con espacio automático no duplica", "que ", s.Texto);
        }
        {
            var (c, s) = Nuevo();
            c.AlternarMayuscula();
            c.Deslizar(Exacto("buenos"));
            c.Deslizar(Exacto("dias"));
            c.TocarSimbolo(".");
            c.Deslizar(Exacto("como"));
            Verificar("mayúsculas y punto", "Buenos días. Como ", s.Texto);
        }
        {
            var (c, s) = Nuevo();
            foreach (var letra in "xqzw") c.TocarLetra(letra.ToString());
            c.Espacio();
            Verificar("palabra desconocida ofrece guardarse", "True", c.Sugerencias.Count == 1 && c.Sugerencias[0].Etiqueta.Contains("xqzw") ? "True" : "False");
            Verificar("…sin guardarla sola", "False", (diccionario.Buscar("xqzw") is not null).ToString());
        }
        return fallas;
    }
}
