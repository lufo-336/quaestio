using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Wsa.Quiz.App.Views;

public enum PausaDialogResult
{
    Annulla,
    Abbandona,
    Salva
}

/// <summary>
/// Modale pausa unificata (step 6), resa come overlay in-page (M3). Tre uscite:
/// Annulla (default + ESC), Abbandona, Salva e esci. Frecce sx/dx muovono il
/// focus ciclicamente fra i 3 bottoni; Ctrl+Tab è bloccato per non cambiare tab
/// con la modale aperta. Si legge <see cref="Risultato"/> dopo
/// <c>await ShowOverlayAsync(...)</c>.
/// </summary>
public partial class PausaDialog : OverlayDialogBase
{
    public PausaDialogResult Risultato { get; private set; } = PausaDialogResult.Annulla;

    public PausaDialog()
    {
        InitializeComponent();

        if (AppEnv.TouchMode)
            AggiornaLayoutPulsantiera(suDueRighe: true);
        CardBorder.SizeChanged += (_, se) =>
        {
            if (!AppEnv.TouchMode && se.NewSize.Width > 0)
                AggiornaLayoutPulsantiera(suDueRighe: se.NewSize.Width < 380);
        };

        var bottoniInOrdine = new Button[] { AbbandonaBtn, AnnullaBtn, SalvaBtn };
        KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Escape)
            {
                ke.Handled = true;
                Risultato = PausaDialogResult.Annulla;
                Chiudi();
                return;
            }
            if (ke.Key == Key.Tab && ke.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                ke.Handled = true;
                return;
            }
            if (ke.Key == Key.Left || ke.Key == Key.Right)
            {
                var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Button;
                int idx = Array.IndexOf(bottoniInOrdine, focused);
                if (idx < 0) idx = 1; // default su Annulla
                int delta = ke.Key == Key.Right ? +1 : -1;
                int nuovo = (idx + delta + bottoniInOrdine.Length) % bottoniInOrdine.Length;
                // Con il metodo direzionale il bordo giallo segue le frecce (Trappola 25).
                bottoniInOrdine[nuovo].Focus(NavigationMethod.Directional);
                ke.Handled = true;
            }
        };
    }

    private void AggiornaLayoutPulsantiera(bool suDueRighe)
    {
        if (suDueRighe)
        {
            Pulsantiera.ColumnDefinitions = new ColumnDefinitions("*");
            Grid.SetColumn(AzioniDestra, 0);
            Grid.SetRow(AzioniDestra, 1);
            AzioniDestra.HorizontalAlignment = HorizontalAlignment.Right;
            AzioniDestra.Margin = new Thickness(0, 8, 0, 0);
        }
        else
        {
            Pulsantiera.ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
            Grid.SetColumn(AzioniDestra, 2);
            Grid.SetRow(AzioniDestra, 0);
            AzioniDestra.HorizontalAlignment = HorizontalAlignment.Left;
            AzioniDestra.Margin = new Thickness(0);
        }
    }

    protected override void OnShown() => AnnullaBtn.Focus();

    private void OnAbbandonaClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Risultato = PausaDialogResult.Abbandona;
        Chiudi();
    }

    private void OnAnnullaClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Risultato = PausaDialogResult.Annulla;
        Chiudi();
    }

    private void OnSalvaClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Risultato = PausaDialogResult.Salva;
        Chiudi();
    }
}
