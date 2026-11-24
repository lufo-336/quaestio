using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Wsa.Quiz.App.Controls;

/// <summary>
/// Barra proporzionale a 3 segmenti (Rosse / Ambra / Verdi) usata nelle card materia
/// delle Statistiche. Puramente Avalonia, nessuna libreria di grafici.
/// </summary>
public class BarraLivelli : Control
{
    public static readonly StyledProperty<int> RosseProperty =
        AvaloniaProperty.Register<BarraLivelli, int>(nameof(Rosse));

    public static readonly StyledProperty<int> AmbraProperty =
        AvaloniaProperty.Register<BarraLivelli, int>(nameof(Ambra));

    public static readonly StyledProperty<int> VerdiProperty =
        AvaloniaProperty.Register<BarraLivelli, int>(nameof(Verdi));

    private static readonly IBrush _trackBrush = new SolidColorBrush(Color.Parse("#33888888"));

    static BarraLivelli()
    {
        AffectsRender<BarraLivelli>(RosseProperty, AmbraProperty, VerdiProperty);
    }

    public int Rosse { get => GetValue(RosseProperty); set => SetValue(RosseProperty, value); }
    public int Ambra { get => GetValue(AmbraProperty); set => SetValue(AmbraProperty, value); }
    public int Verdi { get => GetValue(VerdiProperty); set => SetValue(VerdiProperty, value); }

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        double r = h / 2.0;
        var full = new Rect(0, 0, w, h);
        context.DrawRectangle(_trackBrush, null, full, r, r);

        int tot = Math.Max(0, Rosse) + Math.Max(0, Ambra) + Math.Max(0, Verdi);
        if (tot == 0) return;

        using (context.PushClip(new RoundedRect(full, r)))
        {
            double x = 0;
            if (Rosse > 0)
            {
                double sw = w * Rosse / tot;
                context.FillRectangle(QuizColors.RossoBrush, new Rect(x, 0, sw, h));
                x += sw;
            }
            if (Ambra > 0)
            {
                double sw = w * Ambra / tot;
                context.FillRectangle(QuizColors.AmbraBrush, new Rect(x, 0, sw, h));
                x += sw;
            }
            if (Verdi > 0)
            {
                double sw = w - x;
                context.FillRectangle(QuizColors.VerdeBrush, new Rect(x, 0, sw, h));
            }
        }
    }
}

/// <summary>
/// Barra orizzontale singola (0..1) usata nelle righe categoria delle Statistiche.
/// </summary>
public class BarraErrore : Control
{
    public static readonly StyledProperty<double> ValoreProperty =
        AvaloniaProperty.Register<BarraErrore, double>(nameof(Valore));

    public static readonly StyledProperty<IBrush?> ColoreProperty =
        AvaloniaProperty.Register<BarraErrore, IBrush?>(nameof(Colore));

    private static readonly IBrush _trackBrush = new SolidColorBrush(Color.Parse("#33888888"));

    static BarraErrore()
    {
        AffectsRender<BarraErrore>(ValoreProperty, ColoreProperty);
    }

    public double Valore { get => GetValue(ValoreProperty); set => SetValue(ValoreProperty, value); }
    public IBrush? Colore { get => GetValue(ColoreProperty); set => SetValue(ColoreProperty, value); }

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        double r = h / 2.0;
        context.DrawRectangle(_trackBrush, null, new Rect(0, 0, w, h), r, r);

        double pct = Math.Clamp(Valore, 0.0, 1.0);
        if (pct <= 0) return;

        double fillW = Math.Max(h, w * pct);
        context.DrawRectangle(Colore ?? QuizColors.NeutroBrush, null, new Rect(0, 0, fillW, h), r, r);
    }
}
