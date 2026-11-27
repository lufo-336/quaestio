using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Wsa.Quiz.App.State;
using Wsa.Quiz.Core.Models;

using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;
namespace Wsa.Quiz.App.Views;

/// <summary>
/// Sub-view della <see cref="CronologiaView"/>: mostra il dettaglio domanda-per-domanda
/// di una singola sessione. Riusa il pattern di <see cref="RiepilogoView"/> per le
/// statistiche in alto e per il template della singola riga, ma:
/// - mostra TUTTE le risposte (corrette e sbagliate, con bordo colorato),
/// - ha un bottone "Indietro" invece di "Torna alla Home".
/// </summary>
public partial class CronologiaDettaglioView : UserControl, INotifyPropertyChanged
{
    /// <summary>Sollevato al click di "Torna alla cronologia". La <see cref="CronologiaView"/> torna alla lista.</summary>
    public event EventHandler? IndietroRichiesto;

    /// <summary>Sollevato quando l'utente conferma l'eliminazione di questa partita
    /// dal dettaglio. La <see cref="CronologiaView"/> esegue l'eliminazione su storage,
    /// torna alla lista e ricarica.</summary>
    public event EventHandler<string>? EliminazioneRichiesta;

    public new event PropertyChangedEventHandler? PropertyChanged;

    private readonly RisultatoQuiz _risultato;

    // ------------------------------------------------------------------ DATI

    public string Titolo { get; }
    public string Sottotitolo { get; }
    public string TitoloLista => Dettagli.Count == 0
        ? Strings.Dettaglio_DomandeSessione
        : string.Format(Strings.Dettaglio_DomandeSessioneN, Dettagli.Count);

    public string PercentualeFormattata { get; }
    public IBrush ColorePercentuale => QuizColors.PercentualeTesto(_risultato.PercentualeCorrette);
    public int TotaleDomande => _risultato.TotaleDomande;
    public int RisposteCorrette => _risultato.RisposteCorrette;
    public int RisposteErrate => _risultato.RisposteErrate;
    public int Punteggio => _risultato.Punteggio;
    public string DurataFormattata { get; }
    public string Modalita => string.IsNullOrWhiteSpace(_risultato.Modalita) ? "—" : EtichetteContenuti.Modalita(_risultato.Modalita);
    public string MateriaEtichetta => string.IsNullOrWhiteSpace(_risultato.MateriaNome) ? "—" : EtichetteContenuti.Correnti.Materia(_risultato.MateriaNome);

    public IList<DettaglioRispostaItem> Dettagli { get; }
    public bool NessunDettaglio => Dettagli.Count == 0;

    // ------------------------------------------------------------------ STATO CONFERMA ELIMINA

    private bool _inAttesaConferma;
    public bool InAttesaConfermaEliminazione
    {
        get => _inAttesaConferma;
        private set
        {
            if (_inAttesaConferma == value) return;
            _inAttesaConferma = value;
            Raise();
            Raise(nameof(NonInAttesaConferma));
        }
    }

    public bool NonInAttesaConferma => !_inAttesaConferma;

    // ------------------------------------------------------------------ COSTRUZIONE

    public CronologiaDettaglioView(RisultatoQuiz risultato)
    {
        InitializeComponent();

        if (AppEnv.TouchMode)
        {
            var g = GrigliaStat;
            g.ColumnDefinitions = new ColumnDefinitions("*,*");
            g.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto");

            Grid.SetColumn(BloccoPercentuale, 0);
            Grid.SetColumnSpan(BloccoPercentuale, 2);
            Grid.SetRow(BloccoPercentuale, 0);
            Grid.SetRowSpan(BloccoPercentuale, 1);

            PosizionaMetrica(BloccoTotale,    col: 0, row: 1);
            PosizionaMetrica(BloccoCorrette,  col: 1, row: 1);
            PosizionaMetrica(BloccoErrate,    col: 0, row: 2);
            PosizionaMetrica(BloccoPunteggio, col: 1, row: 2);

            Grid.SetColumn(BloccoDurataModalita, 0);
            Grid.SetColumnSpan(BloccoDurataModalita, 2);
            Grid.SetRow(BloccoDurataModalita, 3);

            BarraInferiore.ColumnDefinitions = new ColumnDefinitions("*");
            Grid.SetColumn(TornaBtn, 0);
            Grid.SetRow(TornaBtn, 1);
            TornaBtn.HorizontalAlignment = HorizontalAlignment.Right;
            TornaBtn.Margin = new Thickness(0, 10, 0, 0);
        }

        _risultato = risultato;

        Titolo = risultato.Abbandonato
            ? Strings.Dettaglio_SessioneAbbandonata
            : Strings.Dettaglio_Titolo;
        Sottotitolo = string.Format(Lingue.Cultura, Strings.Dettaglio_Avviata, risultato.DataOra);

        PercentualeFormattata = $"{Math.Round(risultato.PercentualeCorrette)}%";

        DurataFormattata = Core.Services.QuizService.FormattaDurata(risultato.DurataQuiz);

        Dettagli = risultato.Dettagli
            .Select(x => new DettaglioRispostaItem(x))
            .ToList();

        DataContext = this;
        QuizColors.CollegaCambioTema(this, AggiornaColoriTema);

        Focusable = true;
        AttachedToVisualTree += (_, _) => Focus();
    }

    private void AggiornaColoriTema()
    {
        Raise(nameof(ColorePercentuale));
        foreach (var item in Dettagli)
            item.NotificaCambioTema();
    }


    private void OnIndietroClick(object? sender, RoutedEventArgs e)
    {
        IndietroRichiesto?.Invoke(this, EventArgs.Empty);
    }

    private void OnEliminaClick(object? sender, RoutedEventArgs e)
    {
        InAttesaConfermaEliminazione = true;
    }

    private void OnAnnullaEliminaClick(object? sender, RoutedEventArgs e)
    {
        InAttesaConfermaEliminazione = false;
    }

    private void OnConfermaEliminaClick(object? sender, RoutedEventArgs e)
    {
        EliminazioneRichiesta?.Invoke(this, _risultato.Id);
    }

    // ------------------------------------------------------------------ TASTIERA (step 8)

    /// <summary>
    /// Step 8: Esc chiude il dettaglio e torna alla lista (= bottone Indietro).
    /// Se la conferma di eliminazione e' attiva, Esc la annulla invece di chiudere.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (InAttesaConfermaEliminazione)
            {
                InAttesaConfermaEliminazione = false;
            }
            else
            {
                IndietroRichiesto?.Invoke(this, EventArgs.Empty);
            }
            return;
        }
        base.OnKeyDown(e);
    }

    static void PosizionaMetrica(Control c, int col, int row)
    {
        Grid.SetColumn(c, col);
        Grid.SetColumnSpan(c, 1);
        Grid.SetRow(c, row);
        c.Margin = new Thickness(0, 8, 0, 0);
    }

    private void Raise([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
