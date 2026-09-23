using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using TecladoConMouse.Nucleo;
using Forms = System.Windows.Forms;

namespace TecladoConMouse;

public partial class App : System.Windows.Application
{
    const string NombreMutexInstancia = "TecladoConMouse.InstanciaUnica";
    const string NombreEventoAlternar = "TecladoConMouse.Alternar";
    const string ClaveInicioWindows = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string NombreValorInicio = "TecladoConMouse";

    Mutex? mutexInstancia;
    EventWaitHandle? eventoAlternar;
    Forms.NotifyIcon? iconoBandeja;
    VentanaTeclado? teclado;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        mutexInstancia = new Mutex(true, NombreMutexInstancia, out bool esPrimeraInstancia);
        if (!esPrimeraInstancia)
        {
            if (EventWaitHandle.TryOpenExisting(NombreEventoAlternar, out var enEjecucion)) enEjecucion.Set();
            mutexInstancia = null;
            Shutdown();
            return;
        }

        var configuracion = Configuracion.Cargar();
        var diccionario = new Diccionario();
        diccionario.CargarBase(Path.Combine(AppContext.BaseDirectory, "Datos", "es_50k.txt"));
        diccionario.CargarVocabulario(Rutas.VocabularioPersonal);
        diccionario.CargarUsuario(Rutas.PalabrasUsuario);

        teclado = new VentanaTeclado(configuracion, diccionario);
        teclado.Inicializar();

        eventoAlternar = new EventWaitHandle(false, EventResetMode.AutoReset, NombreEventoAlternar);
        ThreadPool.RegisterWaitForSingleObject(eventoAlternar, (_, _) => Dispatcher.BeginInvoke(teclado.Alternar), null, Timeout.Infinite, false);

        CrearIconoBandeja(configuracion);
        if (Array.IndexOf(e.Args, "--mostrar") >= 0) teclado.Alternar();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        teclado?.Apagar();
        if (iconoBandeja is not null)
        {
            iconoBandeja.Visible = false;
            iconoBandeja.Dispose();
        }
        mutexInstancia?.ReleaseMutex();
        base.OnExit(e);
    }

    void CrearIconoBandeja(Configuracion configuracion)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Mostrar / ocultar", null, (_, _) => teclado!.Alternar());
        var inicioAutomatico = new Forms.ToolStripMenuItem("Iniciar con Windows") { Checked = InicioAutomaticoActivado(), CheckOnClick = true };
        inicioAutomatico.CheckedChanged += (_, _) => ConfigurarInicioAutomatico(inicioAutomatico.Checked);
        menu.Items.Add(inicioAutomatico);
        menu.Items.Add("Abrir carpeta de configuración", null, (_, _) => Process.Start("explorer.exe", Rutas.Carpeta));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Shutdown());

        iconoBandeja = new Forms.NotifyIcon { Icon = CrearIcono(), Text = "Teclado con mouse", ContextMenuStrip = menu, Visible = true };
        iconoBandeja.MouseClick += (_, a) =>
        {
            if (a.Button == Forms.MouseButtons.Left) teclado!.Alternar();
        };

        var mensaje = teclado!.AtajosFallidos.Count == 0
            ? $"Listo. Abrilo con {string.Join(" o ", configuracion.AtajosAbrir)}."
            : $"No se pudo registrar {string.Join(", ", teclado.AtajosFallidos)} (¿lo usa otra app?). Cambialo en configuracion.json.";
        iconoBandeja.ShowBalloonTip(3000, "Teclado con mouse", mensaje, Forms.ToolTipIcon.Info);
    }

    static Icon CrearIcono()
    {
        using var imagen = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(imagen))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var fondo = new SolidBrush(Color.FromArgb(79, 195, 247));
            g.FillEllipse(fondo, 1, 1, 30, 30);
            using var lapiz = new Pen(Color.FromArgb(28, 29, 33), 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(lapiz, new PointF[] { new(8, 20), new(13, 11), new(18, 19), new(24, 10) });
        }
        return Icon.FromHandle(imagen.GetHicon());
    }

    static bool InicioAutomaticoActivado()
    {
        using var clave = Registry.CurrentUser.OpenSubKey(ClaveInicioWindows);
        return clave?.GetValue(NombreValorInicio) is string;
    }

    static void ConfigurarInicioAutomatico(bool activado)
    {
        using var clave = Registry.CurrentUser.CreateSubKey(ClaveInicioWindows);
        if (activado) clave.SetValue(NombreValorInicio, $"\"{Environment.ProcessPath}\"");
        else clave.DeleteValue(NombreValorInicio, throwOnMissingValue: false);
    }
}
