using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TecladoConMouse.Nucleo;
using TecladoConMouse.Win32;

namespace TecladoConMouse;

public partial class VentanaTeclado : Window
{
    const double Separacion = 4;
    const double UmbralDeslizamiento = 0.5;
    const int DistanciaAlCursor = 28;

    static readonly Brush FondoTecla = Congelado("#2E3036");
    static readonly Brush FondoTeclaEspecial = Congelado("#3B3E46");
    static readonly Brush FondoPresionada = Congelado("#4FC3F7");
    static readonly Brush FondoMayusculaUnaVez = Congelado("#2F6F8F");
    static readonly Brush ColorTexto = Congelado("#F1F3F4");
    static readonly Brush ColorAcento = Congelado("#4FC3F7");

    readonly Configuracion configuracion;
    readonly ControladorEscritura controlador;
    readonly GanchoMouse ganchoMouse = new();
    readonly DispatcherTimer temporizadorRepeticion = new();
    readonly Dictionary<Tecla, Border> cajasTeclas = new();
    readonly List<Punto> gesto = new();
    readonly List<int> atajosDeAccion = new();

    AtajosGlobales? atajos;
    IntPtr hwnd;
    IReadOnlyList<Tecla> capa = DisposicionTeclado.Letras;
    Tecla? teclaPresionada;
    bool deslizando;
    bool palabraBorradaAlPrimerClic;
    Action? accionRepetida;
    Nativo.POINT cursorInicioArrastre;
    Nativo.RECT ventanaInicioArrastre;

    public VentanaTeclado(Configuracion configuracion, Diccionario diccionario)
    {
        InitializeComponent();
        this.configuracion = configuracion;
        controlador = new ControladorEscritura(diccionario, new Decodificador(diccionario), new SalidaTextoWin32());
        controlador.Cambio += Actualizar;

        AreaTeclas.Width = DisposicionTeclado.Columnas * configuracion.AnchoTecla;
        AreaTeclas.Height = DisposicionTeclado.Filas * configuracion.AltoTecla;
        AreaTeclas.MouseLeftButtonDown += AlPresionarTecla;
        AreaTeclas.MouseMove += AlMoverSobreTeclas;
        AreaTeclas.MouseLeftButtonUp += AlSoltarTecla;
        AreaTeclas.LostMouseCapture += (_, _) => CancelarPresion();

        MouseRightButtonDown += AlPresionarClicDerecho;
        MouseRightButtonUp += (_, e) => { e.Handled = true; DetenerRepeticion(); };
        MouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            e.Handled = true;
            PresionarEnter();
        };

        Manija.MouseLeftButtonDown += AlPresionarManija;
        Manija.MouseMove += AlMoverManija;
        Manija.MouseLeftButtonUp += (_, _) => Manija.ReleaseMouseCapture();
        BotonCerrar.MouseLeftButtonDown += (_, e) => e.Handled = true;
        BotonCerrar.MouseLeftButtonUp += (_, e) => { e.Handled = true; OcultarTeclado(); };

        temporizadorRepeticion.Tick += (_, _) =>
        {
            temporizadorRepeticion.Interval = TimeSpan.FromMilliseconds(60);
            accionRepetida?.Invoke();
        };

        ganchoMouse.CapturarBotonesLaterales = configuracion.BotonesLaterales;
        ganchoMouse.CapturarInclinacion = configuracion.InclinacionRueda;
        ganchoMouse.BotonLateral += AlBotonLateral;
        ganchoMouse.Inclinacion += controlador.Rotar;
        ganchoMouse.ClicAfuera += controlador.ReiniciarContexto;

        ArmarTeclas();
        Actualizar();
    }

    public List<string> AtajosFallidos { get; } = new();

    public void Inicializar()
    {
        hwnd = new WindowInteropHelper(this).EnsureHandle();
        long estiloExtendido = Nativo.GetWindowLongPtr(hwnd, Nativo.GWL_EXSTYLE).ToInt64();
        Nativo.SetWindowLongPtr(hwnd, Nativo.GWL_EXSTYLE, new IntPtr(estiloExtendido | Nativo.WS_EX_NOACTIVATE | Nativo.WS_EX_TOOLWINDOW));
        HwndSource.FromHwnd(hwnd).AddHook(ProcedimientoVentana);
        ganchoMouse.VentanaPropia = hwnd;

        atajos = new AtajosGlobales(hwnd);
        foreach (var atajo in configuracion.AtajosAbrir)
            if (atajos.Registrar(atajo, Alternar) is null) AtajosFallidos.Add(atajo);
    }

    public void Alternar()
    {
        if (IsVisible) OcultarTeclado();
        else MostrarTeclado();
    }

    public void Apagar()
    {
        DetenerRepeticion();
        ganchoMouse.Desinstalar();
    }

    void MostrarTeclado()
    {
        Opacity = 0;
        Show();
        UbicarJuntoAlCursor();
        Opacity = configuracion.Opacidad;
        ganchoMouse.Instalar();

        var acciones = new (string Atajo, Action Accion)[]
        {
            (configuracion.AtajoAceptar, controlador.Aceptar),
            (configuracion.AtajoBorrar, controlador.Borrar),
            (configuracion.AtajoEnter, PresionarEnter),
            (configuracion.AtajoSiguienteSugerencia, () => controlador.Rotar(1)),
        };
        foreach (var (atajo, accion) in acciones)
            if (!string.IsNullOrWhiteSpace(atajo) && atajos!.Registrar(atajo, accion) is int id) atajosDeAccion.Add(id);
    }

    void OcultarTeclado()
    {
        CancelarPresion();
        DetenerRepeticion();
        ganchoMouse.Desinstalar();
        foreach (var id in atajosDeAccion) atajos!.Quitar(id);
        atajosDeAccion.Clear();
        controlador.ReiniciarContexto();
        if (capa != DisposicionTeclado.Letras) CambiarCapa();
        Hide();
    }

    void UbicarJuntoAlCursor()
    {
        Nativo.GetCursorPos(out var cursor);
        Nativo.GetWindowRect(hwnd, out var rectangulo);
        int ancho = rectangulo.Right - rectangulo.Left, alto = rectangulo.Bottom - rectangulo.Top;
        var monitor = new Nativo.MONITORINFO { cbSize = Marshal.SizeOf<Nativo.MONITORINFO>() };
        Nativo.GetMonitorInfo(Nativo.MonitorFromPoint(cursor, Nativo.MONITOR_DEFAULTTONEAREST), ref monitor);
        var areaUtil = monitor.rcWork;

        int x = cursor.X - ancho / 2;
        int y = cursor.Y + DistanciaAlCursor;
        if (y + alto > areaUtil.Bottom) y = cursor.Y - DistanciaAlCursor - alto;
        x = Math.Clamp(x, areaUtil.Left, Math.Max(areaUtil.Left, areaUtil.Right - ancho));
        y = Math.Clamp(y, areaUtil.Top, Math.Max(areaUtil.Top, areaUtil.Bottom - alto));
        Nativo.SetWindowPos(hwnd, Nativo.HWND_TOPMOST, x, y, 0, 0, Nativo.SWP_NOSIZE | Nativo.SWP_NOACTIVATE);
    }

    IntPtr ProcedimientoVentana(IntPtr ventana, int mensaje, IntPtr wParam, IntPtr lParam, ref bool manejado)
    {
        if (mensaje == Nativo.WM_MOUSEACTIVATE)
        {
            manejado = true;
            return new IntPtr(Nativo.MA_NOACTIVATE);
        }
        if (mensaje == Nativo.WM_HOTKEY && atajos is not null) manejado = atajos.Procesar(wParam);
        return IntPtr.Zero;
    }

    void ArmarTeclas()
    {
        LienzoTeclas.Children.Clear();
        cajasTeclas.Clear();
        foreach (var tecla in capa)
        {
            var etiqueta = new TextBlock
            {
                Text = tecla.Etiqueta,
                Foreground = ColorTexto,
                FontSize = tecla.Tipo is TipoTecla.Letra or TipoTecla.Simbolo ? 20 : 15,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var caja = new Border
            {
                Width = tecla.Ancho * configuracion.AnchoTecla - Separacion,
                Height = tecla.Alto * configuracion.AltoTecla - Separacion,
                CornerRadius = new CornerRadius(7),
                Background = FondoBase(tecla),
                Child = etiqueta,
            };
            Canvas.SetLeft(caja, tecla.X * configuracion.AnchoTecla + Separacion / 2);
            Canvas.SetTop(caja, tecla.Y * configuracion.AltoTecla + Separacion / 2);
            LienzoTeclas.Children.Add(caja);
            cajasTeclas[tecla] = caja;
        }
    }

    Brush FondoBase(Tecla tecla) => tecla.Tipo switch
    {
        TipoTecla.Letra or TipoTecla.Simbolo or TipoTecla.Espacio => FondoTecla,
        TipoTecla.Mayuscula => controlador.Mayuscula switch
        {
            EstadoMayuscula.UnaVez => FondoMayusculaUnaVez,
            EstadoMayuscula.Fija => ColorAcento,
            _ => FondoTeclaEspecial,
        },
        _ => FondoTeclaEspecial,
    };

    void MarcarPresionada(Tecla tecla, bool presionada)
    {
        if (cajasTeclas.TryGetValue(tecla, out var caja)) caja.Background = presionada ? FondoPresionada : FondoBase(tecla);
    }

    void CambiarCapa()
    {
        capa = capa == DisposicionTeclado.Letras ? DisposicionTeclado.Simbolos : DisposicionTeclado.Letras;
        ArmarTeclas();
        Actualizar();
    }

    void Actualizar()
    {
        bool mayusculas = controlador.Mayuscula != EstadoMayuscula.Apagada;
        foreach (var (tecla, caja) in cajasTeclas)
        {
            if (tecla.Tipo == TipoTecla.Letra) ((TextBlock)caja.Child).Text = mayusculas ? tecla.Etiqueta.ToUpperInvariant() : tecla.Etiqueta;
            if (tecla != teclaPresionada) caja.Background = FondoBase(tecla);
        }

        var sugerencias = controlador.Sugerencias;
        TextoAyuda.Visibility = sugerencias.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BarraSugerencias.Children.Clear();
        BarraSugerencias.Columns = Math.Max(1, sugerencias.Count);
        foreach (var sugerencia in sugerencias)
        {
            var celda = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = sugerencia.Etiqueta,
                    FontSize = 16,
                    Foreground = sugerencia.Resaltada ? ColorAcento : ColorTexto,
                    FontWeight = sugerencia.Resaltada ? FontWeights.SemiBold : FontWeights.Normal,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            celda.MouseEnter += (_, _) => celda.Background = FondoTeclaEspecial;
            celda.MouseLeave += (_, _) => celda.Background = Brushes.Transparent;
            celda.MouseLeftButtonDown += (_, e) => e.Handled = true;
            celda.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                sugerencia.Ejecutar();
            };
            BarraSugerencias.Children.Add(celda);
        }
    }

    void AlPresionarTecla(object sender, MouseButtonEventArgs e)
    {
        var p = AUnidades(e.GetPosition(AreaTeclas));
        var tecla = DisposicionTeclado.TeclaEn(capa, p);
        if (tecla is null) return;
        e.Handled = true;

        teclaPresionada = tecla;
        deslizando = false;
        gesto.Clear();
        gesto.Add(p);
        AreaTeclas.CaptureMouse();
        MarcarPresionada(tecla, true);
        if (tecla.Tipo == TipoTecla.Borrar) IniciarRepeticion(controlador.Borrar);
    }

    void AlMoverSobreTeclas(object sender, MouseEventArgs e)
    {
        if (teclaPresionada is null) return;
        var p = AUnidades(e.GetPosition(AreaTeclas));
        gesto.Add(p);

        if (deslizando)
        {
            Estela.Points.Add(APixeles(p));
        }
        else if (teclaPresionada.Tipo == TipoTecla.Letra && Punto.Distancia(p, gesto[0]) > UmbralDeslizamiento)
        {
            deslizando = true;
            MarcarPresionada(teclaPresionada, false);
            Estela.BeginAnimation(OpacityProperty, null);
            Estela.Opacity = 0.85;
            Estela.Points = new PointCollection(gesto.Select(APixeles));
        }
    }

    void AlSoltarTecla(object sender, MouseButtonEventArgs e)
    {
        if (teclaPresionada is null) return;
        var tecla = teclaPresionada;
        teclaPresionada = null;
        AreaTeclas.ReleaseMouseCapture();
        DetenerRepeticion();
        MarcarPresionada(tecla, false);

        if (deslizando)
        {
            controlador.Deslizar(gesto.ToArray());
            DesvanecerEstela();
        }
        else Tocar(tecla);
    }

    void CancelarPresion()
    {
        if (teclaPresionada is null) return;
        MarcarPresionada(teclaPresionada, false);
        teclaPresionada = null;
        DetenerRepeticion();
        Estela.Points = new PointCollection();
    }

    void Tocar(Tecla tecla)
    {
        switch (tecla.Tipo)
        {
            case TipoTecla.Letra: controlador.TocarLetra(tecla.Texto); break;
            case TipoTecla.Simbolo: controlador.TocarSimbolo(tecla.Texto); break;
            case TipoTecla.Mayuscula: controlador.AlternarMayuscula(); break;
            case TipoTecla.Capa: CambiarCapa(); break;
            case TipoTecla.Enter: PresionarEnter(); break;
            case TipoTecla.Espacio:
                controlador.Espacio();
                if (capa != DisposicionTeclado.Letras) CambiarCapa();
                break;
        }
    }

    void DesvanecerEstela()
    {
        var desvanecer = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
        desvanecer.Completed += (_, _) =>
        {
            if (!deslizando || teclaPresionada is null) Estela.Points = new PointCollection();
        };
        Estela.BeginAnimation(OpacityProperty, desvanecer);
    }

    void PresionarEnter()
    {
        controlador.Enter();
        if (configuracion.OcultarDespuesDeEnter) OcultarTeclado();
    }

    void AlPresionarClicDerecho(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ClickCount == 1)
        {
            palabraBorradaAlPrimerClic = controlador.HayPalabraReciente;
            IniciarRepeticion(controlador.Borrar);
        }
        else if (e.ClickCount > 2 || !palabraBorradaAlPrimerClic)
        {
            controlador.BorrarPalabra();
        }
    }

    void AlBotonLateral(int boton, bool presionado)
    {
        if (boton == 1)
        {
            if (presionado) IniciarRepeticion(controlador.Borrar);
            else DetenerRepeticion();
        }
        else if (boton == 2 && presionado) controlador.Aceptar();
    }

    void IniciarRepeticion(Action accion)
    {
        accion();
        accionRepetida = accion;
        temporizadorRepeticion.Interval = TimeSpan.FromMilliseconds(420);
        temporizadorRepeticion.Start();
    }

    void DetenerRepeticion()
    {
        temporizadorRepeticion.Stop();
        accionRepetida = null;
    }

    void AlPresionarManija(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Nativo.GetCursorPos(out cursorInicioArrastre);
        Nativo.GetWindowRect(hwnd, out ventanaInicioArrastre);
        Manija.CaptureMouse();
    }

    void AlMoverManija(object sender, MouseEventArgs e)
    {
        if (!Manija.IsMouseCaptured) return;
        Nativo.GetCursorPos(out var cursor);
        Nativo.SetWindowPos(hwnd, IntPtr.Zero,
            ventanaInicioArrastre.Left + cursor.X - cursorInicioArrastre.X,
            ventanaInicioArrastre.Top + cursor.Y - cursorInicioArrastre.Y,
            0, 0, Nativo.SWP_NOSIZE | Nativo.SWP_NOZORDER | Nativo.SWP_NOACTIVATE);
    }

    Punto AUnidades(Point p) => new(p.X / configuracion.AnchoTecla, p.Y / configuracion.AltoTecla);

    Point APixeles(Punto p) => new(p.X * configuracion.AnchoTecla, p.Y * configuracion.AltoTecla);

    static Brush Congelado(string color)
    {
        var pincel = (Brush)new BrushConverter().ConvertFromString(color)!;
        pincel.Freeze();
        return pincel;
    }
}
