using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Wsa.Quiz.Core.Models;

using Wsa.Quiz.App.State;
using Wsa.Quiz.Core.Risorse;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Schermata di riepilogo a fine quiz. Espone proprieta' formattate
/// (percentuale, durata, sottotitolo, lista sbagliate) per il binding XAML.
/// </summary>
public partial class RiepilogoView : UserControl, INotifyPropertyChanged
{
    public event EventHandler? TornaAllaHome;

    public new event PropertyChangedEventHandler? PropertyChanged;

    private readonly RisultatoQuiz _risultato;

    // ------------------------------------------------------------------ DATI

    public string Titolo { get; }
    public string Sottotitolo { get; }
    public string TitoloLista => Sbagliate.Count == 0
        ? Strings.Riepilogo_RisposteSbagliate
        : string.Format(Strings.Riepilogo_RisposteSbagliateN, Sbagliate.Count);

    public string PercentualeFormattata { get; }
    public IBrush ColorePercentuale => QuizColors.PercentualeTesto(_risultato.PercentualeCorrette);
    public int TotaleDomande => _risultato.TotaleDomande;
    public int RisposteCorrette => _risultato.RisposteCorrette;
    public int RisposteErrate => _risultato.RisposteErrate;
    public int Punteggio => _risultato.Punteggio;
    public string DurataFormattata { get; }
    public string Modalita => _risultato.Modalita;

    public IList<DettaglioRispostaItem> Sbagliate { get; }
    public bool NessunaSbagliata => Sbagliate.Count == 0;

    // ------------------------------------------------------------------ COSTRUZIONE

    public RiepilogoView(RisultatoQuiz risultato)
    {
        InitializeComponent();

        if (AppEnv.TouchMode)
        {
            // 5 colonne -> percentuale a tutta riga in alto, metriche 2x2 sotto.
            var g = GrigliaStat;
            g.ColumnDefinitions = new ColumnDefinitions("*,*");
            g.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto");

            // Percentuale (era Col0 RowSpan2): tutta la prima riga.
            Grid.SetColumn(BloccoPercentuale, 0);
            Grid.SetColumnSpan(BloccoPercentuale, 2);
            Grid.SetRow(BloccoPercentuale, 0);
            Grid.SetRowSpan(BloccoPercentuale, 1);

            // 4 metriche (Totale/Corrette/Errate/Punteggio) in griglia 2x2, righe 1-2.
            PosizionaMetrica(BloccoTotale,    col: 0, row: 1);
            PosizionaMetrica(BloccoCorrette,  col: 1, row: 1);
            PosizionaMetrica(BloccoErrate,    col: 0, row: 2);
            PosizionaMetrica(BloccoPunteggio, col: 1, row: 2);

            // Riga durata/modalità: ultima riga, due colonne.
            Grid.SetColumn(BloccoDurataModalita, 0);
            Grid.SetColumnSpan(BloccoDurataModalita, 2);
            Grid.SetRow(BloccoDurataModalita, 3);
        }

        _risultato = risultato;

        bool lab = risultato.Modalita == Core.Services.LaboratorioService.Modalita;
        Titolo = risultato.Abbandonato ? (lab ? Strings.Riepilogo_LabAbbandonato : Strings.Riepilogo_QuizAbbandonato)
               : risultato.TempoScaduto ? Strings.Stato_TempoScaduto
               : lab ? Strings.Riepilogo_LabConcluso : Strings.Riepilogo_QuizConcluso;
        Sottotitolo = risultato.Abbandonato
            ? Strings.Riepilogo_SottotitoloAbbandonato
            : risultato.TempoScaduto
                ? Strings.Riepilogo_SottotitoloTempoScaduto
                : Strings.Riepilogo_SottotitoloConcluso;

        PercentualeFormattata = $"{Math.Round(risultato.PercentualeCorrette)}%";

        DurataFormattata = Core.Services.QuizService.FormattaDurata(risultato.DurataQuiz);

        Sbagliate = risultato.Dettagli.Where(x => !x.Corretta).Select(x => new DettaglioRispostaItem(x)).ToList();

        DataContext = this;
        QuizColors.CollegaCambioTema(this, AggiornaColoriTema);
    }

    private void AggiornaColoriTema()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorePercentuale)));
        foreach (var item in Sbagliate)
            item.NotificaCambioTema();
    }


    private void OnTornaClick(object? sender, RoutedEventArgs e)
    {
        TornaAllaHome?.Invoke(this, EventArgs.Empty);
    }

    static void PosizionaMetrica(Control c, int col, int row)
    {
        Grid.SetColumn(c, col);
        Grid.SetColumnSpan(c, 1);
        Grid.SetRow(c, row);
        c.Margin = new Thickness(0, 8, 0, 0);
    }
}
