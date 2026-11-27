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
/// Macchina a stati di una sessione del Laboratorio di codice (livelli base, media e alta).
/// Eredita <see cref="ObservableObject"/> ed espone stato e metodi di risposta per la <c>LaboratorioView</c>.
/// Al termine o all'abbandono produce un <see cref="RisultatoQuiz"/> compatibile con Cronologia,
/// Statistiche e Riepilogo.
/// </summary>
public class SessioneLaboratorio : ObservableObject
{
    private readonly List<(SchedaLab Scheda, VarianteLab Variante)> _coppie;
    private readonly Stopwatch _cron = new();
    private readonly string _linguaggioId;
    private bool _avviata;
    private bool _rigaAlSecondoTentativo;

    private string _sessioneId = Guid.NewGuid().ToString("N");
    private bool _avviataDaRipresa;
    private TimeSpan _offsetCronometro = TimeSpan.Zero;
    private int _indiceIniziale;
    private int _dettagliSchedeConcluse;

    /// <summary>
    /// Id della pausa da cui questa sessione è stata ripresa, oppure <c>null</c>
    /// se avviata da zero. Usato da <c>MainView</c> per eliminare la pausa originale
    /// al termine o all'abbandono della sessione.
    /// </summary>
    public string? IdSessionePausa => _avviataDaRipresa ? _sessioneId : null;

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

    /// <summary>Spiegazione della variante corrente (per livello alta, elenca tutti i difetti).</summary>
    public string SpiegazioneCorrente => VarianteCorrente == null
        ? string.Empty
        : FormattaSpiegazioneVariante(VarianteCorrente);

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
        _linguaggioId = RisolviLinguaggioId(_coppie, nomeLinguaggio);

        if (_coppie.Count > 0)
        {
            _schedaCorrente = _coppie[0].Scheda;
            _varianteCorrente = _coppie[0].Variante;
        }
    }

    private static string RisolviLinguaggioId(
        IReadOnlyList<(SchedaLab Scheda, VarianteLab Variante)> coppie,
        string nomeLinguaggio)
    {
        if (coppie.Count > 0 && !string.IsNullOrWhiteSpace(coppie[0].Scheda.Linguaggio))
            return coppie[0].Scheda.Linguaggio;

        foreach (var (id, nome) in LaboratorioService.Linguaggi)
        {
            if (string.Equals(nome, nomeLinguaggio, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, nomeLinguaggio, StringComparison.OrdinalIgnoreCase))
                return id;
        }

        return nomeLinguaggio;
    }

    /// <summary>Avvia il cronometro e porta a video la scheda iniziale (o quella di ripresa).</summary>
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
        CaricaScheda(_indiceIniziale);
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

    /// <summary>
    /// Registra la risposta al passo 2 alta ("Trova il bug" — review completa):
    /// valuta i segni (riga 1-based → tipo di difetto) con <see cref="LaboratorioService.PunteggioReview"/>
    /// e applica la soglia per l'archivio (1 → giusto, ½..1 → peso ½, &lt; ½ → sbagliato),
    /// senza seconda possibilità.
    /// </summary>
    public void RispondiBugReview(IReadOnlyDictionary<int, string> segni)
    {
        if (InAttesaDiAvanti || Passo != PassoLab.TrovaBug) return;
        if (SchedaCorrente == null || VarianteCorrente == null || VarianteCorrente.Difetti.Count == 0) return;
        if (segni == null || segni.Count == 0) return;

        double punteggio = LaboratorioService.PunteggioReview(VarianteCorrente, segni);
        var (ok, secondoTentativo) = LaboratorioService.EsitoReview(punteggio);

        string rispostaData = string.Join("\n", segni
            .OrderBy(kv => kv.Key)
            .Select(kv => FormattazioneRigaTipo(kv.Key, kv.Value)));

        string rispostaCorretta = string.Join("\n", VarianteCorrente.Difetti
            .OrderBy(d => d.Riga)
            .Select(d => FormattazioneRigaTipo(d.Riga, d.Tipo)));

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
                {
                    _dettagliSchedeConcluse = Risultato.Dettagli.Count;
                    CaricaScheda(_indiceScheda + 1);
                }
                else
                {
                    Concludi(abbandonato: false);
                }
                break;
        }
    }

    /// <summary>Abbandona la sessione mantenendo i passi già svolti.</summary>
    public void Abbandona()
    {
        if (Passo == PassoLab.Concluso) return;
        Concludi(abbandonato: true);
    }

    /// <summary>Ferma temporaneamente il cronometro mentre è aperto il dialogo di pausa o uscita.</summary>
    public void SospendiCronometro() => _cron.Stop();

    /// <summary>Riprende il cronometro dopo aver annullato il dialogo di pausa o uscita.</summary>
    public void RiprendiCronometro()
    {
        if (_avviata && Passo != PassoLab.Concluso)
            _cron.Start();
    }

    /// <summary>
    /// Produce uno snapshot serializzabile della sessione corrente per la ripresa futura.
    /// Salva le coppie (<c>schedaId/varianteId</c>) in ordine, l'indice della scheda in corso e
    /// i dettagli delle sole schede già concluse (scartando eventuali passi già svolti sulla
    /// scheda corrente, che alla ripresa ripartirà dal passo 1).
    /// </summary>
    public PausaLaboratorio CreaPausa()
    {
        _cron.Stop();

        int daTenere = Math.Clamp(_dettagliSchedeConcluse, 0, Risultato.Dettagli.Count);
        return new PausaLaboratorio
        {
            SessioneId = _sessioneId,
            DataOraPausa = DateTime.Now,
            Linguaggio = _linguaggioId,
            Livello = Livello,
            Coppie = _coppie.Select(c => $"{c.Scheda.Id}/{c.Variante.Id}").ToList(),
            IndiceScheda = _indiceScheda,
            Dettagli = Risultato.Dettagli.Take(daTenere).ToList(),
            TempoTrascorso = _cron.Elapsed + _offsetCronometro
        };
    }

    /// <summary>
    /// Ricostruisce una <see cref="SessioneLaboratorio"/> da una <see cref="PausaLaboratorio"/>
    /// salvata, posizionandola all'inizio (passo 1) della scheda <see cref="PausaLaboratorio.IndiceScheda"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se una scheda o variante della pausa non è più presente fra le schede caricate
    /// (o se la pausa non è valida).
    /// </exception>
    public static SessioneLaboratorio RiprendiDa(PausaLaboratorio pausa, IReadOnlyList<SchedaLab> schede)
    {
        if (pausa == null || pausa.Coppie == null || pausa.Coppie.Count == 0)
            throw new InvalidOperationException(Strings.Lab_PausaOrfana);

        if (pausa.IndiceScheda < 0 || pausa.IndiceScheda >= pausa.Coppie.Count)
            throw new InvalidOperationException(Strings.Lab_PausaOrfana);

        string langId = pausa.Linguaggio;
        string nomeLinguaggio = pausa.Linguaggio;
        foreach (var (id, nome) in LaboratorioService.Linguaggi)
        {
            if (string.Equals(id, pausa.Linguaggio, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nome, pausa.Linguaggio, StringComparison.OrdinalIgnoreCase))
            {
                langId = id;
                nomeLinguaggio = nome;
                break;
            }
        }

        var perChiave = new Dictionary<(string SchedaId, string VarianteId), (SchedaLab Scheda, VarianteLab Variante)>();
        foreach (var s in schede)
        {
            if (!string.Equals(s.Linguaggio, langId, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var v in s.Varianti)
                perChiave.TryAdd((s.Id, v.Id), (s, v));
        }

        var coppie = new List<(SchedaLab Scheda, VarianteLab Variante)>(pausa.Coppie.Count);
        foreach (var voce in pausa.Coppie)
        {
            if (string.IsNullOrEmpty(voce))
                throw new InvalidOperationException(Strings.Lab_PausaOrfana);

            int sep = voce.IndexOf('/');
            if (sep <= 0 || sep >= voce.Length - 1)
                throw new InvalidOperationException(Strings.Lab_PausaOrfana);

            string schedaId = voce[..sep];
            string varianteId = voce[(sep + 1)..];
            if (!perChiave.TryGetValue((schedaId, varianteId), out var coppia))
                throw new InvalidOperationException(Strings.Lab_PausaOrfana);

            coppie.Add(coppia);
        }

        var sessione = new SessioneLaboratorio(coppie, nomeLinguaggio, pausa.Livello)
        {
            _sessioneId = string.IsNullOrWhiteSpace(pausa.SessioneId)
                ? Guid.NewGuid().ToString("N")
                : pausa.SessioneId,
            _avviataDaRipresa = true,
            _offsetCronometro = pausa.TempoTrascorso,
            _indiceIniziale = pausa.IndiceScheda
        };

        // Stato iniziale pronto sui campi privati prima che la vista legga le proprietà o si sottoscriva.
        sessione._indiceScheda = pausa.IndiceScheda;
        sessione._schedaCorrente = coppie[pausa.IndiceScheda].Scheda;
        sessione._varianteCorrente = coppie[pausa.IndiceScheda].Variante;

        if (pausa.Dettagli != null)
        {
            foreach (var dett in pausa.Dettagli)
                sessione.Risultato.Dettagli.Add(dett);
            sessione._dettagliSchedeConcluse = sessione.Risultato.Dettagli.Count;
        }

        return sessione;
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
            Spiegazione = FormattaSpiegazioneVariante(VarianteCorrente),
            Tentativi = 1,
            SecondoTentativo = secondoTentativo,
            RisposteShufflate = new List<string>()
        });
    }

    private static string FormattaSpiegazioneVariante(VarianteLab variante)
    {
        if (variante.Difetti.Count == 0) return string.Empty;
        if (variante.Difetti.Count == 1) return variante.Difetti[0].Spiegazione;

        return string.Join("\n", variante.Difetti
            .Select(d => $"{FormattazioneRigaTipo(d.Riga, d.Tipo)}: {d.Spiegazione}"));
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
        // Assegnamenti di stato letti dalla vista prima delle notifiche su Passo e InAttesaDiAvanti.
        int effettuate = Risultato.Dettagli.Count;
        int corrette = Risultato.Dettagli.Count(d => d.Corretta);

        Risultato.Abbandonato = abbandonato;
        Risultato.DurataQuiz = _cron.Elapsed + _offsetCronometro;
        Risultato.TotaleDomande = effettuate;
        Risultato.RisposteCorrette = corrette;
        Risultato.RisposteErrate = effettuate - corrette;
        Risultato.PercentualeCorrette = effettuate == 0 ? 0 : 100.0 * corrette / effettuate;
        Risultato.Punteggio = QuizService.CalcolaPunteggio(Risultato);

        InAttesaDiAvanti = false;
        Passo = PassoLab.Concluso;

        Concluso?.Invoke(this, EventArgs.Empty);
    }
}
