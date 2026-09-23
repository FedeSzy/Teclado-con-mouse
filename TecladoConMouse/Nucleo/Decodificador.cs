using System;
using System.Collections.Generic;
using System.Linq;

namespace TecladoConMouse.Nucleo;

public sealed record Candidata(EntradaPalabra Entrada, double Puntaje);

public sealed class Decodificador
{
    const int PuntosMuestra = 48;
    const int PuntosMuestraDensa = 96;

    readonly Diccionario diccionario;

    public Decodificador(Diccionario diccionario) => this.diccionario = diccionario;

    public double SigmaUbicacion { get; set; } = 0.27;
    public double SigmaForma { get; set; } = 0.12;
    public double SigmaCobertura { get; set; } = 0.35;
    public double MargenCobertura { get; set; } = 0.45;
    public double PesoFrecuencia { get; set; } = 0.35;
    public double RadioExtremos { get; set; } = 1.3;

    public double SigmaInicio { get; set; } = 0.3;
    public double SigmaFin { get; set; } = 0.5;

    public List<Candidata> Decodificar(IReadOnlyList<Punto> gesto, int maximo)
    {
        var resultado = new List<Candidata>(maximo + 1);
        if (gesto.Count < 2) return resultado;

        var muestra = CalculoTrazos.Remuestrear(gesto, PuntosMuestra);
        var forma = CalculoTrazos.NormalizarForma(muestra);
        var muestraDensa = CalculoTrazos.Remuestrear(gesto, PuntosMuestraDensa);
        double longitud = CalculoTrazos.Longitud(gesto);

        double factorUbicacion = 1 / (2 * SigmaUbicacion * SigmaUbicacion);
        double factorForma = 1 / (2 * SigmaForma * SigmaForma);
        double factorCobertura = 1 / (2 * SigmaCobertura * SigmaCobertura);
        double factorInicio = 1 / (2 * SigmaInicio * SigmaInicio);
        double factorFin = 1 / (2 * SigmaFin * SigmaFin);

        foreach (var entrada in diccionario.Candidatas(LetrasCerca(gesto[0]), LetrasCerca(gesto[^1])))
        {
            if (entrada.Recorrido.Length < 2) continue;
            PrepararPlantilla(entrada);

            if (entrada.LongitudPlantilla > longitud * 1.7 + 1.5 || longitud > entrada.LongitudPlantilla * 1.7 + 2.5) continue;

            double ubicacion = CalculoTrazos.DistanciaMedia(muestra, entrada.Plantilla!);
            if (ubicacion > 1.6) continue;
            double distanciaForma = CalculoTrazos.DistanciaMedia(forma, entrada.PlantillaForma!);
            double cobertura = Cobertura(entrada.Recorrido, muestraDensa);
            double inicio = Punto.DistanciaAlCuadrado(gesto[0], DisposicionTeclado.CentroDe(entrada.Recorrido[0]));
            double fin = Punto.DistanciaAlCuadrado(gesto[^1], DisposicionTeclado.CentroDe(entrada.Recorrido[^1]));

            double puntaje = -ubicacion * ubicacion * factorUbicacion
                             - distanciaForma * distanciaForma * factorForma
                             - cobertura * factorCobertura
                             - inicio * factorInicio
                             - fin * factorFin
                             + PesoFrecuencia * Math.Log(entrada.Frecuencia + 1);
            Insertar(resultado, new Candidata(entrada, puntaje), maximo);
        }
        return resultado;
    }

    List<char> LetrasCerca(Punto p)
    {
        var cerca = DisposicionTeclado.CentrosDeLetras
            .Where(kv => Punto.Distancia(kv.Value, p) <= RadioExtremos)
            .Select(kv => kv.Key)
            .ToList();
        if (cerca.Count > 0) return cerca;

        return DisposicionTeclado.CentrosDeLetras.OrderBy(kv => Punto.Distancia(kv.Value, p)).Take(3).Select(kv => kv.Key).ToList();
    }

    double Cobertura(string recorrido, Punto[] muestraDensa)
    {
        double suma = 0;
        for (int k = 1; k < recorrido.Length - 1; k++)
        {
            var centro = DisposicionTeclado.CentroDe(recorrido[k]);
            double mejor = double.MaxValue;
            foreach (var p in muestraDensa) mejor = Math.Min(mejor, Punto.DistanciaAlCuadrado(p, centro));
            double exceso = Math.Sqrt(mejor) - MargenCobertura;
            if (exceso > 0) suma += exceso * exceso;
        }
        return suma;
    }

    static void PrepararPlantilla(EntradaPalabra entrada)
    {
        if (entrada.Plantilla is not null) return;
        var centros = entrada.Recorrido.Select(DisposicionTeclado.CentroDe).ToArray();
        entrada.Plantilla = CalculoTrazos.Remuestrear(centros, PuntosMuestra);
        entrada.PlantillaForma = CalculoTrazos.NormalizarForma(entrada.Plantilla);
        entrada.LongitudPlantilla = CalculoTrazos.Longitud(centros);
    }

    static void Insertar(List<Candidata> lista, Candidata candidata, int maximo)
    {
        if (lista.Count == maximo && candidata.Puntaje <= lista[^1].Puntaje) return;
        int i = lista.FindIndex(c => c.Puntaje < candidata.Puntaje);
        lista.Insert(i < 0 ? lista.Count : i, candidata);
        if (lista.Count > maximo) lista.RemoveAt(maximo);
    }
}
