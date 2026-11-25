using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App;

/// <summary>
/// Colori condivisi dell'applicazione. I colori degli esiti e di pericolo derivano
/// dai tre inchiostri riservati di <see cref="Inchiostri"/> (<c>verdeacqua</c>,
/// <c>ocra</c>, <c>rosso</c>), con varianti dedicate per i testi su sfondo chiaro.
/// </summary>
public static class QuizColors
{
    private static readonly Inchiostro _verde = Inchiostri.Trova("verdeacqua");
    private static readonly Inchiostro _ambra = Inchiostri.Trova("ocra");
    private static readonly Inchiostro _rosso = Inchiostri.Trova("rosso");

    public static readonly string Verde = _verde.Hex;
    public static readonly string Ambra = _ambra.Hex;
    public static readonly string Rosso = _rosso.Hex;

    public const string Focus = "#FFD500";

    public static readonly string Danger        = _rosso.Hex;
    public static readonly string DangerHover   = FormatHex(Mescola(Color.Parse(_rosso.Hex), Colors.White, 0.10));
    public static readonly string DangerPressed = FormatHex(Mescola(Color.Parse(_rosso.Hex), Colors.Black, 0.20));

    public const string DisabilitatoSfondo = "#D6D6D6";
    public const string DisabilitatoTesto  = "#7A7A7A";

    private static readonly IBrush _verdeBrush = new SolidColorBrush(Color.Parse(Verde));
    private static readonly IBrush _ambraBrush = new SolidColorBrush(Color.Parse(Ambra));
    private static readonly IBrush _rossoBrush = new SolidColorBrush(Color.Parse(Rosso));

    private static readonly IBrush _verdeTestoChiaroBrush = new SolidColorBrush(Color.Parse(_verde.HexTestoSuChiaro!));
    private static readonly IBrush _ambraTestoChiaroBrush = new SolidColorBrush(Color.Parse(_ambra.HexTestoSuChiaro!));
    private static readonly IBrush _rossoTestoChiaroBrush = new SolidColorBrush(Color.Parse(_rosso.HexTestoSuChiaro!));

    /// <summary>Brush pieno dell'esito positivo (per riempimenti, barre e bordi; testo sopra nero).</summary>
    public static IBrush VerdeBrush => _verdeBrush;
    /// <summary>Brush pieno dell'esito intermedio (per riempimenti, barre e bordi; testo sopra nero).</summary>
    public static IBrush AmbraBrush => _ambraBrush;
    /// <summary>Brush pieno dell'esito negativo (per riempimenti, barre e bordi; testo sopra nero).</summary>
    public static IBrush RossoBrush => _rossoBrush;

    private static bool IsTemaScuro =>
        Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    /// <summary>Colore per il testo dell'esito positivo: scuro sul tema chiaro, inchiostro pieno sul tema scuro.</summary>
    public static IBrush VerdeTestoBrush => IsTemaScuro ? _verdeBrush : _verdeTestoChiaroBrush;
    /// <summary>Colore per il testo dell'esito intermedio: scuro sul tema chiaro, inchiostro pieno sul tema scuro.</summary>
    public static IBrush AmbraTestoBrush => IsTemaScuro ? _ambraBrush : _ambraTestoChiaroBrush;
    /// <summary>Colore per il testo dell'esito negativo: scuro sul tema chiaro, inchiostro pieno sul tema scuro.</summary>
    public static IBrush RossoTestoBrush => IsTemaScuro ? _rossoBrush : _rossoTestoChiaroBrush;

    // Neutro per riempimenti (es. pallino Nuove, fallback barre) e testo attenuato del cronometro.
    public const string TestoNeutro = "#767676";
    private static readonly IBrush _neutroBrush = new SolidColorBrush(Color.Parse(TestoNeutro));
    private static readonly IBrush _tempoNeutroChiaroBrush = new SolidColorBrush(Color.Parse("#57564f"));
    private static readonly IBrush _tempoNeutroScuroBrush  = new SolidColorBrush(Color.Parse("#a6a6a6"));
    public static IBrush NeutroBrush => _neutroBrush;

    // Sfondi tenui per le celle della heatmap/riquadri, derivati dagli inchiostri.
    private static readonly IBrush _verdeBrushSfondo = new SolidColorBrush(Mescola(Color.Parse(Verde), Colors.White, 0.86));
    private static readonly IBrush _ambraBrushSfondo = new SolidColorBrush(Mescola(Color.Parse(Ambra), Colors.White, 0.86));
    private static readonly IBrush _rossoBrushSfondo = new SolidColorBrush(Mescola(Color.Parse(Rosso), Colors.White, 0.86));

    /// <summary>
    /// Bordo/riempimento barra in base alla percentuale di errore (0..1):
    /// verde &lt; 20%, ambra 20-59%, rosso &gt;= 60%.
    /// </summary>
    public static IBrush HeatmapBordo(double pctErrore)
    {
        if (pctErrore < 0.20) return _verdeBrush;
        if (pctErrore < 0.60) return _ambraBrush;
        return _rossoBrush;
    }

    /// <summary>
    /// Variante testo in base alla percentuale di errore (0..1), reattiva al tema.
    /// </summary>
    public static IBrush HeatmapTesto(double pctErrore)
    {
        if (pctErrore < 0.20) return VerdeTestoBrush;
        if (pctErrore < 0.60) return AmbraTestoBrush;
        return RossoTestoBrush;
    }

    /// <summary>
    /// Sfondo tenue cella heatmap, abbinato a <see cref="HeatmapBordo"/>.
    /// </summary>
    public static IBrush HeatmapSfondo(double pctErrore)
    {
        if (pctErrore < 0.20) return _verdeBrushSfondo;
        if (pctErrore < 0.60) return _ambraBrushSfondo;
        return _rossoBrushSfondo;
    }

    /// <summary>
    /// Brush di riempimento associato a una percentuale:
    /// verde &gt;= 80%, ambra 50-79%, rosso &lt; 50%.
    /// </summary>
    public static IBrush Percentuale(double pct)
    {
        if (pct >= 80) return _verdeBrush;
        if (pct >= 50) return _ambraBrush;
        return _rossoBrush;
    }

    /// <summary>
    /// Brush per il testo di una percentuale (contrasto >= 4,5:1 in tema chiaro e scuro).
    /// </summary>
    public static IBrush PercentualeTesto(double pct)
    {
        if (pct >= 80) return VerdeTestoBrush;
        if (pct >= 50) return AmbraTestoBrush;
        return RossoTestoBrush;
    }

    /// <summary>
    /// Brush pieno di esito (verde per corretto, rosso per sbagliato), per bordi o riempimenti.
    /// </summary>
    public static IBrush Esito(bool corretto)
    {
        return corretto ? _verdeBrush : _rossoBrush;
    }

    /// <summary>
    /// Brush per il testo di esito (verde/rosso adattato al tema).
    /// </summary>
    public static IBrush EsitoTesto(bool corretto)
    {
        return corretto ? VerdeTestoBrush : RossoTestoBrush;
    }

    /// <summary>
    /// Colore del testo countdown in modalità a tempo in base al tempo rimanente:
    /// rosso &lt;= 10s, ambra &lt;= 60s, neutro oltre.
    /// </summary>
    public static IBrush TempoRimanente(TimeSpan rimanente)
    {
        if (rimanente.TotalSeconds <= 10) return RossoTestoBrush;
        if (rimanente.TotalSeconds <= 60) return AmbraTestoBrush;
        return TempoNeutro;
    }

    /// <summary>Colore neutro del testo del cronometro (modalità non a tempo / countdown sopra soglia).</summary>
    public static IBrush TempoNeutro => IsTemaScuro ? _tempoNeutroScuroBrush : _tempoNeutroChiaroBrush;

    /// <summary>
    /// Aggancia il ricalcolo dei brush dipendenti dal tema solo finché <paramref name="vista"/>
    /// è nel visual tree (evita leak di sottoscrizioni su <see cref="Application.Current"/>)
    /// e riallinea subito a ogni attach nel caso il tema sia cambiato mentre la tab era nascosta.
    /// </summary>
    public static void CollegaCambioTema(Control vista, Action aggiorna)
    {
        EventHandler handler = (_, _) => aggiorna();
        vista.AttachedToVisualTree += (_, _) =>
        {
            if (Application.Current is { } app)
            {
                app.ActualThemeVariantChanged -= handler;
                app.ActualThemeVariantChanged += handler;
            }
            aggiorna();
        };
        vista.DetachedFromVisualTree += (_, _) =>
        {
            if (Application.Current is { } app)
                app.ActualThemeVariantChanged -= handler;
        };
    }

    private static Color Mescola(Color a, Color b, double t)
    {
        byte r = (byte)Math.Clamp(Math.Round(a.R + (b.R - a.R) * t), 0, 255);
        byte g = (byte)Math.Clamp(Math.Round(a.G + (b.G - a.G) * t), 0, 255);
        byte bVal = (byte)Math.Clamp(Math.Round(a.B + (b.B - a.B) * t), 0, 255);
        byte alpha = (byte)Math.Clamp(Math.Round(a.A + (b.A - a.A) * t), 0, 255);
        return Color.FromArgb(alpha, r, g, bVal);
    }

    private static string FormatHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
