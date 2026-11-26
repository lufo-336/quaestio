using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Models.Laboratorio;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Passaggi sequenziali attraversati su ogni scheda del Laboratorio.
/// </summary>
public enum PassoLab
{
    PrevediOutput,
    TrovaBug,
    RompiFunzione,
    Concluso
}

/// <summary>
/// Macchina a stati di una sessione del Laboratorio di codice (livelli base e media).
/// Eredita <see cref="ObservableObject"/> ed espone stato e metodi di risposta per la <c>LaboratorioView</c>.
/// Al termine o all'abbandono produce un <see cref="RisultatoQuiz"/> compatibile con Cronologia,
/// Statistiche e Riepilogo.
/// </summary>
public class SessioneLaboratorio : ObservableObject
{
    private readonly List<(SchedaLab Scheda, VarianteLab Variante)> _coppie;
    private readonly Stopwatch _cron = new();
    private bool _avviata;
    private bool _rigaAlSecondoTentativo;

    public string NomeLinguaggio { get; }
    public string Livello { get; }
    public string MateriaNome { get; }

    /// <summary>Livello effettivo della scheda corrente (o della sessione se non caricata).</summary>
    public string LivelloCorrente => _schedaCorrente?.Livello ?? Livello;
    public bool IsLivelloBase => string.Equals(LivelloCorrente, LivelliLab.Base, StringComparison.OrdinalIgnoreCase);
    public bool IsLivelloMedia => string.Equals(LivelloCorrente, LivelliLab.Media, StringComparison.OrdinalIgnoreCase);
    public bool IsLivelloAlta => string.Equals(LivelloCorrente, LivelliLab.Alta, StringComparison.OrdinalIgnoreCase);

    /// <summary>Risultato della sessione, compilato passo per passo e finalizzato alla chiusura.</summary>
    public RisultatoQuiz Risultato { get; } = new();

    public bool Abbandonato { get; private set; }

    /// <summary>Sollevato quando la sessione termina (regolarmente o per abbandono).</summary>
    public event EventHandler? Concluso;

    // ------------------------------------------------------------------ STATO OBSERVABLE

    private SchedaLab? _schedaCorrente;
    public SchedaLab? SchedaCorrente
    {
        get => _schedaCorrente;
        private set
        {
            if (SetField(ref _schedaCorrente, value))
            {
                RaisePropertyChanged(nameof(LivelloCorrente));
                RaisePropertyChanged(nameof(IsLivelloBase));
                RaisePropertyChanged(nameof(IsLivelloMedia));
                RaisePropertyChanged(nameof(IsLivelloAlta));
            }
        }
    }

    private VarianteLab? _varianteCorrente;
    public VarianteLab? VarianteCorrente
    {
        get => _varianteCorrente;
        private set => SetField(ref _varianteCorrente, value);
    }

    private int _indiceScheda;
    /// <summary>Indice 0-based della scheda corrente.</summary>
    public int IndiceScheda
    {
        get => _indiceScheda;
        private set
        {
            if (SetField(ref _indiceScheda, value))
            {
                RaisePropertyChanged(nameof(NumeroSchedaCorrente));
                RaisePropertyChanged(nameof(EtichettaScheda));
                RaisePropertyChanged(nameof(EUltimoPassoDellaSessione));
                RaisePropertyChanged(nameof(ProgressoPercentuale));
            }
        }
    }

    public int TotaleSchede => _coppie.Count;
    public int NumeroSchedaCorrente => _coppie.Count == 0 ? 0 : _indiceScheda + 1;

    public string EtichettaScheda => TotaleSchede == 0
        ? string.Empty
        : string.Format(Strings.Lab_SchedaNDiM, NumeroSchedaCorrente, TotaleSchede);

    private PassoLab _passo = PassoLab.PrevediOutput;
    public PassoLab Passo
    {
        get => _passo;
        private set
        {
            if (SetField(ref _passo, value))
            {
                RaisePropertyChanged(nameof(NomePasso));
                RaisePropertyChanged(nameof(EUltimoPassoDellaSessione));
                RaisePropertyChanged(nameof(ProgressoPercentuale));
            }
        }
    }

    public string NomePasso => _passo switch
    {
        PassoLab.PrevediOutput => Strings.Lab_PassoOutput,
        PassoLab.TrovaBug => Strings.Lab_PassoBug,
        PassoLab.RompiFunzione => Strings.Lab_PassoInput,
        _ => string.Empty
    };

    private bool _inAttesaDiAvanti;
    /// <summary>True dopo l'invio della risposta del passo corrente, mentre si mostra l'esito.</summary>
    public bool InAttesaDiAvanti
    {
        get => _inAttesaDiAvanti;
        private set
        {
            if (SetField(ref _inAttesaDiAvanti, value))
            {
                RaisePropertyChanged(nameof(InAttesaDiRisposta));
                RaisePropertyChanged(nameof(ProgressoPercentuale));
            }
        }
    }

    public bool InAttesaDiRisposta => !_inAttesaDiAvanti && _passo != PassoLab.Concluso;

    private bool _ultimoEsitoCorretto;
    public bool UltimoEsitoCorretto
    {
        get => _ultimoEsitoCorretto;
        private set => SetField(ref _ultimoEsitoCorretto, value);
    }

    private bool _ultimoSecondoTentativo;
    /// <summary>True se l'ultimo passo chiuso è riuscito al secondo tentativo (peso ½).</summary>
    public bool UltimoSecondoTentativo
    {
        get => _ultimoSecondoTentativo;
        private set => SetField(ref _ultimoSecondoTentativo, value);
    }

    private bool _secondaPossibilitaInCorso;
    /// <summary>True dopo il primo errore di riga nel passo 2 (base/media), finché non si riprova.</summary>
    public bool SecondaPossibilitaInCorso
    {
        get => _secondaPossibilitaInCorso;
        private set => SetField(ref _secondaPossibilitaInCorso, value);
    }

    private string _indizioCorrente = string.Empty;
    /// <summary>Indizio mostrato durante la seconda possibilità nel passo 2.</summary>
    public string IndizioCorrente
    {
        get => _indizioCorrente;
        private set => SetField(ref _indizioCorrente, value);
    }

    private int? _primaRigaErrata;
    /// <summary>Numero 1-based della prima riga sbagliata scelta al passo 2, per non ripeterla.</summary>
    public int? PrimaRigaErrata
    {
        get => _primaRigaErrata;
        private set => SetField(ref _primaRigaErrata, value);
    }

    private bool _rigaTrovata;
    /// <summary>True nel passo 2 (base/media) dopo aver individuato la riga giusta e prima di scegliere tipo o correzione.</summary>
    public bool RigaTrovata
    {
        get => _rigaTrovata;
        private set => SetField(ref _rigaTrovata, value);
    }

    private int? _rigaConfermata;
    /// <summary>Numero 1-based della riga confermata come giusta nel passo 2.</summary>
    public int? RigaConfermata
    {
        get => _rigaConfermata;
        private set => SetField(ref _rigaConfermata, value);
    }

    public bool EUltimoPassoDellaSessione =>
        TotaleSchede > 0 && _indiceScheda + 1 >= TotaleSchede && _passo == PassoLab.RompiFunzione;

    public double ProgressoPercentuale
    {
        get
        {
            if (TotaleSchede == 0) return 0;
            if (_passo == PassoLab.Concluso) return 100.0;
            int passiTotali = TotaleSchede * 3;
            int completati = _indiceScheda * 3 + (int)_passo + (_inAttesaDiAvanti ? 1 : 0);
            return Math.Min(100.0, 100.0 * completati / passiTotali);
        }
    }

    // ------------------------------------------------------------------ COSTRUZIONE E AVVIO

    public SessioneLaboratorio(
        IReadOnlyList<(SchedaLab Scheda, VarianteLab Variante)> coppie,
        string nomeLinguaggio,
        string livello)
    {
        _coppie = coppie.ToList();
        NomeLinguaggio = nomeLinguaggio;
        Livello = livello;
        MateriaNome = nomeLinguaggio + LaboratorioService.SuffissoMateria;

        if (_coppie.Count > 0)
        {
            _schedaCorrente = _coppie[0].Scheda;
            _varianteCorrente = _coppie[0].Variante;
        }
    }

    /// <summary>Avvia il cronometro e porta a video la prima scheda.</summary>
    public void Avvia()
    {
        if (_avviata) return;
        _avviata = true;

        Risultato.DataOra = DateTime.Now;
        Risultato.Modalita = LaboratorioService.Modalita;
        Risultato.MateriaNome = MateriaNome;
        Risultato.CategorieSelezionate = new List<string>();

        if (_coppie.Count == 0)
        {
            Concludi(abbandonato: false);
            return;
        }

        _cron.Start();
        CaricaScheda(0);
    }

    private void CaricaScheda(int indice)
    {
        // Ordine voluto: la vista si ricostruisce su IndiceScheda e Passo, quindi
        // stato d'attesa e variante nuova devono essere già pronti quando cambiano.
        ResetStatoPasso2();
        UltimoEsitoCorretto = false;
        InAttesaDiAvanti = false;
        SchedaCorrente = _coppie[indice].Scheda;
        VarianteCorrente = _coppie[indice].Variante;
        Passo = PassoLab.PrevediOutput;
        IndiceScheda = indice;
    }

    private void ResetStatoPasso2()
    {
        _rigaAlSecondoTentativo = false;
        UltimoSecondoTentativo = false;
        PrimaRigaErrata = null;
        RigaConfermata = null;
        RigaTrovata = false;
        SecondaPossibilitaInCorso = false;
        IndizioCorrente = string.Empty;
    }

    // ------------------------------------------------------------------ RISPOSTE

    /// <summary>Registra la risposta al passo 1 ("Prevedi l'output") confrontando le righe in ordine.</summary>
    public void RispondiOutput(IReadOnlyList<string> righe)
    {
        if (InAttesaDiAvanti || Passo != PassoLab.PrevediOutput) return;
        if (SchedaCorrente == null || VarianteCorrente == null) return;

        var attese = VarianteCorrente.Output;
        bool ok = LaboratorioService.OutputCorretto(VarianteCorrente, righe);

        RegistraDettaglio(
            passoId: "output",
            nomePasso: Strings.Lab_PassoOutput,
            rispostaData: string.Join("\n", righe),
            rispostaCorretta: string.Join("\n", attese),
            corretta: ok);

        UltimoSecondoTentativo = false;
        UltimoEsitoCorretto = ok;
        InAttesaDiAvanti = true;
    }

    /// <summary>
    /// Verifica la riga scelta nel passo 2 (base o media).
    /// Se la prima scelta è sbagliata, attiva la seconda possibilità con l'indizio.
    /// Se fallisce anche la seconda, chiude il passo come errato; se è giusta,
    /// abilita <see cref="RigaTrovata"/> per chiedere tipo o correzione.
    /// </summary>
    public void TentaRigaBug(int riga)
    {
        if (InAttesaDiAvanti || Passo != PassoLab.TrovaBug || RigaTrovata) return;
        if (SchedaCorrente == null || VarianteCorrente == null || VarianteCorrente.Difetti.Count == 0) return;
        if (riga < 1 || riga > VarianteCorrente.Righe.Count) return;
        if (_primaRigaErrata == riga) return;

        var difetto = VarianteCorrente.Difetti[0];
        if (LaboratorioService.BugRigaCorretta(VarianteCorrente, riga))
        {
            _rigaAlSecondoTentativo = _primaRigaErrata.HasValue;
            RigaConfermata = riga;
            SecondaPossibilitaInCorso = false;
            IndizioCorrente = string.Empty;
            RigaTrovata = true;
            return;
        }

        if (!IsLivelloAlta && !_primaRigaErrata.HasValue)
        {
            PrimaRigaErrata = riga;
            IndizioCorrente = difetto.Indizio;
            SecondaPossibilitaInCorso = true;
            return;
        }

        string rispostaCorretta = IsLivelloMedia
            ? FormattazioneRigaCorrezione(difetto.Riga, difetto.Correzione)
            : FormattazioneRigaTipo(difetto.Riga, difetto.Tipo);

        RegistraDettaglio(
            passoId: "bug",
            nomePasso: Strings.Lab_PassoBug,
            rispostaData: string.Format(Strings.Lab_RigaN, riga),
            rispostaCorretta: rispostaCorretta,
            corretta: false,
            secondoTentativo: true);

        SecondaPossibilitaInCorso = false;
        IndizioCorrente = string.Empty;
        UltimoSecondoTentativo = false;
        UltimoEsitoCorretto = false;
        InAttesaDiAvanti = true;
    }

    /// <summary>Registra la risposta al passo 2 base ("Trova il bug": riga 1-based + tipo di difetto).</summary>
    public void RispondiBugTipo(int riga, string tipo)
    {
        if (VarianteCorrente == null || VarianteCorrente.Difetti.Count == 0) return;
        var difetto = VarianteCorrente.Difetti[0];
        bool ok = LaboratorioService.BugTipoCorretto(VarianteCorrente, riga, tipo);

        RispondiBugSingolo(
            riga,
            ok,
            rispostaData: FormattazioneRigaTipo(riga, tipo),
            rispostaCorretta: FormattazioneRigaTipo(difetto.Riga, difetto.Tipo));
    }

    /// <summary>Registra la risposta al passo 2 media ("Trova il bug": riga 1-based + correzione scelta).</summary>
    public void RispondiBugCorrezione(int riga, string correzione)
    {
        if (VarianteCorrente == null || VarianteCorrente.Difetti.Count == 0) return;
        var difetto = VarianteCorrente.Difetti[0];
        bool ok = LaboratorioService.BugCorrezioneCorretta(VarianteCorrente, riga, correzione);

        RispondiBugSingolo(
            riga,
            ok,
            rispostaData: FormattazioneRigaCorrezione(riga, correzione),
            rispostaCorretta: FormattazioneRigaCorrezione(difetto.Riga, difetto.Correzione));
    }

    private void RispondiBugSingolo(int riga, bool ok, string rispostaData, string rispostaCorretta)
    {
        if (InAttesaDiAvanti || Passo != PassoLab.TrovaBug) return;
        if (SchedaCorrente == null || VarianteCorrente == null || VarianteCorrente.Difetti.Count == 0) return;
        if (riga < 1 || riga > VarianteCorrente.Righe.Count) return;

        // Se chiamato direttamente (es. da test) su una riga sbagliata prima di RigaTrovata,
        // applica comunque la seconda possibilità sulla riga.
        if (!RigaTrovata && !LaboratorioService.BugRigaCorretta(VarianteCorrente, riga))
        {
            TentaRigaBug(riga);
            return;
        }

        bool secondoTentativo = _rigaAlSecondoTentativo || _primaRigaErrata.HasValue;

        RegistraDettaglio(
            passoId: "bug",
            nomePasso: Strings.Lab_PassoBug,
            rispostaData: rispostaData,
            rispostaCorretta: rispostaCorretta,
            corretta: ok,
            secondoTentativo: secondoTentativo);

        SecondaPossibilitaInCorso = false;
        IndizioCorrente = string.Empty;
        UltimoSecondoTentativo = ok && secondoTentativo;
        UltimoEsitoCorretto = ok;
        InAttesaDiAvanti = true;
    }

    /// <summary>Registra la risposta al passo 3 ("Rompi la funzione": indice dell'input scelto).</summary>
    public void RispondiInput(int indice)
    {
        if (InAttesaDiAvanti || Passo != PassoLab.RompiFunzione) return;
        if (SchedaCorrente == null || VarianteCorrente == null) return;
        if (indice < 0 || indice >= VarianteCorrente.Input.Count) return;

        var scelto = VarianteCorrente.Input[indice];
        bool ok = LaboratorioService.InputCorretto(VarianteCorrente, indice);
        string corretta = string.Join("\n", VarianteCorrente.Input.Where(i => i.Espone).Select(i => i.Chiamata));

        RegistraDettaglio(
            passoId: "input",
            nomePasso: Strings.Lab_PassoInput,
            rispostaData: scelto.Chiamata,
            rispostaCorretta: corretta,
            corretta: ok);

        UltimoSecondoTentativo = false;
        UltimoEsitoCorretto = ok;
        InAttesaDiAvanti = true;
    }

    /// <summary>Passa al passo successivo o alla scheda successiva (o conclude la sessione).</summary>
    public void Avanti()
    {
        if (!InAttesaDiAvanti || Passo == PassoLab.Concluso) return;

        switch (Passo)
        {
            // Reset e InAttesaDiAvanti prima di Passo: la vista prepara il passo nuovo
            // (righe cliccabili) quando cambia Passo.
            case PassoLab.PrevediOutput:
                ResetStatoPasso2();
                UltimoEsitoCorretto = false;
                InAttesaDiAvanti = false;
                Passo = PassoLab.TrovaBug;
                break;

            case PassoLab.TrovaBug:
                ResetStatoPasso2();
                UltimoEsitoCorretto = false;
                InAttesaDiAvanti = false;
                Passo = PassoLab.RompiFunzione;
                break;

            case PassoLab.RompiFunzione:
                if (_indiceScheda + 1 < TotaleSchede)
                    CaricaScheda(_indiceScheda + 1);
                else
                    Concludi(abbandonato: false);
                break;
        }
    }

    /// <summary>Abbandona la sessione mantenendo i passi già svolti.</summary>
    public void Abbandona()
    {
        if (Passo == PassoLab.Concluso) return;
        Concludi(abbandonato: true);
    }

    // ------------------------------------------------------------------ SUPPORTO E CHIUSURA

    private void RegistraDettaglio(
        string passoId,
        string nomePasso,
        string rispostaData,
        string rispostaCorretta,
        bool corretta,
        bool secondoTentativo = false)
    {
        if (SchedaCorrente == null || VarianteCorrente == null) return;

        var primoDifetto = VarianteCorrente.Difetti.FirstOrDefault();
        string categoria = primoDifetto != null
            ? TipiDifetto.EtichettaCanonica(primoDifetto.Tipo)
            : string.Empty;

        Risultato.Dettagli.Add(new DettaglioRisposta
        {
            IdDomanda = LaboratorioService.IdPasso(SchedaCorrente, VarianteCorrente, passoId),
            Categoria = categoria,
            MateriaNome = MateriaNome,
            TestoDomanda = $"{SchedaCorrente.Titolo} — {nomePasso}",
            RispostaData = rispostaData,
            RispostaCorretta = rispostaCorretta,
            Corretta = corretta,
            Spiegazione = primoDifetto?.Spiegazione ?? string.Empty,
            Tentativi = 1,
            SecondoTentativo = secondoTentativo,
            RisposteShufflate = new List<string>()
        });
    }

    private static string FormattazioneRigaTipo(int riga, string tipo)
    {
        string parteRiga = string.Format(Strings.Lab_RigaN, riga);
        string etichettaTipo = TipiDifetto.Etichetta(tipo);
        return string.IsNullOrEmpty(etichettaTipo) ? parteRiga : $"{parteRiga} · {etichettaTipo}";
    }

    private static string FormattazioneRigaCorrezione(int riga, string correzione)
    {
        string parteRiga = string.Format(Strings.Lab_RigaN, riga);
        string testo = correzione.Trim();
        return string.IsNullOrEmpty(testo) ? parteRiga : $"{parteRiga} · {testo}";
    }

    private void Concludi(bool abbandonato)
    {
        _cron.Stop();
        Abbandonato = abbandonato;
        Passo = PassoLab.Concluso;
        InAttesaDiAvanti = false;

        int effettuate = Risultato.Dettagli.Count;
        int corrette = Risultato.Dettagli.Count(d => d.Corretta);

        Risultato.Abbandonato = abbandonato;
        Risultato.DurataQuiz = _cron.Elapsed;
        Risultato.TotaleDomande = effettuate;
        Risultato.RisposteCorrette = corrette;
        Risultato.RisposteErrate = effettuate - corrette;
        Risultato.PercentualeCorrette = effettuate == 0 ? 0 : 100.0 * corrette / effettuate;
        Risultato.Punteggio = QuizService.CalcolaPunteggio(Risultato);

        Concluso?.Invoke(this, EventArgs.Empty);
    }
}
