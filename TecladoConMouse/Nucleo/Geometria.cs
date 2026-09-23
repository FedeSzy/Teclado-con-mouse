using System;
using System.Collections.Generic;

namespace TecladoConMouse.Nucleo;

public readonly record struct Punto(double X, double Y)
{
    public static Punto operator +(Punto a, Punto b) => new(a.X + b.X, a.Y + b.Y);
    public static Punto operator -(Punto a, Punto b) => new(a.X - b.X, a.Y - b.Y);
    public static Punto operator *(Punto a, double k) => new(a.X * k, a.Y * k);

    public static double Distancia(Punto a, Punto b) => Math.Sqrt(DistanciaAlCuadrado(a, b));

    public static double DistanciaAlCuadrado(Punto a, Punto b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}

public static class CalculoTrazos
{
    public static double Longitud(IReadOnlyList<Punto> puntos)
    {
        double total = 0;
        for (int i = 1; i < puntos.Count; i++) total += Punto.Distancia(puntos[i - 1], puntos[i]);
        return total;
    }

    public static Punto[] Remuestrear(IReadOnlyList<Punto> puntos, int cantidad)
    {
        var resultado = new Punto[cantidad];
        double total = Longitud(puntos);
        if (puntos.Count == 1 || total < 1e-9)
        {
            Array.Fill(resultado, puntos[0]);
            return resultado;
        }

        double paso = total / (cantidad - 1);
        double acumulado = 0;
        Punto anterior = puntos[0];
        resultado[0] = anterior;
        int k = 1, i = 1;
        while (i < puntos.Count && k < cantidad)
        {
            Punto actual = puntos[i];
            double d = Punto.Distancia(anterior, actual);
            if (d > 0 && acumulado + d >= paso)
            {
                anterior += (actual - anterior) * ((paso - acumulado) / d);
                resultado[k++] = anterior;
                acumulado = 0;
            }
            else
            {
                acumulado += d;
                anterior = actual;
                i++;
            }
        }
        while (k < cantidad) resultado[k++] = puntos[^1];
        return resultado;
    }

    public static Punto[] NormalizarForma(Punto[] puntos, double extensionMinima = 1.0)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        double sumaX = 0, sumaY = 0;
        foreach (var p in puntos)
        {
            minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
            sumaX += p.X; sumaY += p.Y;
        }
        var centroide = new Punto(sumaX / puntos.Length, sumaY / puntos.Length);
        double escala = 1.0 / Math.Max(Math.Max(maxX - minX, maxY - minY), extensionMinima);
        var resultado = new Punto[puntos.Length];
        for (int i = 0; i < puntos.Length; i++) resultado[i] = (puntos[i] - centroide) * escala;
        return resultado;
    }

    public static double DistanciaMedia(Punto[] a, Punto[] b)
    {
        double suma = 0;
        for (int i = 0; i < a.Length; i++) suma += Punto.Distancia(a[i], b[i]);
        return suma / a.Length;
    }
}
