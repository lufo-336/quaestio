using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Parser = Wsa.Quiz.Core.Services.TestoConCodice;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.Controls;

/// <summary>
/// Mostra un testo di domanda/risposta/spiegazione riconoscendo il codice:
/// blocchi ```lang ... ``` (riquadro con numeri di riga) e codice inline tra
/// backtick (chip monospazio). Il parsing e' in <c>Wsa.Quiz.Core.Services</c>.
/// FontSize, Foreground, FontWeight e FontStyle del controllo si propagano al
/// testo normale per ereditarieta'. Niente XAML: tutto in code-behind.
/// </summary>
public class TestoConCodice : ContentControl
{
    public static readonly StyledProperty<string?> TestoProperty =
        AvaloniaProperty.Register<TestoConCodice, string?>(nameof(Testo));

    public static readonly StyledProperty<double> InterlineaProperty =
        AvaloniaProperty.Register<TestoConCodice, double>(nameof(Interlinea), double.NaN);

    public string? Testo
    {
        get => GetValue(TestoProperty);
        set => SetValue(TestoProperty, value);
    }

    public double Interlinea
    {
        get => GetValue(InterlineaProperty);
        set => SetValue(InterlineaProperty, value);
    }

    static TestoConCodice()
    {
        // Il contenuto dipende da questi valori: se cambiano, si ricostruisce.
        TestoProperty.Changed.AddClassHandler<TestoConCodice>((c, _) => c.Ricostruisci());
        InterlineaProperty.Changed.AddClassHandler<TestoConCodice>((c, _) => c.Ricostruisci());
        FontSizeProperty.Changed.AddClassHandler<TestoConCodice>((c, _) => c.Ricostruisci());
    }

    /// <summary>
    /// Lega una proprieta' a una risorsa (brush) in modo dinamico, cosi' il
    /// cambio tema aggiorna i colori (trappola 7: niente lettura una tantum).
    /// </summary>
    private void LegaRisorsa(AvaloniaObject destinazione, AvaloniaProperty proprieta, string chiave)
        => _legami.Add(destinazione.Bind(proprieta, this.GetResourceObservable(chiave)));

    // Legami alle risorse del contenuto attuale: rilasciati a ogni ricostruzione,
    // altrimenti ogni domanda lascerebbe agganciati gli elementi delle precedenti.
    private readonly List<IDisposable> _legami = new();

    private void Ricostruisci()
    {
        foreach (var legame in _legami) legame.Dispose();
        _legami.Clear();

        var segmenti = Parser.Analizza(Testo);
        if (segmenti.Count == 0)
        {
            Content = null;
            return;
        }

        var pannello = new StackPanel { Spacing = 8 };
        TextBlock? corrente = null;

        foreach (var seg in segmenti)
        {
            if (seg.Tipo == TipoSegmento.Blocco)
            {
                corrente = null;
                pannello.Children.Add(CreaBlocco(seg.Testo));
                continue;
            }

            // Testo e Inline consecutivi finiscono nello stesso TextBlock.
            if (corrente is null)
            {
                corrente = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = Interlinea,
                    Inlines = new InlineCollection()
                };
                pannello.Children.Add(corrente);
            }

            var run = new Run { Text = seg.Testo };
            if (seg.Tipo == TipoSegmento.Inline)
            {
                run.FontSize = FontSize * 0.9;
                LegaRisorsa(run, Inline.BackgroundProperty, "ChipSfondo");
                LegaRisorsa(run, Run.FontFamilyProperty, "FontCodice");
            }
            corrente.Inlines!.Add(run);
        }

        Content = pannello;
    }

    private Control CreaBlocco(string codice)
    {
        string[] righe = codice.Split('\n');
        double dimensione = FontSize * 0.87;
        double altezzaRiga = FontSize * 1.45;

        var numeri = new TextBlock
        {
            Text = string.Join("\n", Enumerable.Range(1, righe.Length)),
            TextAlignment = TextAlignment.Right,
            FontSize = dimensione,
            LineHeight = altezzaRiga,
            Margin = new Thickness(0, 0, 10, 0)
        };
        LegaRisorsa(numeri, TextBlock.FontFamilyProperty, "FontCodice");
        LegaRisorsa(numeri, TextBlock.ForegroundProperty, "CodiceNumeri");

        var testo = new TextBlock
        {
            Text = codice,
            TextWrapping = TextWrapping.NoWrap,
            FontSize = dimensione,
            LineHeight = altezzaRiga
        };
        LegaRisorsa(testo, TextBlock.FontFamilyProperty, "FontCodice");
        LegaRisorsa(testo, TextBlock.ForegroundProperty, "CodiceTesto");
        Grid.SetColumn(testo, 1);

        var griglia = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        griglia.Children.Add(numeri);
        griglia.Children.Add(testo);

        // Scroll solo orizzontale: la pagina resta l'unico scroll verticale (trappola 11).
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = griglia
        };

        var bordo = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(10, 8),
            Child = scroll
        };
        LegaRisorsa(bordo, Border.BackgroundProperty, "CodiceSfondo");
        LegaRisorsa(bordo, Border.BorderBrushProperty, "CodiceBordo");
        return bordo;
    }
}
