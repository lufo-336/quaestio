# Pausa congela timer + Modalità a tempo — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** La pausa congela il cronometro, e una nuova modalità a tempo (countdown + colore di allerta) termina la partita allo scadere dei minuti scelti, combinabile con tutte le altre opzioni.

**Architecture:** Code-behind + INPC (no MVVM), come tutto il resto della GUI. Due campi additivi nel modello Core condiviso (`OpzioniQuiz.LimiteTempoMinuti`, `RisultatoQuiz.TempoScaduto`). La logica di countdown/scadenza vive in `SessioneQuiz` (state machine), guidata dal `DispatcherTimer` già presente. La pausa usa `Stopwatch.Stop()/Start()` (accumula, niente offset). Colori centralizzati in `QuizColors`.

**Tech Stack:** .NET 10, Avalonia 12, C#. Branch: `feature/timer-pausa-modalita-tempo` (già creato).

**Testing:** Il repo non ha progetto di test e segue smoke test manuali (CLAUDE.md). Verifica per task = `dotnet build Wsa.Quiz.sln` verde. Smoke test finale in coda al piano (Task 8). Non introdurre un framework di test (YAGNI + norme progetto).

---

## File Structure

| File | Responsabilità della modifica |
|---|---|
| `wsa.quiz.core/Models/OpzioniQuiz.cs` | nuovo campo `LimiteTempoMinuti` |
| `wsa.quiz.core/Models/RisultatoQuiz.cs` | nuovo campo `TempoScaduto` |
| `wsa.quiz.app/QuizColors.cs` | helper colore countdown + brush neutro |
| `wsa.quiz.app/State/SessioneQuiz.cs` | sospendi/riprendi cronometro, modalità a tempo, `TempoBrush`, `Concludi(tempoScaduto)` |
| `wsa.quiz.app/Views/QuizView.axaml.cs` | sospendi/riprendi attorno alla modale pausa |
| `wsa.quiz.app/Views/QuizView.axaml` | bind `Foreground` del tempo a `TempoBrush` |
| `wsa.quiz.app/Views/HomeView.axaml(.cs)` | UI + observable + avvio modalità a tempo |
| `wsa.quiz.app/State/RisultatoCronologiaItem.cs` | `TempoScaduto` + `StatoEtichetta` |
| `wsa.quiz.app/Views/CronologiaView.axaml` | badge "Tempo scaduto" |
| `wsa.quiz.app/Views/RiepilogoView.axaml.cs` | titolo/sottotitolo per timeout |

---

## Task 1: Campi nel modello Core

**Files:**
- Modify: `wsa.quiz.core/Models/OpzioniQuiz.cs`
- Modify: `wsa.quiz.core/Models/RisultatoQuiz.cs`

- [ ] **Step 1: Aggiungi `LimiteTempoMinuti` a `OpzioniQuiz`**

In `wsa.quiz.core/Models/OpzioniQuiz.cs`, dopo la proprietà `LimiteDomande` (riga 21):

```csharp
    /// <summary>Se >0, la sessione termina automaticamente dopo questi minuti (modalità a tempo).</summary>
    public int LimiteTempoMinuti { get; set; }
```

- [ ] **Step 2: Aggiungi `TempoScaduto` a `RisultatoQuiz`**

In `wsa.quiz.core/Models/RisultatoQuiz.cs`, dopo `Abbandonato` (riga 25):

```csharp
    /// <summary>True se la sessione è terminata per scadenza del limite di tempo (modalità a tempo). Additivo: i record JSON vecchi restano deserializzabili.</summary>
    public bool TempoScaduto { get; set; }
```

- [ ] **Step 3: Build**

Run: `dotnet build Wsa.Quiz.sln`
Expected: build OK, 0 errori.

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.core/Models/OpzioniQuiz.cs wsa.quiz.core/Models/RisultatoQuiz.cs
git commit -m "feat(core): campi LimiteTempoMinuti e TempoScaduto"
```

---

## Task 2: Helper colore countdown in QuizColors

**Files:**
- Modify: `wsa.quiz.app/QuizColors.cs`

- [ ] **Step 1: Aggiungi brush neutro e helper `TempoRimanente`**

In `wsa.quiz.app/QuizColors.cs`, dopo la riga `private static readonly IBrush _rossoBrush = ...` (riga 23) aggiungi il brush neutro:

```csharp
    // Colore neutro del testo (usato dal cronometro fuori soglia di allerta).
    public const string TestoNeutro = "#1F1F1F";
    private static readonly IBrush _neutroBrush = new SolidColorBrush(Color.Parse(TestoNeutro));
```

Poi, dopo il metodo `Esito` (riga 68), aggiungi:

```csharp
    /// <summary>
    /// Colore del countdown in modalità a tempo in base al tempo rimanente:
    /// rosso &lt;= 10s, ambra &lt;= 60s, neutro oltre.
    /// </summary>
    public static IBrush TempoRimanente(TimeSpan rimanente)
    {
        if (rimanente.TotalSeconds <= 10) return _rossoBrush;
        if (rimanente.TotalSeconds <= 60) return _ambraBrush;
        return _neutroBrush;
    }

    /// <summary>Colore neutro del cronometro (modalità non a tempo / countdown sopra soglia).</summary>
    public static IBrush TempoNeutro => _neutroBrush;
```

- [ ] **Step 2: Aggiungi `using System;`**

In cima a `wsa.quiz.app/QuizColors.cs`, se non presente, aggiungi sotto `using Avalonia.Media;`:

```csharp
using System;
```

(Serve per `TimeSpan`.)

- [ ] **Step 3: Build**

Run: `dotnet build Wsa.Quiz.sln`
Expected: build OK.

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.app/QuizColors.cs
git commit -m "feat(app): QuizColors.TempoRimanente per countdown a tempo"
```

---

## Task 3: Pausa congela il cronometro (Feature 1)

**Files:**
- Modify: `wsa.quiz.app/State/SessioneQuiz.cs`
- Modify: `wsa.quiz.app/Views/QuizView.axaml.cs:147-181`

- [ ] **Step 1: Aggiungi `SospendiCronometro` / `RiprendiCronometro` a `SessioneQuiz`**

In `wsa.quiz.app/State/SessioneQuiz.cs`, subito prima della regione PAUSA (prima del commento `// ----...---- PAUSA: ESPORTA / RIPRENDI`, riga 486), aggiungi:

```csharp
    // ------------------------------------------------------------------ SOSPENSIONE TEMPORANEA (modale pausa)

    /// <summary>
    /// Congela cronometro e timer mentre la modale pausa è aperta. Non produce
    /// uno snapshot: la sessione resta viva. <see cref="Stopwatch"/> accumula,
    /// quindi al <see cref="RiprendiCronometro"/> il tempo riparte da dov'era.
    /// </summary>
    public void SospendiCronometro()
    {
        _cron.Stop();
        _timer.Stop();
    }

    /// <summary>Riprende cronometro e timer dopo "Annulla" sulla modale pausa.</summary>
    public void RiprendiCronometro()
    {
        _cron.Start();
        if (MostraTempo) _timer.Start();
    }
```

- [ ] **Step 2: Sospendi/riprendi attorno alla modale in `QuizView`**

In `wsa.quiz.app/Views/QuizView.axaml.cs`, metodo `ApriMenuPausaAsync` (riga 147). Aggiungi la sospensione prima di mostrare la modale e la ripresa sul ramo Annulla. Sostituisci il corpo del metodo da `var dialog = new PausaDialog();` fino alla fine con:

```csharp
        _sessione.SospendiCronometro();

        var dialog = new PausaDialog();
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner != null)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();

        Focus();

        switch (dialog.Risultato)
        {
            case PausaDialogResult.Abbandona:
                _sessione.Abbandona();
                return;

            case PausaDialogResult.Salva:
                try
                {
                    var pausa = _sessione.EsportaPausa();
                    _storage.SalvaPausa(pausa);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SalvaPausa failed: {ex}");
                    return;
                }
                QuizMessoInPausa?.Invoke(this, EventArgs.Empty);
                return;

            default: // Annulla
                _sessione.RiprendiCronometro();
                return;
        }
```

(Nota: `EsportaPausa` e `Abbandona`/`Concludi` chiamano già `_cron.Stop()`/`_timer.Stop()`, idempotenti dopo `SospendiCronometro`.)

- [ ] **Step 3: Build**

Run: `dotnet build Wsa.Quiz.sln`
Expected: build OK.

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.app/State/SessioneQuiz.cs wsa.quiz.app/Views/QuizView.axaml.cs
git commit -m "feat(app): la modale pausa congela il cronometro"
```

---

## Task 4: Modalità a tempo in SessioneQuiz (Feature 2 — core logica)

**Files:**
- Modify: `wsa.quiz.app/State/SessioneQuiz.cs`

- [ ] **Step 1: Aggiungi `using Avalonia.Media;`**

In cima a `wsa.quiz.app/State/SessioneQuiz.cs`, dopo `using Avalonia.Threading;` (riga 6):

```csharp
using Avalonia.Media;
```

- [ ] **Step 2: Aggiungi `HaLimiteTempo` e proprietà observable `TempoBrush`**

In `SessioneQuiz`, subito dopo la proprietà `MostraTempo` (riga 153), aggiungi:

```csharp
    /// <summary>True se è attiva la modalità a tempo.</summary>
    public bool HaLimiteTempo => Opzioni.LimiteTempoMinuti > 0;

    private IBrush _tempoBrush = QuizColors.TempoNeutro;
    /// <summary>Colore del testo del cronometro: neutro, o ambra/rosso in modalità a tempo vicino alla scadenza.</summary>
    public IBrush TempoBrush { get => _tempoBrush; private set => SetField(ref _tempoBrush, value); }
```

- [ ] **Step 3: Aggiorna `MostraTempo` per includere la modalità a tempo**

In `SessioneQuiz` sostituisci la riga 153:

```csharp
    public bool MostraTempo => Opzioni.Cronometro;
```

con:

```csharp
    public bool MostraTempo => Opzioni.Cronometro || HaLimiteTempo;
```

- [ ] **Step 4: Avvia il timer anche in modalità a tempo**

In `Avvia()` sostituisci la riga 297:

```csharp
        if (Opzioni.Cronometro) _timer.Start();
```

con:

```csharp
        if (MostraTempo) _timer.Start();
```

- [ ] **Step 5: Countdown e scadenza in `AggiornaTempo`**

Sostituisci interamente il metodo `AggiornaTempo` (righe 480-484):

```csharp
    private void AggiornaTempo()
    {
        var elapsed = _cron.Elapsed + _offsetCronometro;

        if (HaLimiteTempo)
        {
            var rimanente = TimeSpan.FromMinutes(Opzioni.LimiteTempoMinuti) - elapsed;
            if (rimanente <= TimeSpan.Zero)
            {
                Tempo = "00:00";
                TempoBrush = QuizColors.TempoRimanente(TimeSpan.Zero);
                Concludi(abbandonato: false, tempoScaduto: true);
                return;
            }
            Tempo = QuizService.FormattaDurata(rimanente);
            TempoBrush = QuizColors.TempoRimanente(rimanente);
        }
        else
        {
            Tempo = QuizService.FormattaDurata(elapsed);
        }
    }
```

- [ ] **Step 6: Estendi `Concludi` con `tempoScaduto`**

Sostituisci la firma e l'inizio di `Concludi` (righe 449-454):

```csharp
    private void Concludi(bool abbandonato)
    {
        _cron.Stop();
        _timer.Stop();
        Abbandonato = abbandonato;
        Risultato.Abbandonato = abbandonato;
        Risultato.DurataQuiz = _cron.Elapsed + _offsetCronometro;
```

con:

```csharp
    private void Concludi(bool abbandonato, bool tempoScaduto = false)
    {
        _cron.Stop();
        _timer.Stop();
        Abbandonato = abbandonato;
        Risultato.Abbandonato = abbandonato;
        Risultato.TempoScaduto = tempoScaduto;
        Risultato.DurataQuiz = _cron.Elapsed + _offsetCronometro;
```

(Le chiamate esistenti `Concludi(false)` / `Concludi(true)` restano valide: `tempoScaduto` ha default `false`.)

- [ ] **Step 7: Build**

Run: `dotnet build Wsa.Quiz.sln`
Expected: build OK.

- [ ] **Step 8: Commit**

```bash
git add wsa.quiz.app/State/SessioneQuiz.cs
git commit -m "feat(app): modalità a tempo con countdown e scadenza in SessioneQuiz"
```

---

## Task 5: Bind del colore tempo in QuizView.axaml

**Files:**
- Modify: `wsa.quiz.app/Views/QuizView.axaml:129-135`

- [ ] **Step 1: Aggiungi `Foreground="{Binding TempoBrush}"` al TextBlock del tempo**

In `wsa.quiz.app/Views/QuizView.axaml`, il TextBlock alle righe 129-135. Sostituiscilo con:

```xml
                <TextBlock Grid.Column="1"
                           Text="{Binding Tempo}"
                           Foreground="{Binding TempoBrush}"
                           FontFamily="Consolas, monospace"
                           FontSize="16"
                           FontWeight="SemiBold"
                           VerticalAlignment="Center"
                           Margin="20,0,12,0"
                           IsVisible="{Binding MostraTempo}"/>
```

- [ ] **Step 2: Build**

Run: `dotnet build Wsa.Quiz.sln`
Expected: build OK.

- [ ] **Step 3: Commit**

```bash
git add wsa.quiz.app/Views/QuizView.axaml
git commit -m "feat(app): colore del cronometro bindato a TempoBrush"
```

---

## Task 6: UI modalità a tempo nella Home

**Files:**
- Modify: `wsa.quiz.app/Views/HomeView.axaml.cs`
- Modify: `wsa.quiz.app/Views/HomeView.axaml:158-172`

- [ ] **Step 1: Aggiungi le observable `ModalitaTempo` e `MinutiTempo`**

In `wsa.quiz.app/Views/HomeView.axaml.cs`, dopo la proprietà `LimiteN` (riga 74), aggiungi:

```csharp
    private bool _modalitaTempo;
    public bool ModalitaTempo
    {
        get => _modalitaTempo;
        set { if (_modalitaTempo != value) { _modalitaTempo = value; Raise(); AggiornaRiepilogo(); } }
    }

    private decimal _minutiTempo = 10;
    public decimal MinutiTempo
    {
        get => _minutiTempo;
        set { if (_minutiTempo != value) { _minutiTempo = value; Raise(); AggiornaRiepilogo(); } }
    }
```

- [ ] **Step 2: Imposta `LimiteTempoMinuti` all'avvio**

In `OnAvviaClick`, dentro l'inizializzatore di `OpzioniQuiz` (righe 323-333), aggiungi la riga dopo `LimiteDomande`:

```csharp
            LimiteTempoMinuti       = ModalitaTempo ? (int)MinutiTempo : 0,
```

Risultato (per chiarezza, l'oggetto completo):

```csharp
        var opz = new OpzioniQuiz
        {
            NomeModalita            = nomeMod,
            MateriaNomeLabel        = materiaLabel,
            RandomizzaOrdineDomande = RandomizzaOrdine,
            Rotazione               = Rotazione,
            Cronometro              = Cronometro,
            LimiteDomande           = LimitaDomande ? (int)LimiteN : 0,
            LimiteTempoMinuti       = ModalitaTempo ? (int)MinutiTempo : 0,
            Categorie               = catSel.Select(c => c.ChiaveCombinata).ToList(),
            Materie                 = materieSel.Select(m => m.Id).ToList()
        };
```

- [ ] **Step 3: Aggiungi "· max N min" al riepilogo**

In `AggiornaRiepilogo`, sostituisci il blocco che costruisce `RiepilogoSelezione` (righe 292-294):

```csharp
        RiepilogoSelezione = nMaterie == 0
            ? testoMaterie
            : $"{testoMaterie}  ·  {testoCat}  ·  {testoDom}";
```

con:

```csharp
        string testoTempo = (ModalitaTempo && (int)MinutiTempo > 0)
            ? $"  ·  max {(int)MinutiTempo} min"
            : string.Empty;

        RiepilogoSelezione = nMaterie == 0
            ? testoMaterie
            : $"{testoMaterie}  ·  {testoCat}  ·  {testoDom}{testoTempo}";
```

- [ ] **Step 4: Aggiungi il blocco UI nella sezione Opzioni**

In `wsa.quiz.app/Views/HomeView.axaml`, subito dopo la `<Grid>` del "Limita a" (che si chiude con `</Grid>` a riga 172) e prima della chiusura `</StackPanel>` della zona Opzioni (riga 173), aggiungi:

```xml
                        <Grid ColumnDefinitions="Auto,Auto,Auto" Margin="0,4,0,0">
                            <CheckBox Content="Modalità a tempo"
                                      IsChecked="{Binding ModalitaTempo, Mode=TwoWay}"
                                      VerticalAlignment="Center"
                                      ToolTip.Tip="Allo scadere dei minuti la partita termina automaticamente. Combinabile con le altre opzioni."/>
                            <NumericUpDown Grid.Column="1"
                                           Minimum="1" Maximum="180"
                                           Value="{Binding MinutiTempo, Mode=TwoWay}"
                                           Increment="5"
                                           Width="120"
                                           Margin="10,0,8,0"
                                           IsEnabled="{Binding ModalitaTempo}"/>
                            <TextBlock Grid.Column="2"
                                       Text="minuti"
                                       VerticalAlignment="Center"/>
                        </Grid>
```

- [ ] **Step 5: Build**

Run: `dotnet build Wsa.Quiz.sln`
Expected: build OK.

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.app/Views/HomeView.axaml wsa.quiz.app/Views/HomeView.axaml.cs
git commit -m "feat(app): opzione Modalità a tempo nella Home"
```

---

## Task 7: Esito "Tempo scaduto" in Riepilogo e Cronologia

**Files:**
- Modify: `wsa.quiz.app/Views/RiepilogoView.axaml.cs:51-54`
- Modify: `wsa.quiz.app/State/RisultatoCronologiaItem.cs:29-30,67-68`
- Modify: `wsa.quiz.app/Views/CronologiaView.axaml:127-134`

- [ ] **Step 1: Titolo/sottotitolo nel Riepilogo**

In `wsa.quiz.app/Views/RiepilogoView.axaml.cs`, sostituisci le righe 51-54:

```csharp
        Titolo = risultato.Abbandonato ? "Quiz abbandonato" : "Quiz concluso";
        Sottotitolo = risultato.Abbandonato
            ? "Hai abbandonato la sessione. Le risposte gia' date sono state comunque salvate."
            : "Ecco com'e' andata.";
```

con:

```csharp
        Titolo = risultato.Abbandonato ? "Quiz abbandonato"
               : risultato.TempoScaduto ? "Tempo scaduto"
               : "Quiz concluso";
        Sottotitolo = risultato.Abbandonato
            ? "Hai abbandonato la sessione. Le risposte gia' date sono state comunque salvate."
            : risultato.TempoScaduto
                ? "Il tempo e' scaduto. Ecco com'e' andata fino a qui."
                : "Ecco com'e' andata.";
```

- [ ] **Step 2: `TempoScaduto` + `StatoEtichetta` in `RisultatoCronologiaItem`**

In `wsa.quiz.app/State/RisultatoCronologiaItem.cs`, dopo la proprietà `Abbandonato` (riga 29) aggiungi:

```csharp
    public bool TempoScaduto { get; }
```

Poi sostituisci le righe 67-68:

```csharp
        Abbandonato = r.Abbandonato;
        StatoEtichetta = r.Abbandonato ? "Abbandonato" : "Concluso";
```

con:

```csharp
        Abbandonato = r.Abbandonato;
        TempoScaduto = r.TempoScaduto;
        StatoEtichetta = r.Abbandonato ? "Abbandonato"
                       : r.TempoScaduto ? "Tempo scaduto"
                       : "Concluso";
```

- [ ] **Step 3: Badge "Tempo scaduto" in CronologiaView**

In `wsa.quiz.app/Views/CronologiaView.axaml`, dentro lo `StackPanel` Grid.Column="1" (righe 127-134), dopo il TextBlock "Abbandonato" (che termina a riga 133), aggiungi un secondo TextBlock:

```xml
                                        <TextBlock Text="⏱ Tempo scaduto"
                                                   FontSize="10"
                                                   Foreground="#B8860B"
                                                   IsVisible="{Binding TempoScaduto}"/>
```

Lo `StackPanel` risultante (righe 127-135) deve essere:

```xml
                                    <StackPanel Grid.Column="1" Spacing="2" Margin="8,2,4,2">
                                        <TextBlock Text="{Binding DataFormattata}"
                                                   FontSize="13" FontWeight="SemiBold"/>
                                        <TextBlock Text="Abbandonato"
                                                   FontSize="10"
                                                   Foreground="#B85450"
                                                   IsVisible="{Binding Abbandonato}"/>
                                        <TextBlock Text="⏱ Tempo scaduto"
                                                   FontSize="10"
                                                   Foreground="#B8860B"
                                                   IsVisible="{Binding TempoScaduto}"/>
                                    </StackPanel>
```

- [ ] **Step 4: Build**

Run: `dotnet build Wsa.Quiz.sln`
Expected: build OK.

- [ ] **Step 5: Commit**

```bash
git add wsa.quiz.app/Views/RiepilogoView.axaml.cs wsa.quiz.app/State/RisultatoCronologiaItem.cs wsa.quiz.app/Views/CronologiaView.axaml
git commit -m "feat(app): esito 'Tempo scaduto' in riepilogo e cronologia"
```

---

## Task 8: Smoke test manuale

**Files:** nessuno (verifica a mano).

- [ ] **Step 1: Avvia l'app**

Run: `dotnet run --project wsa.quiz.app`

- [ ] **Step 2: Quiz a tempo da 1 minuto**

Seleziona una materia, spunta "Modalità a tempo", imposta 1 minuto (il NumericUpDown ha minimo 1; per testare rapidamente puoi anche lasciare 1), avvia.
Atteso:
- Il cronometro è visibile anche con "Mostra cronometro" spento, e fa il **countdown** (scende).
- Sotto 60s diventa ambra, sotto 10s diventa rosso.
- Allo zero la partita **termina subito** e va al riepilogo con titolo "Tempo scaduto"; la domanda eventualmente a video non risposta NON è conteggiata.

- [ ] **Step 3: Pausa congela il timer**

Avvia un quiz (a tempo o con cronometro normale), apri la modale Pausa, aspetta qualche secondo, premi Annulla.
Atteso: il tempo (countdown o salita) NON è avanzato durante la modale.

- [ ] **Step 4: Combinazioni**

Avvia un quiz a tempo + rotazione + "Limita a N domande" insieme.
Atteso: si avvia regolarmente; allo scadere termina comunque.

- [ ] **Step 5: Sospendi/Riprendi a tempo**

In un quiz a tempo scegli "Salva e esci", poi vai nei Sospesi e Riprendi.
Atteso: il countdown riprende coerente col tempo già trascorso prima della sospensione.

- [ ] **Step 6: Cronometro normale invariato**

Quiz con solo "Mostra cronometro" (no modalità a tempo).
Atteso: conta in **salita**, colore neutro, nessuna terminazione automatica.

- [ ] **Step 7: Cronologia**

Apri la tab Cronologia.
Atteso: la partita scaduta mostra il badge "⏱ Tempo scaduto" (ambra), non "Abbandonato".

---

## Self-Review

- **Spec coverage:** Feature 1 (Task 3). Feature 2: campi (Task 1), colore (Task 2), logica countdown/scadenza/Concludi (Task 4), bind colore (Task 5), UI Home (Task 6), esito riepilogo+cronologia (Task 7), smoke test inclusi nello spec (Task 8). "Domanda in corso ignorata" → garantita dal fatto che la corrente non è in `Dettagli` al momento della scadenza (Task 4, Step 5: nessun add). Pausa+resume a tempo → coperto da campi persistiti esistenti + `MostraTempo` aggiornato (Task 4) e smoke test Step 5.
- **Placeholder scan:** nessun TBD/TODO; ogni step ha codice concreto e comandi.
- **Type consistency:** `TempoBrush` (IBrush), `HaLimiteTempo`/`MostraTempo` (bool), `Concludi(bool, bool=false)`, `QuizColors.TempoRimanente(TimeSpan)`/`TempoNeutro`, `LimiteTempoMinuti` (int), `TempoScaduto` (bool), `ModalitaTempo`/`MinutiTempo` — nomi coerenti fra i task.
