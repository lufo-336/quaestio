using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Wsa.Quiz.App.State;
using Wsa.Quiz.Core.Models.Laboratorio;
using Wsa.Quiz.Core.Risorse;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Schermata di esecuzione di una sessione del Laboratorio di codice (livello base).
/// Riceve nel costruttore una <see cref="SessioneLaboratorio"/> non ancora avviata
/// e la avvia in <c>OnAttachedToVisualTree</c>.
/// </summary>
public partial class LaboratorioView : UserControl, INotifyPropertyChanged
{
    /// <summary>Sollevato quando la sessione termina (regolarmente o per abbandono).</summary>
    public event EventHandler<SessioneLaboratorio>? LaboratorioConcluso;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public SessioneLaboratorio Sessione { get; }

    private bool _avviata;
    private int _schedaPreparataIndice = -1;
    private List<string> _vociOutputMescolate = new();
    private List<string> _tessereOutputMescolate = new();

    private int? _rigaSelezionata;
    private string? _tipoSelezionato;
    private string? _correzioneSelezionata;
    private int? _indiceInputSelezionato;

    public ObservableCollection<RigaCodiceItem> RigheCodice { get; } = new();
    public ObservableCollection<MenuOutputItem> RigheMenuOutput { get; } = new();
    public ObservableCollection<TesseraOutputItem> TessereOutput { get; } = new();
    public ObservableCollection<TesseraCompostaItem> OutputComposto { get; } = new();
    public ObservableCollection<TipoDifettoItem> TipiDifettoVoci { get; } = new();
    public ObservableCollection<CorrezioneBugItem> CorrezioniBug { get; } = new();
    public ObservableCollection<InputCandidatoItem> InputCandidati { get; } = new();

    // ------------------------------------------------------------------ PROPRIETA' LEGATE DALLA UI

    public string TitoloScheda => Sessione.SchedaCorrente?.Titolo ?? string.Empty;
    public string RequisitoScheda => Sessione.SchedaCorrente?.Requisito ?? string.Empty;
    public string ChiamataScheda => Sessione.SchedaCorrente?.Chiamata ?? string.Empty;

    public string EtichettaSchedaMeta
    {
        get
        {
            string livello = Sessione.Livello switch
            {
                LivelliLab.Base => Strings.Lab_Livello_Base,
                LivelliLab.Media => Strings.Lab_Livello_Media,
                LivelliLab.Alta => Strings.Lab_Livello_Alta,
                _ => Sessione.Livello
            };
            return $"{Sessione.EtichettaScheda} · {Sessione.NomeLinguaggio} · {livello}";
        }
    }

    public bool IsPassoOutput => Sessione.Passo == PassoLab.PrevediOutput;
    public bool IsPassoOutputMenu => IsPassoOutput && Sessione.IsLivelloBase;
    public bool IsPassoOutputTessere => IsPassoOutput && !Sessione.IsLivelloBase;
    public bool OutputCompostoVuoto => OutputComposto.Count == 0;
    public bool IsPassoBug => Sessione.Passo == PassoLab.TrovaBug;
    public bool MostraPannelloTipo => IsPassoBug && Sessione.IsLivelloBase && Sessione.RigaTrovata;
    public bool MostraPannelloCorrezione => IsPassoBug && Sessione.IsLivelloMedia && Sessione.RigaTrovata;
    public bool IsPassoInput => Sessione.Passo == PassoLab.RompiFunzione;

    public string SuggerimentoPassoBug => !Sessione.RigaTrovata
        ? Strings.Lab_SuggerimentoTrovaBugRiga
        : (Sessione.IsLivelloMedia ? Strings.Lab_SuggerimentoScegliCorrezione : Strings.Lab_SuggerimentoScegliTipo);

    public string EtichettaAvanti => Sessione.EUltimoPassoDellaSessione
        ? Strings.Lab_Fine
        : Strings.Lab_Avanti;

    public bool PuoConfermare => Sessione.Passo switch
    {
        PassoLab.PrevediOutput => Sessione.IsLivelloBase
            ? RigheMenuOutput.Count > 0 && RigheMenuOutput.All(r => r.VoceSelezionata != null)
            : OutputComposto.Count > 0,
        PassoLab.TrovaBug => !Sessione.RigaTrovata
            ? _rigaSelezionata.HasValue
            : (Sessione.IsLivelloMedia
                ? !string.IsNullOrEmpty(_correzioneSelezionata)
                : !string.IsNullOrEmpty(_tipoSelezionato)),
        PassoLab.RompiFunzione => _indiceInputSelezionato.HasValue,
        _ => false
    };

    private string _feedbackTitolo = string.Empty;
    public string FeedbackTitolo
    {
        get => _feedbackTitolo;
        private set { if (_feedbackTitolo != value) { _feedbackTitolo = value; Raise(); } }
    }

    private string _feedbackDettaglio = string.Empty;
    public string FeedbackDettaglio
    {
        get => _feedbackDettaglio;
        private set { if (_feedbackDettaglio != value) { _feedbackDettaglio = value; Raise(); } }
    }

    private string _outputAttesoTesto = string.Empty;
    public string OutputAttesoTesto
    {
        get => _outputAttesoTesto;
        private set
        {
            if (_outputAttesoTesto != value)
            {
                _outputAttesoTesto = value;
                Raise();
                Raise(nameof(MostraOutputAtteso));
            }
        }
    }

    public bool MostraOutputAtteso => !string.IsNullOrEmpty(_outputAttesoTesto);

    public IBrush BarraPasso0Brush => ColoreBarraPasso(PassoLab.PrevediOutput);
    public IBrush BarraPasso1Brush => ColoreBarraPasso(PassoLab.TrovaBug);
    public IBrush BarraPasso2Brush => ColoreBarraPasso(PassoLab.RompiFunzione);

    // ------------------------------------------------------------------ COSTRUZIONE

    public LaboratorioView(SessioneLaboratorio sessione)
    {
        InitializeComponent();

        if (AppEnv.TouchMode)
        {
            RequisitoBorder.Padding = new Thickness(8, 5);
            RequisitoText.FontSize = 12;
        }

        Sessione = sessione;
        DataContext = this;

        Sessione.PropertyChanged += OnSessionePropertyChanged;
        Sessione.Concluso += OnSessioneConclusa;
        QuizColors.CollegaCambioTema(this, AggiornaColoriTema);

        PreparaSchedaSeNecessario();
        PreparaPassoCorrente();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_avviata)
        {
            _avviata = true;
            Sessione.Avvia();
        }
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Escape)
        {
            e.Handled = true;
            OnEsciClick(null, new RoutedEventArgs());
            return;
        }
        base.OnKeyDown(e);
    }

    // ------------------------------------------------------------------ SINCRONIZZAZIONE CON SESSIONE

    private void OnSessionePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SessioneLaboratorio.IndiceScheda):
            case nameof(SessioneLaboratorio.SchedaCorrente):
            case nameof(SessioneLaboratorio.VarianteCorrente):
                PreparaSchedaSeNecessario();
                PreparaPassoCorrente();
                NotificaTestata();
                break;

            case nameof(SessioneLaboratorio.Passo):
                PreparaPassoCorrente();
                NotificaPassi();
                break;

            case nameof(SessioneLaboratorio.RigaTrovata):
                Raise(nameof(SuggerimentoPassoBug));
                Raise(nameof(MostraPannelloTipo));
                Raise(nameof(MostraPannelloCorrezione));
                Raise(nameof(PuoConfermare));
                break;

            case nameof(SessioneLaboratorio.InAttesaDiAvanti):
                NotificaPassi();
                Raise(nameof(PuoConfermare));
                break;
        }
    }

    private static List<string> Mescola(IEnumerable<string> sorgente)
    {
        var lista = sorgente.ToList();
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (lista[i], lista[j]) = (lista[j], lista[i]);
        }
        return lista;
    }

    private void PreparaSchedaSeNecessario()
    {
        var variante = Sessione.VarianteCorrente;
        if (variante == null || _schedaPreparataIndice == Sessione.IndiceScheda) return;
        _schedaPreparataIndice = Sessione.IndiceScheda;

        // Voci del passo 1 base (distinte) e tessere per media/alta (mantengono eventuali righe ripetute in Output).
        _vociOutputMescolate = Mescola(variante.Output.Concat(variante.Esche).Distinct(StringComparer.Ordinal));
        _tessereOutputMescolate = Mescola(variante.Output.Concat(variante.Esche));

        RigheCodice.Clear();
        for (int i = 0; i < variante.Righe.Count; i++)
            RigheCodice.Add(new RigaCodiceItem(i + 1, variante.Righe[i]));
    }

    private void PreparaPassoCorrente()
    {
        var variante = Sessione.VarianteCorrente;
        if (variante == null) return;

        _rigaSelezionata = null;
        _tipoSelezionato = null;
        _correzioneSelezionata = null;
        _indiceInputSelezionato = null;
        FeedbackTitolo = string.Empty;
        FeedbackDettaglio = string.Empty;
        OutputAttesoTesto = string.Empty;

        bool eBug = Sessione.Passo == PassoLab.TrovaBug;
        foreach (var r in RigheCodice)
        {
            r.Interattiva = eBug && Sessione.InAttesaDiRisposta && !Sessione.RigaTrovata && r.Numero != Sessione.PrimaRigaErrata;
            r.IsSelected = false;
            r.IsCorretta = false;
            r.IsSbagliata = false;
        }

        if (Sessione.Passo == PassoLab.PrevediOutput)
        {
            RigheMenuOutput.Clear();
            TessereOutput.Clear();
            OutputComposto.Clear();

            if (Sessione.IsLivelloBase)
            {
                for (int i = 0; i < variante.Output.Count; i++)
                {
                    var item = new MenuOutputItem(i, _vociOutputMescolate);
                    item.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName == nameof(MenuOutputItem.VoceSelezionata))
                            Raise(nameof(PuoConfermare));
                    };
                    RigheMenuOutput.Add(item);
                }
            }
            else
            {
                for (int i = 0; i < _tessereOutputMescolate.Count; i++)
                    TessereOutput.Add(new TesseraOutputItem(i, _tessereOutputMescolate[i]));
            }
            Raise(nameof(OutputCompostoVuoto));
        }
        else if (Sessione.Passo == PassoLab.TrovaBug)
        {
            TipiDifettoVoci.Clear();
            CorrezioniBug.Clear();

            if (Sessione.IsLivelloMedia)
            {
                var difetto = variante.Difetti.FirstOrDefault();
                if (difetto != null)
                {
                    var opzioni = Mescola(new[] { difetto.Correzione }.Concat(difetto.Trappole));
                    for (int i = 0; i < opzioni.Count; i++)
                        CorrezioniBug.Add(new CorrezioneBugItem(i, opzioni[i]));
                }
            }
            else
            {
                foreach (var id in TipiDifetto.Tutti)
                    TipiDifettoVoci.Add(new TipoDifettoItem(id, TipiDifetto.Etichetta(id)));
            }
        }
        else if (Sessione.Passo == PassoLab.RompiFunzione)
        {
            InputCandidati.Clear();
            for (int i = 0; i < variante.Input.Count; i++)
            {
                var inp = variante.Input[i];
                InputCandidati.Add(new InputCandidatoItem(i, inp.Chiamata, inp.Atteso, inp.Effettivo, inp.Espone));
            }
        }

        Raise(nameof(PuoConfermare));
        NotificaTestata();
        NotificaPassi();
    }

    // ------------------------------------------------------------------ INTERAZIONI

    private void OnTesseraPoolClick(object? sender, RoutedEventArgs e)
    {
        if (Sessione.Passo != PassoLab.PrevediOutput || Sessione.IsLivelloBase || !Sessione.InAttesaDiRisposta) return;
        if (sender is not Button { DataContext: TesseraOutputItem tessera } || tessera.IsUsata) return;

        tessera.IsUsata = true;
        OutputComposto.Add(new TesseraCompostaItem(tessera));
        Raise(nameof(OutputCompostoVuoto));
        Raise(nameof(PuoConfermare));
        Focus();
    }

    private void OnTesseraCompostaClick(object? sender, RoutedEventArgs e)
    {
        if (Sessione.Passo != PassoLab.PrevediOutput || Sessione.IsLivelloBase || !Sessione.InAttesaDiRisposta) return;
        if (sender is not Button { DataContext: TesseraCompostaItem item }) return;

        OutputComposto.Remove(item);
        item.Origine.IsUsata = false;
        Raise(nameof(OutputCompostoVuoto));
        Raise(nameof(PuoConfermare));
        Focus();
    }

    private void OnRigaCodiceClick(object? sender, RoutedEventArgs e)
    {
        if (Sessione.Passo != PassoLab.TrovaBug || !Sessione.InAttesaDiRisposta || Sessione.RigaTrovata) return;
        if (sender is not Button { DataContext: RigaCodiceItem riga }) return;
        if (Sessione.PrimaRigaErrata == riga.Numero) return;

        _rigaSelezionata = riga.Numero;
        foreach (var r in RigheCodice)
            r.IsSelected = r.Numero == riga.Numero;

        Raise(nameof(PuoConfermare));
        Focus();
    }

    private void OnTipoDifettoClick(object? sender, RoutedEventArgs e)
    {
        if (Sessione.Passo != PassoLab.TrovaBug || !Sessione.InAttesaDiRisposta || !Sessione.RigaTrovata) return;
        if (sender is not Button { DataContext: TipoDifettoItem item }) return;

        _tipoSelezionato = item.Id;
        foreach (var t in TipiDifettoVoci)
            t.IsSelected = t.Id == item.Id;

        Raise(nameof(PuoConfermare));
        Focus();
    }

    private void OnCorrezioneBugClick(object? sender, RoutedEventArgs e)
    {
        if (Sessione.Passo != PassoLab.TrovaBug || !Sessione.InAttesaDiRisposta || !Sessione.RigaTrovata) return;
        if (sender is not Button { DataContext: CorrezioneBugItem item }) return;

        _correzioneSelezionata = item.Testo;
        foreach (var c in CorrezioniBug)
            c.IsSelected = c.Indice == item.Indice;

        Raise(nameof(PuoConfermare));
        Focus();
    }

    private void OnInputCandidatoClick(object? sender, RoutedEventArgs e)
    {
        if (Sessione.Passo != PassoLab.RompiFunzione || !Sessione.InAttesaDiRisposta) return;
        if (sender is not Button { DataContext: InputCandidatoItem item }) return;

        _indiceInputSelezionato = item.Indice;
        foreach (var inp in InputCandidati)
            inp.IsSelected = inp.Indice == item.Indice;

        Raise(nameof(PuoConfermare));
        Focus();
    }

    private void OnConfermaClick(object? sender, RoutedEventArgs e)
    {
        var variante = Sessione.VarianteCorrente;
        if (variante == null || !PuoConfermare || !Sessione.InAttesaDiRisposta) return;

        switch (Sessione.Passo)
        {
            case PassoLab.PrevediOutput:
            {
                if (Sessione.IsLivelloBase)
                {
                    var risposte = RigheMenuOutput.Select(r => r.VoceSelezionata!).ToList();
                    Sessione.RispondiOutput(risposte);

                    for (int i = 0; i < RigheMenuOutput.Count && i < variante.Output.Count; i++)
                        RigheMenuOutput[i].ImpostaEsito(variante.Output[i]);

                    OutputAttesoTesto = string.Empty;
                }
                else
                {
                    var risposte = OutputComposto.Select(t => t.Testo).ToList();
                    Sessione.RispondiOutput(risposte);

                    foreach (var t in TessereOutput)
                        t.Interattiva = false;

                    for (int i = 0; i < OutputComposto.Count; i++)
                    {
                        var tile = OutputComposto[i];
                        tile.Interattiva = false;
                        bool giusta = i < variante.Output.Count
                            && string.Equals(tile.Testo, variante.Output[i], StringComparison.Ordinal);
                        tile.IsCorretta = giusta;
                        tile.IsSbagliata = !giusta;
                    }

                    OutputAttesoTesto = Sessione.UltimoEsitoCorretto
                        ? string.Empty
                        : string.Join("\n", variante.Output);
                }

                FeedbackTitolo = Sessione.UltimoEsitoCorretto
                    ? Strings.Lab_OutputGiusto
                    : Strings.Lab_OutputSbagliato;
                FeedbackDettaglio = string.Empty;
                break;
            }

            case PassoLab.TrovaBug:
            {
                var difetto = variante.Difetti.FirstOrDefault();

                if (!Sessione.RigaTrovata)
                {
                    int rigaScelta = _rigaSelezionata!.Value;
                    Sessione.TentaRigaBug(rigaScelta);

                    if (Sessione.RigaTrovata)
                    {
                        foreach (var r in RigheCodice)
                        {
                            r.Interattiva = false;
                            r.IsSelected = false;
                            if (r.Numero == rigaScelta)
                                r.IsCorretta = true;
                        }
                        NotificaPassi();
                        Raise(nameof(PuoConfermare));
                    }
                    else if (Sessione.InAttesaDiAvanti)
                    {
                        foreach (var r in RigheCodice)
                        {
                            r.Interattiva = false;
                            r.IsSelected = false;
                            if (difetto != null && r.Numero == difetto.Riga)
                                r.IsCorretta = true;
                            else if (r.Numero == rigaScelta || r.Numero == Sessione.PrimaRigaErrata)
                                r.IsSbagliata = true;
                        }
                        FeedbackTitolo = Strings.Quiz_Sbagliato;
                        FeedbackDettaglio = difetto?.Spiegazione ?? string.Empty;
                    }
                    else
                    {
                        _rigaSelezionata = null;
                        foreach (var r in RigheCodice)
                        {
                            r.IsSelected = false;
                            if (r.Numero == rigaScelta)
                            {
                                r.IsSbagliata = true;
                                r.Interattiva = false;
                            }
                        }
                        Raise(nameof(PuoConfermare));
                    }
                    break;
                }

                int rigaConfermata = Sessione.RigaConfermata ?? _rigaSelezionata!.Value;
                if (Sessione.IsLivelloMedia)
                {
                    string corrScelta = _correzioneSelezionata!;
                    Sessione.RispondiBugCorrezione(rigaConfermata, corrScelta);

                    foreach (var c in CorrezioniBug)
                    {
                        c.Interattivo = false;
                        c.IsSelected = false;
                        if (difetto != null && string.Equals(c.Testo, difetto.Correzione, StringComparison.Ordinal))
                            c.IsCorretta = true;
                        else if (string.Equals(c.Testo, corrScelta, StringComparison.Ordinal))
                            c.IsSbagliata = true;
                    }
                }
                else
                {
                    string tipoScelto = _tipoSelezionato!;
                    Sessione.RispondiBugTipo(rigaConfermata, tipoScelto);

                    foreach (var t in TipiDifettoVoci)
                    {
                        t.Interattivo = false;
                        t.IsSelected = false;
                        if (difetto != null && string.Equals(t.Id, difetto.Tipo, StringComparison.Ordinal))
                            t.IsCorretta = true;
                        else if (string.Equals(t.Id, tipoScelto, StringComparison.Ordinal))
                            t.IsSbagliata = true;
                    }
                }

                FeedbackTitolo = Sessione.UltimoEsitoCorretto
                    ? (Sessione.UltimoSecondoTentativo ? Strings.Quiz_MezzoCorretto : Strings.Quiz_Corretto)
                    : Strings.Quiz_Sbagliato;
                FeedbackDettaglio = difetto?.Spiegazione ?? string.Empty;
                break;
            }

            case PassoLab.RompiFunzione:
            {
                int indiceScelto = _indiceInputSelezionato!.Value;
                Sessione.RispondiInput(indiceScelto);

                foreach (var inp in InputCandidati)
                {
                    inp.Interattivo = false;
                    inp.IsSelected = false;
                    inp.MostraDettaglioEsito = true;
                    if (inp.Espone)
                        inp.IsCorretta = true;
                    else if (inp.Indice == indiceScelto)
                        inp.IsSbagliata = true;
                }

                var difetto = variante.Difetti.FirstOrDefault();
                FeedbackTitolo = Sessione.UltimoEsitoCorretto ? Strings.Quiz_Corretto : Strings.Quiz_Sbagliato;
                FeedbackDettaglio = difetto?.Spiegazione ?? string.Empty;
                break;
            }
        }

        Focus();
    }

    private void OnAvantiClick(object? sender, RoutedEventArgs e)
    {
        Sessione.Avanti();
        Focus();
    }

    private async void OnEsciClick(object? sender, RoutedEventArgs e)
    {
        try { await ChiediUscitaAsync(); }
        catch (Exception) { /* best effort */ }
    }

    private async Task ChiediUscitaAsync()
    {
        var dialog = new ConfermaDialog(
            Strings.Lab_Esci,
            Strings.Lab_EsciTitolo,
            Strings.Lab_EsciDettaglio,
            Strings.Lab_Esci,
            "danger");
        await dialog.ShowOverlayAsync(this);
        Focus();

        if (dialog.Confermato)
            Sessione.Abbandona();
    }

    private void OnSessioneConclusa(object? sender, EventArgs e)
    {
        LaboratorioConcluso?.Invoke(this, Sessione);
    }

    // ------------------------------------------------------------------ COLORI & NOTIFICHE

    private IBrush ColoreBarraPasso(PassoLab passo)
    {
        if (Sessione.Passo == PassoLab.Concluso || (int)Sessione.Passo > (int)passo)
            return RisorsaBrush("VerdeBrush", QuizColors.VerdeBrush);
        if (Sessione.Passo == passo)
            return RisorsaBrush("AccentoBrush", QuizColors.VerdeBrush);
        return RisorsaBrush("RigaDebole", QuizColors.NeutroBrush);
    }

    private static IBrush RisorsaBrush(string chiave, IBrush fallback)
    {
        if (Application.Current != null &&
            Application.Current.TryFindResource(chiave, Application.Current.ActualThemeVariant, out var val) &&
            val is IBrush b)
        {
            return b;
        }
        return fallback;
    }

    private void AggiornaColoriTema()
    {
        Raise(nameof(BarraPasso0Brush));
        Raise(nameof(BarraPasso1Brush));
        Raise(nameof(BarraPasso2Brush));
        foreach (var m in RigheMenuOutput)
            m.NotificaCambioTema();
    }

    private void NotificaTestata()
    {
        Raise(nameof(TitoloScheda));
        Raise(nameof(RequisitoScheda));
        Raise(nameof(ChiamataScheda));
        Raise(nameof(EtichettaSchedaMeta));
    }

    private void NotificaPassi()
    {
        Raise(nameof(IsPassoOutput));
        Raise(nameof(IsPassoOutputMenu));
        Raise(nameof(IsPassoOutputTessere));
        Raise(nameof(OutputCompostoVuoto));
        Raise(nameof(IsPassoBug));
        Raise(nameof(SuggerimentoPassoBug));
        Raise(nameof(MostraPannelloTipo));
        Raise(nameof(MostraPannelloCorrezione));
        Raise(nameof(IsPassoInput));
        Raise(nameof(EtichettaAvanti));
        Raise(nameof(BarraPasso0Brush));
        Raise(nameof(BarraPasso1Brush));
        Raise(nameof(BarraPasso2Brush));
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
