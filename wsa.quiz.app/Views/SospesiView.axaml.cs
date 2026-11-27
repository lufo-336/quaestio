using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Wsa.Quiz.App.State;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Models.Laboratorio;
using Wsa.Quiz.Core.Services;

using Wsa.Quiz.Core.Risorse;
namespace Wsa.Quiz.App.Views;

/// <summary>
/// Tab "Sospesi": elenco delle sessioni messe in pausa (Quiz o Laboratorio).
/// Lo storage dei quiz e' condiviso fra console e GUI; le pause del Laboratorio
/// vivono nel file dedicato <c>laboratorio_in_pausa.json</c>.
/// Riprendi solleva <see cref="RiprendiRichiesto"/> o <see cref="RiprendiLabRichiesto"/>
/// con la pausa originale; la <c>MainView</c> ricostruisce la sessione e apre la vista.
/// Elimina ha conferma inline.
/// </summary>
public partial class SospesiView : TabellaViewBase<SessioneSospesaItem>
{
    /// <summary>L'utente ha cliccato "Riprendi" su una pausa quiz: la <c>MainView</c>
    /// costruisce la sessione e cambia view.</summary>
    public event EventHandler<SessionePausa>? RiprendiRichiesto;

    /// <summary>L'utente ha cliccato "Riprendi" su una pausa del Laboratorio: la <c>MainView</c>
    /// ricostruisce la <see cref="SessioneLaboratorio"/> e cambia view.</summary>
    public event EventHandler<PausaLaboratorio>? RiprendiLabRichiesto;

    /// <summary>Sollevato quando l'utente commuta l'interruttore globale Quiz | Laboratorio.</summary>
    public event EventHandler<bool>? LaboratorioCambiato;

    private bool _laboratorio;
    /// <summary>True in modalita' Laboratorio, false in modalita' Quiz.</summary>
    public bool Laboratorio
    {
        get => _laboratorio;
        set
        {
            if (_laboratorio != value)
            {
                _laboratorio = value;
                Raise();
                Raise(nameof(TestoVuotoDettaglio));
                Ricarica();
                LaboratorioCambiato?.Invoke(this, _laboratorio);
            }
        }
    }

    /// <summary>Messaggio di dettaglio dello stato vuoto, distinto per Quiz e Laboratorio.</summary>
    public string TestoVuotoDettaglio => _laboratorio
        ? Strings.Lab_SospesiNessunaDettaglio
        : Strings.Sospesi_NessunaDettaglio;

    public SospesiView()
    {
        InitializeComponent();
        Focusable = true;
        // Step 15: Tunnel + handledEventsToo per intercettare anche eventi
        // gia' marcati Handled da ListBoxItem o ScrollViewer.
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel, handledEventsToo: true);
        // Se il focus arriva sullo UserControl stesso (es. Ctrl+Tab dal
        // MainWindow), lo trasferiamo alla RIGA selezionata (non alla ListBox,
        // che non e' focusable: vedi FocusRigaSelezionata e Trappola 21).
        GotFocus += (_, e) =>
        {
            if (e.Source == this)
                FocusRigaSelezionata(ListaPause);
        };
    }

    /// <summary>Inizializza la vista con lo storage e lo stato iniziale dell'interruttore globale.</summary>
    public void Inizializza(StorageService storage, bool laboratorio)
    {
        _laboratorio = laboratorio;
        Raise(nameof(Laboratorio));
        Raise(nameof(TestoVuotoDettaglio));
        base.Inizializza(storage);
    }

    private void OnModoQuizClick(object? sender, RoutedEventArgs e) => Laboratorio = false;

    private void OnModoLabClick(object? sender, RoutedEventArgs e) => Laboratorio = true;

    protected override void RicaricaCore()
    {
        if (_laboratorio)
        {
            var pauseLab = _storage!.CaricaPauseLab();
            foreach (var p in pauseLab.OrderByDescending(x => x.DataOraPausa))
                Sessioni.Add(new SessioneSospesaItem(p));
            return;
        }

        var pause = _storage!.CaricaPause();
        foreach (var p in pause.OrderByDescending(x => x.DataOraPausa))
            Sessioni.Add(new SessioneSospesaItem(p));
    }

    protected override string FormatSottotitolo(int count) => count switch
    {
        0 => Strings.Sospesi_NessunaPunto,
        1 => Strings.Sospesi_ContaUno,
        _ => string.Format(Strings.Sospesi_ContaMolte, count)
    };

    private void OnRigaSospesaInitialized(object? sender, EventArgs e)
    {
        if (!AppEnv.TouchMode || sender is not Grid g) return;
        g.ColumnDefinitions = new ColumnDefinitions("*");
        g.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto");
        if (g.Children.OfType<Control>().FirstOrDefault(c => Equals(c.Tag, "Azioni")) is { } azioni)
        {
            Grid.SetColumn(azioni, 0);
            Grid.SetRow(azioni, 3);
            Grid.SetRowSpan(azioni, 1);
            azioni.VerticalAlignment = VerticalAlignment.Center;
            azioni.HorizontalAlignment = HorizontalAlignment.Left;
            azioni.Margin = new Thickness(0, 10, 0, 0);
        }
    }

    protected override void EliminaDaStorage(SessioneSospesaItem item)
    {
        if (item.ELaboratorio)
            _storage!.EliminaPausaLab(item.SessioneId);
        else
            _storage!.EliminaPausa(item.SessioneId);
    }

    // ------------------------------------------------------------------ HANDLER

    private void SollevaRiprendi(SessioneSospesaItem item)
    {
        if (item.PausaLab != null)
            RiprendiLabRichiesto?.Invoke(this, item.PausaLab);
        else if (item.Pausa != null)
            RiprendiRichiesto?.Invoke(this, item.Pausa);
    }

    /// <summary>Chiede alla <c>MainView</c> di riprendere questa sessione.</summary>
    private void OnRiprendiClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b) return;
        if (b.DataContext is not SessioneSospesaItem item) return;
        SollevaRiprendi(item);
    }

    // ------------------------------------------------------------------ TASTIERA (step 8)

    /// <summary>
    /// Step 8: Invio sulla riga selezionata = Riprendi; Canc avvia conferma
    /// inline (secondo Canc = conferma definitiva); Esc annulla la conferma.
    /// </summary>
    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (ListaPause.SelectedItem is not SessioneSospesaItem item) return;

        switch (e.Key)
        {
            case Key.Enter:
                if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Button)
                    return;
                e.Handled = true;
                SollevaRiprendi(item);
                return;

            case Key.Delete:
                e.Handled = true;
                if (item.InAttesaConfermaEliminazione)
                {
                    EseguiEliminazione(item);
                }
                else
                {
                    AzzeraConferme();
                    item.InAttesaConfermaEliminazione = true;
                }
                return;

            case Key.Escape:
                if (item.InAttesaConfermaEliminazione)
                {
                    e.Handled = true;
                    item.InAttesaConfermaEliminazione = false;
                }
                return;
        }
    }
}
