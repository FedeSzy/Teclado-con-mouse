using System.Text;
using TecladoConMouse.Nucleo;

const int MinimoPalabrasNuevas = 3;

if (args.Length == 0)
{
    Console.WriteLine("Uso: dotnet run --project TecladoConMouse.Entrenador -- <chat.txt> \"Tu nombre\"");
    return 1;
}

var lineas = File.ReadAllLines(args[0], Encoding.UTF8);
var autores = ImportadorWhatsApp.Autores(lineas);
if (args.Length < 2 || !autores.ContainsKey(args[1]))
{
    Console.WriteLine("Indicá tu nombre tal como aparece en el chat. Participantes:");
    foreach (var (autor, mensajes) in autores.OrderByDescending(a => a.Value)) Console.WriteLine($"  \"{autor}\" ({mensajes} mensajes)");
    return 1;
}

var diccionario = new Diccionario();
diccionario.CargarBase(Path.Combine(AppContext.BaseDirectory, "es_50k.txt"));

var mensajesPropios = ImportadorWhatsApp.MensajesDe(lineas, args[1]).ToList();
var conteo = ImportadorWhatsApp.ContarPalabras(mensajesPropios);
var vocabulario = ImportadorWhatsApp.Vocabulario(conteo, diccionario, MinimoPalabrasNuevas);
var nuevas = vocabulario.Where(kv => diccionario.Buscar(kv.Key) is null).ToList();

var destino = args.Length >= 3 ? args[2] : Rutas.VocabularioPersonal;
Diccionario.GuardarVocabulario(destino, vocabulario);

Console.WriteLine($"Mensajes de {args[1]}: {mensajesPropios.Count}");
Console.WriteLine($"Palabras escritas: {conteo.Values.Sum()} ({conteo.Count} distintas)");
Console.WriteLine($"Vocabulario guardado: {vocabulario.Count} palabras ({nuevas.Count} que no estaban en el diccionario)");
Console.WriteLine($"Descartadas por aparecer menos de {MinimoPalabrasNuevas} veces: {conteo.Count - vocabulario.Count}");
Console.WriteLine("Tus palabras más usadas: " + string.Join(", ", vocabulario.Take(20).Select(kv => kv.Key)));
Console.WriteLine("Palabras nuevas más usadas: " + string.Join(", ", nuevas.Take(20).Select(kv => kv.Key)));
Console.WriteLine($"Archivo: {destino}");
return 0;
