# Import materie/domande da JSON — "Gestione contenuti" — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nuova 5ª tab "Gestione contenuti" che permette di creare una materia o aggiungere domande a una esistente caricando un file `.json`, con validazione, anteprima e conferma; i contenuti importati restano locali al dispositivo. La schermata espone anche la guida e il motore per generare domande bilanciate con un'IA.

**Architecture:** Nuovo `ContenutiUtenteService` (in `Wsa.Quiz.Core`) legge/scrive `materie.json` + `domande/**` dentro `CartellaUtenteDefault()/ContenutiUtente/` (filesystem reale, scrivibile su desktop e Android). `MainView.Caricamento()` unisce le materie/domande built-in (via `IFonteDati`, invariato) con quelle utente. La nuova `GestioneContenutiView` riusa `TabellaViewBase<TItem>` per l'elenco materie + rimozione inline (stesso pattern di `SospesiView`/`CronologiaView`), e aggiunge i due flussi di import con `IStorageProvider` di Avalonia per i file picker (introdotto per la prima volta nel repo). La guida (`GUIDA_GENERAZIONE_DOMANDE.md`) e il motore (`quiz-engine.js`) vengono copiati nel repo e incorporati come `AvaloniaResource`, scaricabili dalla schermata.

**Tech Stack:** .NET 10, Avalonia 12.0.2 (Fluent), C# code-behind + INPC (no MVVM), `IStorageProvider` per i file picker.

## Global Constraints

- **Persistenza locale al dispositivo**: i contenuti importati vivono SOLO in `CartellaUtenteDefault()/ContenutiUtente/` (es. `%APPDATA%\WsaQuiz\ContenutiUtente` su Windows). Non si scrive mai nei file built-in (`materie.json`/`domande/**` in root repo o embedded) — quelli restano sola lettura via `IFonteDati`, invariato.
- **Desktop**: la nuova 5ª tab compare anche su desktop (non è touch-only); tutto il resto del comportamento desktop resta invariato.
- **`net10.0`**, **Avalonia 12.0.2** (API v12).
- **Compiled bindings**: `x:DataType` obbligatorio su ogni `UserControl`/`DataTemplate` che usa `{Binding}` (Trappola 5 in CLAUDE.md). Gli elenchi di sole stringhe (anteprima/errori) non usano `{Binding}` e non ne hanno bisogno.
- **Errori di validazione**: un solo campo non valido rifiuta l'intero file importato (nessun import parziale in presenza di errori strutturali).
- **Duplicati** (append a materia esistente): confronto per testo esatto di `domanda` contro le domande già caricate per quella materia; si saltano silenziosamente, si importa il resto.
- **Nessun framework di test nel repo** (verificato: nessun progetto xunit/nunit/mstest). Per la logica pura (task 3-4) si usa un harness scratch fuori dal repo (`%TEMP%\wsaquiz-verify\`, stesso pattern già usato per M2/M3 — vedi `docs/superpowers/plans/2026-06-19-m3-touch-friendly-layout.md:37`). Per la UI: build verde + verifica manuale desktop/device (nessun test automatico UI nel repo).
- **APK installabile** solo con `dotnet build wsa.quiz.android -c Release` (Debug non produce APK). Prima dei build Android: `$env:ANDROID_HOME = "C:\Users\<utente>\AppData\Local\Android\Sdk"` (verificare il percorso corretto sul PC locale attuale, potrebbe differire da quello della VM).
- Spec: `docs/superpowers/specs/2026-07-02-import-domande-json-design.md`.

---

## File Structure

- `wsa.quiz.core/Models/Materia.cs` — modifica: campo `Importata` (non serializzato).
- `wsa.quiz.core/Services/StorageService.cs` — modifica: 3 metodi `private` → `internal` (riuso in `ContenutiUtenteService`).
- `wsa.quiz.core/Services/ValidatoreImportDomande.cs` — **nuovo**: validazione strutturale (porting mirato da `quiz-engine.js`).
- `wsa.quiz.core/Services/ContenutiUtenteService.cs` — **nuovo**: storage/merge dei contenuti importati.
- `generazione-domande/GUIDA_GENERAZIONE_DOMANDE.md` — **nuovo** (copia da `G:\Il mio Drive\ITS Corso\MODELLO - Generatore Domande Quiz.md`).
- `generazione-domande/quiz-engine.js` — **nuovo** (copia da `G:\Il mio Drive\ITS Corso\MODELLO - quiz-engine.js`).
- `wsa.quiz.app/Wsa.Quiz.App.csproj` — modifica: `AvaloniaResource` per i due file guida.
- `wsa.quiz.app/MainView.axaml` + `.axaml.cs` — modifica: merge dati utente in `Caricamento()`, 5ª sezione/tab/icona.
- `wsa.quiz.app/State/MateriaListItem.cs` — **nuovo**: wrapper riga materia per `TabellaViewBase`.
- `wsa.quiz.app/Views/GestioneContenutiView.axaml` + `.axaml.cs` — **nuovo**: la schermata.
- `README.md` — modifica: sezioni "Aggiungere materie..." e "Generare nuove domande con un'IA".

---

### Task 1: Modello `Materia` — flag `Importata`

**Files:**
- Modify: `wsa.quiz.core/Models/Materia.cs`

**Interfaces:**
- Produces: `Materia.Importata` (`bool`, `[JsonIgnore]`, default `false`).

- [ ] **Step 1: Aggiungere il campo**

In `wsa.quiz.core/Models/Materia.cs`, dopo la proprietà `Formato` (riga 24), aggiungere:

```csharp
    /// <summary>
    /// True se la materia è stata importata dall'utente via "Gestione
    /// contenuti" (ContenutiUtenteService), false se è built-in (caricata da
    /// IFonteDati). Calcolato al caricamento in MainView, mai
    /// serializzato/deserializzato da/verso JSON.
    /// </summary>
    [JsonIgnore]
    public bool Importata { get; set; }
```

- [ ] **Step 2: Build libreria core**

Run: `dotnet build "wsa.quiz.core/Wsa.Quiz.Core.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 3: Commit**

```bash
git add wsa.quiz.core/Models/Materia.cs
git commit -m "feat(core): flag Materia.Importata per contenuti utente"
```

---

### Task 2: `StorageService` — esporre gli helper di I/O riusabili

**Files:**
- Modify: `wsa.quiz.core/Services/StorageService.cs:162, 226, 237`

**Interfaces:**
- Produces: `StorageService.GeneraId(Domanda)`, `StorageService.ScriviAtomico(string, string)`, `StorageService.LeggiFileCondiviso(string)` — tutti `internal static`, riusabili da `ContenutiUtenteService` (stesso assembly `Wsa.Quiz.Core`).

- [ ] **Step 1: Cambiare la visibilità dei tre metodi**

In `wsa.quiz.core/Services/StorageService.cs`:

Riga 162, da:
```csharp
    private static string GeneraId(Domanda d)
```
a:
```csharp
    internal static string GeneraId(Domanda d)
```

Riga 226, da:
```csharp
    private static void ScriviAtomico(string percorso, string contenuto)
```
a:
```csharp
    internal static void ScriviAtomico(string percorso, string contenuto)
```

Riga 237, da:
```csharp
    private static string LeggiFileCondiviso(string percorso)
```
a:
```csharp
    internal static string LeggiFileCondiviso(string percorso)
```

Nessun'altra riga cambia: sono solo modificatori di accesso, comportamento identico.

- [ ] **Step 2: Build libreria core**

Run: `dotnet build "wsa.quiz.core/Wsa.Quiz.Core.csproj" -c Debug`
Expected: `Errori: 0` (nessuna chiamata esistente a questi metodi è `public`/esterna all'assembly, quindi nessuna regressione).

- [ ] **Step 3: Commit**

```bash
git add wsa.quiz.core/Services/StorageService.cs
git commit -m "refactor(core): esporre helper I/O di StorageService come internal"
```

---

### Task 3: `ValidatoreImportDomande` — validazione strutturale

**Files:**
- Create: `wsa.quiz.core/Services/ValidatoreImportDomande.cs`
- Test: harness scratch `%TEMP%\wsaquiz-verify\Program.cs` (fuori dal repo)

**Interfaces:**
- Consumes: `Wsa.Quiz.Core.Models.Domanda`.
- Produces: `ValidatoreImportDomande.Valida(string json) -> (List<Domanda> domande, List<string> errori)`. Se `errori.Count > 0`, `domande` è vuota (nessun import parziale).

- [ ] **Step 1: Creare il validatore**

`wsa.quiz.core/Services/ValidatoreImportDomande.cs`:

```csharp
using System.Text.Json;
using Wsa.Quiz.Core.Models;

namespace Wsa.Quiz.Core.Services;

/// <summary>
/// Validazione strutturale di un file JSON di domande caricato dall'utente
/// (schermata "Gestione contenuti"). Porting mirato dei controlli del
/// motore esterno "quiz-engine.js" (vedi generazione-domande/): SOLO
/// struttura (campi, indici, duplicati nel file), NON l'audit di
/// bilanciamento lunghezze L/S/M/Q, che resta uno strumento per chi scrive
/// le domande, non un gate d'import.
/// Un solo errore rifiuta l'intero file: la lista domande restituita è
/// vuota se errori.Count > 0.
/// </summary>
public static class ValidatoreImportDomande
{
    private static readonly JsonSerializerOptions OpzioniLettura = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 64
    };

    public static (List<Domanda> domande, List<string> errori) Valida(string json)
    {
        var errori = new List<string>();
        List<Domanda>? domande;
        try
        {
            domande = JsonSerializer.Deserialize<List<Domanda>>(json, OpzioniLettura);
        }
        catch (JsonException ex)
        {
            errori.Add($"JSON non valido: {ex.Message}");
            return (new List<Domanda>(), errori);
        }

        if (domande == null || domande.Count == 0)
        {
            errori.Add("Il file non contiene domande (array JSON vuoto o assente).");
            return (new List<Domanda>(), errori);
        }

        for (int i = 0; i < domande.Count; i++)
        {
            var d = domande[i];
            int n = i + 1;
            if (string.IsNullOrWhiteSpace(d.DomandaTesto))
                errori.Add($"#{n}: domanda mancante");
            if (d.Risposte == null || d.Risposte.Count < 2)
                errori.Add($"#{n}: servono almeno 2 risposte");
            else if (d.RispostaCorretta < 0 || d.RispostaCorretta >= d.Risposte.Count)
                errori.Add($"#{n}: rispostaCorretta fuori range");
            if (d.Risposte != null && d.Risposte.Distinct().Count() != d.Risposte.Count)
                errori.Add($"#{n}: risposte duplicate nella stessa domanda");
            if (string.IsNullOrWhiteSpace(d.Spiegazione))
                errori.Add($"#{n}: spiegazione mancante");
        }

        var testi = domande.Select(d => d.DomandaTesto).ToList();
        for (int i = 0; i < testi.Count; i++)
        {
            if (testi.IndexOf(testi[i]) != i)
                errori.Add($"#{i + 1}: domanda duplicata nel file (\"{Tronca(testi[i])}\")");
        }

        return errori.Count > 0 ? (new List<Domanda>(), errori) : (domande, errori);
    }

    private static string Tronca(string s) => s.Length <= 40 ? s : s[..40] + "...";
}
```

Aggiungere `using System.Linq;` in cima al file (per `.Distinct()`/`.Select()`/`.IndexOf`).

- [ ] **Step 2: Build libreria core**

Run: `dotnet build "wsa.quiz.core/Wsa.Quiz.Core.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 3: Creare l'harness di verifica scratch**

```bash
dotnet new console -o "%TEMP%\wsaquiz-verify" --force
dotnet add "%TEMP%\wsaquiz-verify" reference "wsa.quiz.core\Wsa.Quiz.Core.csproj"
```

- [ ] **Step 4: Scrivere il Program.cs di verifica**

`%TEMP%\wsaquiz-verify\Program.cs`:

```csharp
using Wsa.Quiz.Core.Services;

int falliti = 0;
void Check(string nome, bool condizione)
{
    Console.WriteLine(condizione ? $"OK  - {nome}" : $"FAIL- {nome}");
    if (!condizione) falliti++;
}

// 1) JSON valido -> nessun errore, domande popolate
string jsonValido = """
[
  { "categoria":"Basi", "domanda":"Cos'e' X?", "risposte":["A","B","C","D"], "rispostaCorretta":1, "spiegazione":"perche'" }
]
""";
var (d1, e1) = ValidatoreImportDomande.Valida(jsonValido);
Check("JSON valido: 0 errori", e1.Count == 0);
Check("JSON valido: 1 domanda", d1.Count == 1);

// 2) rispostaCorretta fuori range -> rifiuta tutto
string jsonIndiceFuoriRange = """
[
  { "categoria":"Basi", "domanda":"Cos'e' X?", "risposte":["A","B"], "rispostaCorretta":5, "spiegazione":"perche'" }
]
""";
var (d2, e2) = ValidatoreImportDomande.Valida(jsonIndiceFuoriRange);
Check("Indice fuori range: rifiuta tutto (0 domande)", d2.Count == 0);
Check("Indice fuori range: almeno 1 errore", e2.Count >= 1);

// 3) domanda duplicata nel file -> rifiuta tutto
string jsonDuplicato = """
[
  { "categoria":"Basi", "domanda":"Uguale?", "risposte":["A","B"], "rispostaCorretta":0, "spiegazione":"x" },
  { "categoria":"Basi", "domanda":"Uguale?", "risposte":["C","D"], "rispostaCorretta":1, "spiegazione":"y" }
]
""";
var (d3, e3) = ValidatoreImportDomande.Valida(jsonDuplicato);
Check("Duplicato nel file: rifiuta tutto (0 domande)", d3.Count == 0);
Check("Duplicato nel file: almeno 1 errore", e3.Count >= 1);

// 4) spiegazione mancante -> rifiuta tutto
string jsonSenzaSpiegazione = """
[
  { "categoria":"Basi", "domanda":"Cos'e' X?", "risposte":["A","B"], "rispostaCorretta":0, "spiegazione":"" }
]
""";
var (d4, e4) = ValidatoreImportDomande.Valida(jsonSenzaSpiegazione);
Check("Spiegazione mancante: rifiuta tutto", d4.Count == 0 && e4.Count >= 1);

// 5) array vuoto -> errore
var (d5, e5) = ValidatoreImportDomande.Valida("[]");
Check("Array vuoto: errore", e5.Count == 1 && d5.Count == 0);

Console.WriteLine(falliti == 0 ? "\nTUTTI I CHECK OK" : $"\n{falliti} CHECK FALLITI");
Environment.Exit(falliti == 0 ? 0 : 1);
```

- [ ] **Step 5: Eseguire l'harness**

Run: `dotnet run --project "%TEMP%\wsaquiz-verify"`
Expected: tutte le righe `OK  - ...`, ultima riga `TUTTI I CHECK OK`, exit code 0.

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.core/Services/ValidatoreImportDomande.cs
git commit -m "feat(core): validazione strutturale import domande JSON"
```

---

### Task 4: `ContenutiUtenteService` — storage e merge dei contenuti importati

**Files:**
- Create: `wsa.quiz.core/Services/ContenutiUtenteService.cs`
- Test: harness scratch `%TEMP%\wsaquiz-verify\Program.cs` (esteso)

**Interfaces:**
- Consumes: `StorageService.GeneraId/ScriviAtomico/LeggiFileCondiviso` (Task 2), `Materia`, `Domanda`.
- Produces:
  - `ContenutiUtenteService(string cartellaUtente)` — costruttore.
  - `List<Materia> CaricaMaterieUtente()`
  - `List<Domanda> CaricaDomandeUtente(Materia materia)`
  - `static string GeneraIdMateria(string nome, IEnumerable<string> idEsistenti)`
  - `Materia CreaMateria(string nome, List<Domanda> domande)`
  - `(int aggiunte, int duplicati) AggiungiDomande(string materiaId, List<Domanda> nuoveDomande, IEnumerable<Domanda> domandeEsistenti)`
  - `void RimuoviMateria(string materiaId)`

- [ ] **Step 1: Creare il servizio**

`wsa.quiz.core/Services/ContenutiUtenteService.cs`:

```csharp
using System.Globalization;
using System.Text;
using System.Text.Json;
using Wsa.Quiz.Core.Models;

namespace Wsa.Quiz.Core.Services;

/// <summary>
/// Gestisce i contenuti (materie/domande) importati dall'utente via
/// "Gestione contenuti": vivono in &lt;cartellaUtente&gt;/ContenutiUtente/, mai
/// nei file built-in (che restano sola lettura via IFonteDati). Stessa
/// cartella utente di cronologia/pause (StorageService.CartellaUtenteDefault()),
/// quindi scrivibile identicamente su desktop e Android.
/// </summary>
public class ContenutiUtenteService
{
    private readonly string _cartellaContenuti;
    private readonly string _fileMaterie;

    private static readonly JsonSerializerOptions OpzioniScrittura = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions OpzioniLettura = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 64
    };

    public ContenutiUtenteService(string cartellaUtente)
    {
        _cartellaContenuti = Path.Combine(Path.GetFullPath(cartellaUtente), "ContenutiUtente");
        _fileMaterie = Path.Combine(_cartellaContenuti, "materie.json");
        Directory.CreateDirectory(_cartellaContenuti);
    }

    // ---------------------------------------------------------------- LETTURA

    public List<Materia> CaricaMaterieUtente()
    {
        if (!File.Exists(_fileMaterie)) return new List<Materia>();
        string json = StorageService.LeggiFileCondiviso(_fileMaterie);
        return JsonSerializer.Deserialize<List<Materia>>(json, OpzioniLettura) ?? new List<Materia>();
    }

    public List<Domanda> CaricaDomandeUtente(Materia materia)
    {
        string cartellaMateria = Path.Combine(_cartellaContenuti, "domande", materia.Id);
        var risultato = new List<Domanda>();
        if (!Directory.Exists(cartellaMateria)) return risultato;

        foreach (var file in Directory.EnumerateFiles(cartellaMateria, "*.json"))
        {
            string json = StorageService.LeggiFileCondiviso(file);
            var domande = JsonSerializer.Deserialize<List<Domanda>>(json, OpzioniLettura) ?? new List<Domanda>();
            foreach (var d in domande)
            {
                d.MateriaId = materia.Id;
                d.MateriaNome = materia.Nome;
            }
            risultato.AddRange(domande);
        }

        var idVisti = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in risultato)
        {
            string baseId = StorageService.GeneraId(d);
            string finale = baseId;
            int suffisso = 2;
            while (!idVisti.Add(finale))
            {
                finale = $"{baseId}-{suffisso}";
                suffisso++;
            }
            d.Id = finale;
        }

        return risultato;
    }

    // ---------------------------------------------------------------- ID MATERIA

    /// <summary>Slug dal nome (minuscolo, senza accenti, spazi->'-'), con suffisso numerico su collisione.</summary>
    public static string GeneraIdMateria(string nome, IEnumerable<string> idEsistenti)
    {
        string slug = Slug(nome);
        if (string.IsNullOrEmpty(slug)) slug = "materia";

        var esistenti = new HashSet<string>(idEsistenti, StringComparer.OrdinalIgnoreCase);
        string finale = slug;
        int suffisso = 2;
        while (esistenti.Contains(finale))
        {
            finale = $"{slug}-{suffisso}";
            suffisso++;
        }
        return finale;
    }

    private static string Slug(string nome)
    {
        string normalizzato = nome.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in normalizzato)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is ' ' or '-' or '_') sb.Append('-');
        }
        string risultato = sb.ToString();
        while (risultato.Contains("--")) risultato = risultato.Replace("--", "-");
        return risultato.Trim('-');
    }

    // ---------------------------------------------------------------- SCRITTURA

    /// <summary>Crea una nuova materia utente con il suo primo file di domande. Non tocca i file built-in.</summary>
    public Materia CreaMateria(string nome, List<Domanda> domande)
    {
        var materie = CaricaMaterieUtente();
        string id = GeneraIdMateria(nome, materie.Select(m => m.Id));
        var materia = new Materia
        {
            Id = id,
            Nome = nome,
            Cartella = $"ContenutiUtente/domande/{id}",
            Formato = "standard"
        };
        materie.Add(materia);
        ScriviMaterie(materie);
        ScriviDomande(id, domande, "import.json");
        return materia;
    }

    /// <summary>
    /// Aggiunge domande a una materia (built-in o utente) già esistente,
    /// saltando quelle il cui testo coincide con una già presente. Scrive un
    /// nuovo file timestampato: non tocca eventuali file già scritti.
    /// </summary>
    public (int aggiunte, int duplicati) AggiungiDomande(string materiaId, List<Domanda> nuoveDomande, IEnumerable<Domanda> domandeEsistenti)
    {
        var testiVisti = new HashSet<string>(domandeEsistenti.Select(d => d.DomandaTesto), StringComparer.Ordinal);
        var davveroNuove = nuoveDomande.Where(d => testiVisti.Add(d.DomandaTesto)).ToList();
        int duplicati = nuoveDomande.Count - davveroNuove.Count;

        if (davveroNuove.Count > 0)
            ScriviDomande(materiaId, davveroNuove, $"import-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");

        return (davveroNuove.Count, duplicati);
    }

    /// <summary>Rimuove una materia utente (indice + tutti i suoi file di domande). No-op se l'id non è utente.</summary>
    public void RimuoviMateria(string materiaId)
    {
        var materie = CaricaMaterieUtente();
        int rimosse = materie.RemoveAll(m => m.Id == materiaId);
        if (rimosse == 0) return;

        ScriviMaterie(materie);

        string cartellaMateria = Path.Combine(_cartellaContenuti, "domande", materiaId);
        if (Directory.Exists(cartellaMateria))
            Directory.Delete(cartellaMateria, recursive: true);
    }

    private void ScriviMaterie(List<Materia> materie)
    {
        string json = JsonSerializer.Serialize(materie, OpzioniScrittura);
        StorageService.ScriviAtomico(_fileMaterie, json);
    }

    private void ScriviDomande(string materiaId, List<Domanda> domande, string nomeFile)
    {
        string cartellaMateria = Path.Combine(_cartellaContenuti, "domande", materiaId);
        Directory.CreateDirectory(cartellaMateria);
        string json = JsonSerializer.Serialize(domande, OpzioniScrittura);
        StorageService.ScriviAtomico(Path.Combine(cartellaMateria, nomeFile), json);
    }
}
```

- [ ] **Step 2: Build libreria core**

Run: `dotnet build "wsa.quiz.core/Wsa.Quiz.Core.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 3: Estendere l'harness di verifica**

Aggiungere in fondo a `%TEMP%\wsaquiz-verify\Program.cs` (prima della riga `Console.WriteLine(falliti == 0 ...)`):

```csharp
// 6) ContenutiUtenteService: crea materia, ricarica, aggiungi con duplicati, rimuovi
using Wsa.Quiz.Core.Models;

string cartellaTest = Path.Combine(Path.GetTempPath(), "wsaquiz-verify-data-" + Guid.NewGuid().ToString("N"));
var svc = new ContenutiUtenteService(cartellaTest);

var domandeIniziali = new List<Domanda>
{
    new() { Categoria = "T", DomandaTesto = "Prima?", Risposte = new() { "a", "b" }, RispostaCorretta = 0, Spiegazione = "s" },
    new() { Categoria = "T", DomandaTesto = "Seconda?", Risposte = new() { "a", "b" }, RispostaCorretta = 1, Spiegazione = "s" }
};
var materiaCreata = svc.CreaMateria("Materia Di Prova", domandeIniziali);
Check("CreaMateria: id slug corretto", materiaCreata.Id == "materia-di-prova");

var materieRilette = svc.CaricaMaterieUtente();
Check("CaricaMaterieUtente: 1 materia", materieRilette.Count == 1);

var domandeRilette = svc.CaricaDomandeUtente(materiaCreata);
Check("CaricaDomandeUtente: 2 domande", domandeRilette.Count == 2);
Check("CaricaDomandeUtente: Id assegnati", domandeRilette.All(d => !string.IsNullOrEmpty(d.Id)));

var nuoveConDuplicato = new List<Domanda>
{
    new() { Categoria = "T", DomandaTesto = "Prima?", Risposte = new() { "a", "b" }, RispostaCorretta = 0, Spiegazione = "s" }, // duplicata
    new() { Categoria = "T", DomandaTesto = "Terza?", Risposte = new() { "a", "b" }, RispostaCorretta = 0, Spiegazione = "s" }
};
var (aggiunte, duplicati) = svc.AggiungiDomande(materiaCreata.Id, nuoveConDuplicato, domandeRilette);
Check("AggiungiDomande: 1 aggiunta", aggiunte == 1);
Check("AggiungiDomande: 1 duplicato saltato", duplicati == 1);
Check("CaricaDomandeUtente dopo append: 3 domande", svc.CaricaDomandeUtente(materiaCreata).Count == 3);

svc.RimuoviMateria(materiaCreata.Id);
Check("RimuoviMateria: 0 materie rimaste", svc.CaricaMaterieUtente().Count == 0);
Check("RimuoviMateria: cartella domande cancellata", !Directory.Exists(Path.Combine(cartellaTest, "ContenutiUtente", "domande", materiaCreata.Id)));

Directory.Delete(cartellaTest, recursive: true);
```

- [ ] **Step 4: Eseguire l'harness**

Run: `dotnet run --project "%TEMP%\wsaquiz-verify"`
Expected: tutte le righe `OK  - ...`, `TUTTI I CHECK OK`, exit code 0.

- [ ] **Step 5: Commit**

```bash
git add wsa.quiz.core/Services/ContenutiUtenteService.cs
git commit -m "feat(core): ContenutiUtenteService per import materie/domande utente"
```

---

### Task 5: Guida e motore — copia nel repo + risorsa incorporata

**Files:**
- Create: `generazione-domande/GUIDA_GENERAZIONE_DOMANDE.md`
- Create: `generazione-domande/quiz-engine.js`
- Modify: `wsa.quiz.app/Wsa.Quiz.App.csproj:28-31`

**Interfaces:**
- Produces: risorse `avares://Wsa.Quiz.App/Assets/Guida/GUIDA_GENERAZIONE_DOMANDE.md` e `avares://Wsa.Quiz.App/Assets/Guida/quiz-engine.js`, leggibili via `AssetLoader` (usate in Task 11).

- [ ] **Step 1: Copiare i due file dal Drive nel repo**

```bash
mkdir -p "generazione-domande"
cp "G:\Il mio Drive\ITS Corso\MODELLO - Generatore Domande Quiz.md" "generazione-domande\GUIDA_GENERAZIONE_DOMANDE.md"
cp "G:\Il mio Drive\ITS Corso\MODELLO - quiz-engine.js" "generazione-domande\quiz-engine.js"
```

(Eseguire dalla root del repo, `C:\Users\<utente>\Documents\Quiz\wsa.quiz`. Contenuto invariato: nessuna modifica al testo della guida o al codice del motore.)

- [ ] **Step 2: Aggiungere le risorse al csproj dell'app**

In `wsa.quiz.app/Wsa.Quiz.App.csproj`, righe 28-31, da:

```xml
  <ItemGroup>
    <AvaloniaResource Include="..\materie.json" Link="Assets\materie.json" />
    <AvaloniaResource Include="..\domande\**\*.json" Link="Assets\domande\%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>
```

a:

```xml
  <ItemGroup>
    <AvaloniaResource Include="..\materie.json" Link="Assets\materie.json" />
    <AvaloniaResource Include="..\domande\**\*.json" Link="Assets\domande\%(RecursiveDir)%(Filename)%(Extension)" />
    <AvaloniaResource Include="..\generazione-domande\GUIDA_GENERAZIONE_DOMANDE.md" Link="Assets\Guida\GUIDA_GENERAZIONE_DOMANDE.md" />
    <AvaloniaResource Include="..\generazione-domande\quiz-engine.js" Link="Assets\Guida\quiz-engine.js" />
  </ItemGroup>
```

- [ ] **Step 3: Build libreria app**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 4: Verificare che le risorse siano incorporate**

Aggiungere temporaneamente in `%TEMP%\wsaquiz-verify` (o verificare più semplicemente via `ilspycmd`/`dotnet` non necessario): rimandata alla verifica funzionale in Task 11 (il pulsante "Scarica guida" apre e legge la risorsa). Nessun check separato qui: il build che include `AvaloniaResource` fallisce già se i percorsi `Include` non esistono, quindi il build verde di Step 3 è già garanzia sufficiente.

- [ ] **Step 5: Commit**

```bash
git add generazione-domande/GUIDA_GENERAZIONE_DOMANDE.md generazione-domande/quiz-engine.js wsa.quiz.app/Wsa.Quiz.App.csproj
git commit -m "docs: guida e motore generazione domande incorporati nell'app"
```

---

### Task 6: Merge built-in + utente nel caricamento dati

**Files:**
- Modify: `wsa.quiz.app/MainView.axaml.cs:24-27` (campi), `:206-260` (`Caricamento()`)

**Interfaces:**
- Consumes: `ContenutiUtenteService` (Task 4).
- Produces: campo `MainView._contenutiUtente` (`ContenutiUtenteService?`); dopo `Caricamento()`, `_materie`/`_tutteDomande` includono anche i contenuti utente, con `Materia.Importata = true` sulle materie utente.

- [ ] **Step 1: Aggiungere il campo**

In `wsa.quiz.app/MainView.axaml.cs`, dopo il campo `_mappaDomande` (riga 27), aggiungere:

```csharp
    private ContenutiUtenteService? _contenutiUtente;
```

- [ ] **Step 2: Merge in `Caricamento()`**

Nello stesso file, dentro `Caricamento()`, sostituire (righe 227-230):

```csharp
            _storage = new StorageService(fonte, cartellaUtente);
            _materie = _storage.CaricaMaterie();
            _tutteDomande = _storage.CaricaTutteLeDomande(_materie);
            _mappaDomande = _tutteDomande.ToDictionary(d => d.Id, d => d);
```

con:

```csharp
            _storage = new StorageService(fonte, cartellaUtente);
            _contenutiUtente = new ContenutiUtenteService(cartellaUtente);

            _materie = _storage.CaricaMaterie();
            var materieUtente = _contenutiUtente.CaricaMaterieUtente();
            foreach (var m in materieUtente) m.Importata = true;
            _materie.AddRange(materieUtente);

            _tutteDomande = _storage.CaricaTutteLeDomande(_materie);
            foreach (var m in _materie)
                _tutteDomande.AddRange(_contenutiUtente.CaricaDomandeUtente(m));

            _mappaDomande = _tutteDomande.ToDictionary(d => d.Id, d => d);
```

Nota: `_storage.CaricaTutteLeDomande(_materie)` include ormai anche le materie utente nell'elenco passato, ma per queste `_fonte.ElencaJson(materia.Cartella)` restituisce sempre sequenza vuota (built-in non le conosce), quindi non produce domande — l'unica fonte per le materie utente è `_contenutiUtente.CaricaDomandeUtente`. Per le materie built-in, lo stesso ciclo aggiunge eventuali domande aggiunte via "Gestione contenuti" (append), sommandole a quelle built-in già caricate.

`Wsa.Quiz.Core.Services` è già importato (riga 13 esistente): nessun nuovo `using` necessario.

- [ ] **Step 3: Build app**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 4: Verifica manuale desktop (merge senza UI dedicata ancora)**

1. Avviare `dotnet run --project wsa.quiz.desktop`.
2. Chiudere l'app. Creare a mano `%APPDATA%\WsaQuiz\ContenutiUtente\materie.json`:
   ```json
   [{"id":"prova-merge","nome":"Prova Merge","cartella":"ContenutiUtente/domande/prova-merge","formato":"standard"}]
   ```
   e `%APPDATA%\WsaQuiz\ContenutiUtente\domande\prova-merge\import.json`:
   ```json
   [{"categoria":"T","domanda":"Domanda di prova?","risposte":["a","b"],"rispostaCorretta":0,"spiegazione":"s"}]
   ```
3. Riavviare `dotnet run --project wsa.quiz.desktop`.
4. Verificare: `StatoHeaderText` (header) mostra una materia e una domanda in più rispetto a prima; "Prova Merge" compare nel selettore materie della Home.
5. Cancellare `%APPDATA%\WsaQuiz\ContenutiUtente` al termine (pulizia dati di test).

- [ ] **Step 5: Commit**

```bash
git add wsa.quiz.app/MainView.axaml.cs
git commit -m "feat(app): merge materie/domande utente nel caricamento dati"
```

---

### Task 7: `GestioneContenutiView` — elenco materie + rimozione (skeleton)

**Files:**
- Create: `wsa.quiz.app/State/MateriaListItem.cs`
- Create: `wsa.quiz.app/Views/GestioneContenutiView.axaml`
- Create: `wsa.quiz.app/Views/GestioneContenutiView.axaml.cs`

**Interfaces:**
- Consumes: `TabellaViewBase<TItem>` (`wsa.quiz.app/Views/TabellaViewBase.axaml.cs`), `ContenutiUtenteService` (Task 4).
- Produces: `GestioneContenutiView.Inizializza(StorageService, ContenutiUtenteService, List<Materia>, List<Domanda>)`; evento `GestioneContenutiView.ContenutiCambiati`.

- [ ] **Step 1: Creare il wrapper riga materia**

`wsa.quiz.app/State/MateriaListItem.cs`:

```csharp
using Wsa.Quiz.App.Views;
using Wsa.Quiz.Core.Models;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Wrapper di una riga della tabella materie in "Gestione contenuti".
/// Observable solo per la conferma inline dell'eliminazione (solo le
/// materie importate sono eliminabili: la UI nasconde il bottone se
/// !Importata, vedi GestioneContenutiView.axaml).
/// </summary>
public class MateriaListItem : ObservableObject, IConfermaEliminazione
{
    public string Id { get; }
    public string Nome { get; }
    public int ConteggioDomande { get; }
    public bool Importata { get; }
    public string Badge => Importata ? "Importata" : "Built-in";

    private bool _inAttesaConferma;
    public bool InAttesaConfermaEliminazione
    {
        get => _inAttesaConferma;
        set
        {
            if (SetField(ref _inAttesaConferma, value))
                RaisePropertyChanged(nameof(NonInAttesaConferma));
        }
    }

    public bool NonInAttesaConferma => !_inAttesaConferma;

    public MateriaListItem(Materia materia, int conteggioDomande)
    {
        Id = materia.Id;
        Nome = materia.Nome;
        ConteggioDomande = conteggioDomande;
        Importata = materia.Importata;
    }
}
```

- [ ] **Step 2: Creare lo XAML della view**

`wsa.quiz.app/Views/GestioneContenutiView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:state="using:Wsa.Quiz.App.State"
             xmlns:views="using:Wsa.Quiz.App.Views"
             mc:Ignorable="d"
             x:Class="Wsa.Quiz.App.Views.GestioneContenutiView"
             x:DataType="views:GestioneContenutiView">

    <ScrollViewer Margin="20,16,20,16" VerticalScrollBarVisibility="Auto">
        <StackPanel Spacing="16">

            <StackPanel Spacing="2">
                <TextBlock Text="Gestione contenuti" FontSize="22" FontWeight="SemiBold"/>
                <TextBlock Text="{Binding Sottotitolo}"
                           FontSize="12"
                           Foreground="{DynamicResource SystemBaseMediumColor}"/>
            </StackPanel>

            <ItemsControl ItemsSource="{Binding Sessioni}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate DataType="state:MateriaListItem">
                        <Border Background="{DynamicResource SystemRegionBrush}"
                                BorderBrush="{DynamicResource SystemBaseLowColor}"
                                BorderThickness="1"
                                CornerRadius="6"
                                Padding="14,10"
                                Margin="0,0,0,8">
                            <Grid ColumnDefinitions="*,Auto">
                                <StackPanel Grid.Column="0" Spacing="2">
                                    <StackPanel Orientation="Horizontal" Spacing="8">
                                        <TextBlock Text="{Binding Nome}" FontWeight="SemiBold" FontSize="14"/>
                                        <Border Background="{DynamicResource SystemBaseLowColor}"
                                                CornerRadius="3" Padding="6,2">
                                            <TextBlock Text="{Binding Badge}" FontSize="10"/>
                                        </Border>
                                    </StackPanel>
                                    <TextBlock Text="{Binding ConteggioDomande, StringFormat='{}{0} domande'}"
                                               FontSize="12"
                                               Foreground="{DynamicResource SystemBaseMediumColor}"/>
                                </StackPanel>

                                <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="6"
                                            VerticalAlignment="Center"
                                            IsVisible="{Binding Importata}">
                                    <Button Content="Elimina" Click="OnEliminaClick"
                                            IsTabStop="False" Classes="danger"
                                            Padding="{DynamicResource SzBtnPadding}" FontSize="12"
                                            IsVisible="{Binding NonInAttesaConferma}"/>
                                    <TextBlock Text="Sicuro?" VerticalAlignment="Center" FontSize="12"
                                               Foreground="#B85450" FontWeight="SemiBold"
                                               IsVisible="{Binding InAttesaConfermaEliminazione}"/>
                                    <Button Content="Si', elimina" Click="OnConfermaEliminaClick"
                                            IsTabStop="False" Classes="danger"
                                            Padding="{DynamicResource SzBtnPadding}" FontSize="12"
                                            IsVisible="{Binding InAttesaConfermaEliminazione}"/>
                                    <Button Content="Annulla" Click="OnAnnullaEliminaClick"
                                            IsTabStop="False"
                                            Padding="{DynamicResource SzBtnPadding}" FontSize="12"
                                            IsVisible="{Binding InAttesaConfermaEliminazione}"/>
                                </StackPanel>
                            </Grid>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>

        </StackPanel>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 3: Creare il code-behind**

`wsa.quiz.app/Views/GestioneContenutiView.axaml.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Wsa.Quiz.App.State;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Tab "Gestione contenuti": elenco materie (built-in + importate) con
/// rimozione delle importate (riuso di TabellaViewBase, stesso pattern di
/// SospesiView). I due flussi di import da JSON sono nei task successivi.
/// </summary>
public partial class GestioneContenutiView : TabellaViewBase<MateriaListItem>
{
    /// <summary>Sollevato dopo un import o una rimozione riusciti: la MainView ricarica tutti i dati.</summary>
    public event EventHandler? ContenutiCambiati;

    private ContenutiUtenteService? _contenutiUtente;
    private List<Materia> _materie = new();
    private List<Domanda> _tutteDomande = new();

    public GestioneContenutiView()
    {
        InitializeComponent();
    }

    public void Inizializza(StorageService storage, ContenutiUtenteService contenutiUtente,
        List<Materia> materie, List<Domanda> tutteDomande)
    {
        _contenutiUtente = contenutiUtente;
        _materie = materie;
        _tutteDomande = tutteDomande;
        base.Inizializza(storage); // TabellaViewBase.Inizializza: setta _storage e chiama Ricarica()
    }

    protected override void RicaricaCore()
    {
        var conteggi = _tutteDomande
            .GroupBy(d => d.MateriaId)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var m in _materie.OrderBy(m => m.Nome))
            Sessioni.Add(new MateriaListItem(m, conteggi.TryGetValue(m.Id, out var c) ? c : 0));
    }

    protected override string FormatSottotitolo(int count) =>
        count == 0 ? "Nessuna materia disponibile." : $"{count} materie disponibili.";

    protected override void EliminaDaStorage(MateriaListItem item)
    {
        if (!item.Importata || _contenutiUtente == null) return;
        _contenutiUtente.RimuoviMateria(item.Id);
        ContenutiCambiati?.Invoke(this, EventArgs.Empty);
    }
}
```

Nota sull'`Inizializza` a 4 argomenti: siccome dichiara un metodo con lo stesso nome di `TabellaViewBase<TItem>.Inizializza(StorageService)`, la risoluzione degli overload dentro `GestioneContenutiView` vede solo gli overload dichiarati in questa classe — per questo la chiamata usa esplicitamente `base.Inizializza(storage)` e non `Inizializza(storage)` (che fallirebbe la compilazione, wrong arg count).

- [ ] **Step 4: Build app**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 5: Commit**

```bash
git add wsa.quiz.app/State/MateriaListItem.cs wsa.quiz.app/Views/GestioneContenutiView.axaml wsa.quiz.app/Views/GestioneContenutiView.axaml.cs
git commit -m "feat(app): GestioneContenutiView, elenco materie con rimozione importate"
```

---

### Task 8: Wiring della 5ª sezione in `MainView`

**Files:**
- Modify: `wsa.quiz.app/MainView.axaml:62-99` (BottomNav), `:125-157` (Tabs/SwipePages)
- Modify: `wsa.quiz.app/MainView.axaml.cs` (campi, `IndiceSezione`, `ConfiguraNavigazione`, `DimensionaPagine`, `AggiornaNav`, `OnKeyDown`, `Caricamento`)

**Interfaces:**
- Consumes: `GestioneContenutiView` (Task 7).
- Produces: 5ª sezione raggiungibile da tab desktop, swipe/bottom-nav touch, `Ctrl+Tab`.

- [ ] **Step 1: XAML — BottomNav a 5 colonne**

In `wsa.quiz.app/MainView.axaml`, riga 72, da:
```xml
            <Grid ColumnDefinitions="*,*,*,*">
```
a:
```xml
            <Grid ColumnDefinitions="*,*,*,*,*">
```

Dopo il `Button x:Name="NavStatistiche"` (chiude a riga 96), aggiungere un 5° bottone (icona "+"):
```xml
                <Button x:Name="NavGestione" Grid.Column="4" Classes="nav" Tag="4" Click="OnNavClick">
                    <Path x:Name="NavGestioneIcon" Width="24" Height="24" Stretch="Uniform"
                          Stroke="White" StrokeThickness="2.4"
                          StrokeLineCap="Round"
                          Data="M12 4 V20 M4 12 H20"/>
                </Button>
```

- [ ] **Step 2: XAML — 5° TabItem e 5° pagina touch**

Dopo il `TabItem Header="Statistiche"` (righe 135-137), aggiungere:
```xml
                <TabItem Header="Gestione contenuti">
                    <ContentControl x:Name="GestioneArea"/>
                </TabItem>
```

Dentro `StackPanel x:Name="SwipePages"`, dopo `ContentControl x:Name="StatisticheAreaTouch"` (riga 155), aggiungere:
```xml
                    <ContentControl x:Name="GestioneAreaTouch"/>
```

- [ ] **Step 3: Code-behind — campi**

In `wsa.quiz.app/MainView.axaml.cs`, dopo `_statisticheView` (riga 32) aggiungere:
```csharp
    private GestioneContenutiView? _gestioneView;
```
Dopo `_statisticheArea` (riga 37) aggiungere:
```csharp
    private ContentControl _gestioneArea = null!;
```

- [ ] **Step 4: Code-behind — `IndiceSezione` e `ScorriAllaPagina` da 4 a 5 sezioni**

Riga 60, da:
```csharp
            return Math.Clamp((int)Math.Round(SwipeHost.Offset.X / w), 0, 3);
```
a:
```csharp
            return Math.Clamp((int)Math.Round(SwipeHost.Offset.X / w), 0, 4);
```

Riga 116, da:
```csharp
        double target = Math.Clamp(indice, 0, 3) * w;
```
a:
```csharp
        double target = Math.Clamp(indice, 0, 4) * w;
```

- [ ] **Step 5: Code-behind — `ConfiguraNavigazione`**

Nel ramo touch (dopo `_statisticheArea = StatisticheAreaTouch;`, riga 76), aggiungere:
```csharp
            _gestioneArea = GestioneAreaTouch;
```
Nel ramo desktop (dopo `_statisticheArea = StatisticheArea;`, riga 94), aggiungere:
```csharp
            _gestioneArea = GestioneArea;
```

- [ ] **Step 6: Code-behind — `DimensionaPagine`**

Riga 105, da:
```csharp
        foreach (var pagina in new[] { HomeAreaTouch, CronologiaAreaTouch, SospesiAreaTouch, StatisticheAreaTouch })
```
a:
```csharp
        foreach (var pagina in new[] { HomeAreaTouch, CronologiaAreaTouch, SospesiAreaTouch, StatisticheAreaTouch, GestioneAreaTouch })
```

- [ ] **Step 7: Code-behind — `AggiornaNav`**

Riga 149, da:
```csharp
        var icone = new[] { NavHomeIcon, NavCronologiaIcon, NavSospesiIcon, NavStatisticheIcon };
```
a:
```csharp
        var icone = new[] { NavHomeIcon, NavCronologiaIcon, NavSospesiIcon, NavStatisticheIcon, NavGestioneIcon };
```

- [ ] **Step 8: Code-behind — `OnKeyDown` (Ctrl+Tab)**

Riga 190, da:
```csharp
            const int n = 4;
```
a:
```csharp
            const int n = 5;
```

Nello `switch (IndiceSezione)` (righe 193-199), dopo `case 3: (_statisticheArea.Content as Control)?.Focus(); break;`, aggiungere:
```csharp
                case 4: (_gestioneArea.Content as Control)?.Focus(); break;
```

- [ ] **Step 9: Code-behind — istanziare la view in `Caricamento()`**

Dopo il blocco che istanzia `_statisticheView` (righe 248-250), aggiungere:
```csharp
            _gestioneView = new GestioneContenutiView();
            _gestioneView.Inizializza(_storage, _contenutiUtente!, _materie, _tutteDomande);
            _gestioneView.ContenutiCambiati += (_, _) => Caricamento();
            _gestioneArea.Content = _gestioneView;
```

(`_contenutiUtente` è già assegnato poche righe sopra dal Task 6, prima di `_materie`/`_tutteDomande`: non è mai null a questo punto del metodo.)

- [ ] **Step 10: Build app**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 11: Build + run desktop, verifica manuale**

Run: `dotnet run --project wsa.quiz.desktop`
Verificare: 5ª tab "Gestione contenuti" in alto, mostra l'elenco di tutte le materie built-in (badge "Built-in", nessun bottone Elimina). `Ctrl+Tab` cicla anche su di essa.

- [ ] **Step 12: Build APK + install + verifica device**

```bash
$env:ANDROID_HOME = "C:\Users\<utente>\AppData\Local\Android\Sdk"
dotnet build wsa.quiz.android -c Release
adb install -r wsa.quiz.android\bin\Release\net10.0-android\it.eduits.wsaquiz-Signed.apk
```
Verificare: 5 icone in bottom-nav (l'ultima "+"), swipe/tap raggiungono "Gestione contenuti", elenco materie built-in visibile, layout coerente con le altre schermate touch.

- [ ] **Step 13: Commit**

```bash
git add wsa.quiz.app/MainView.axaml wsa.quiz.app/MainView.axaml.cs
git commit -m "feat(app): 5a sezione Gestione contenuti in navigazione desktop/touch"
```

---

### Task 9: Flusso "Nuova materia"

**Files:**
- Modify: `wsa.quiz.app/Views/GestioneContenutiView.axaml` (form + pannello anteprima)
- Modify: `wsa.quiz.app/Views/GestioneContenutiView.axaml.cs` (handler, file picker, stato pendente)

**Interfaces:**
- Consumes: `ValidatoreImportDomande.Valida` (Task 3), `ContenutiUtenteService.CreaMateria` (Task 4).
- Produces: flusso completo file→validazione→anteprima→conferma per una nuova materia.

- [ ] **Step 1: XAML — sezione "Nuova materia" e pannello anteprima**

In `GestioneContenutiView.axaml`, dentro lo `StackPanel` principale, dopo l'`ItemsControl` dell'elenco materie, aggiungere:

```xml
            <Border Background="{DynamicResource SystemRegionBrush}"
                    BorderBrush="{DynamicResource SystemBaseLowColor}"
                    BorderThickness="1" CornerRadius="6" Padding="16">
                <StackPanel Spacing="10">
                    <TextBlock Text="Nuova materia" FontWeight="SemiBold" FontSize="15"/>
                    <TextBox x:Name="NomeNuovaMateria" Watermark="Nome della materia"/>
                    <Button Content="Scegli file JSON e importa" Click="OnScegliFileNuovaMateriaClick"
                            Classes="accent" Padding="{DynamicResource SzBtnPadding}"/>
                    <TextBlock x:Name="EsitoNuovaMateria" FontSize="12" TextWrapping="Wrap"
                               Foreground="{DynamicResource SystemBaseMediumColor}"/>
                </StackPanel>
            </Border>

            <Border x:Name="PannelloAnteprima" IsVisible="False"
                    Background="{DynamicResource SystemRegionBrush}"
                    BorderBrush="{DynamicResource SystemBaseLowColor}"
                    BorderThickness="1" CornerRadius="6" Padding="16">
                <StackPanel Spacing="10">
                    <TextBlock x:Name="AnteprimaTitoloText" FontWeight="SemiBold" FontSize="15" TextWrapping="Wrap"/>
                    <ScrollViewer MaxHeight="240" VerticalScrollBarVisibility="Auto">
                        <ItemsControl x:Name="AnteprimaLista"/>
                    </ScrollViewer>
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <Button x:Name="BottoneConferma" Content="Conferma" Click="OnConfermaImportClick"
                                Classes="accent" Padding="{DynamicResource SzBtnPadding}"/>
                        <Button Content="Annulla" Click="OnAnnullaImportClick"
                                Padding="{DynamicResource SzBtnPadding}"/>
                    </StackPanel>
                </StackPanel>
            </Border>
```

- [ ] **Step 2: Code-behind — stato pendente e helper condivisi**

In `GestioneContenutiView.axaml.cs`, aggiungere in cima i `using` mancanti:
```csharp
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
```

Aggiungere i campi (dopo `_tutteDomande`):
```csharp
    private string? _pendingNuovaMateriaNome;
    private string? _pendingMateriaEsistenteId;
    private string? _pendingMateriaEsistenteNome;
    private List<Domanda>? _pendingDomande;
```

Aggiungere gli helper condivisi (usati anche dal Task 10):
```csharp
    private async Task<string?> ScegliFileJsonAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } provider) return null;

        var risultati = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Scegli il file JSON delle domande",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } }
        });
        if (risultati.Count == 0) return null;

        await using var stream = await risultati[0].OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private void MostraErrori(List<string> errori)
    {
        PannelloAnteprima.IsVisible = true;
        AnteprimaTitoloText.Text = $"File non valido — {errori.Count} errori";
        AnteprimaLista.ItemsSource = errori;
        BottoneConferma.IsVisible = false;
        _pendingDomande = null;
    }

    private void MostraAnteprima(string titolo, List<Domanda> domande)
    {
        PannelloAnteprima.IsVisible = true;
        AnteprimaTitoloText.Text = titolo;
        AnteprimaLista.ItemsSource = domande
            .Select(d => $"[{d.Categoria}] {(d.DomandaTesto.Length <= 70 ? d.DomandaTesto : d.DomandaTesto[..70] + "…")}")
            .ToList();
        BottoneConferma.IsVisible = true;
    }

    private void OnAnnullaImportClick(object? sender, RoutedEventArgs e)
    {
        PannelloAnteprima.IsVisible = false;
        _pendingDomande = null;
        _pendingNuovaMateriaNome = null;
        _pendingMateriaEsistenteId = null;
    }
```

- [ ] **Step 3: Code-behind — handler "Nuova materia" e conferma**

```csharp
    private async void OnScegliFileNuovaMateriaClick(object? sender, RoutedEventArgs e)
    {
        string nome = NomeNuovaMateria.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(nome))
        {
            EsitoNuovaMateria.Text = "Inserisci un nome per la materia prima di scegliere il file.";
            return;
        }

        string? json = await ScegliFileJsonAsync();
        if (json == null) return;

        var (domande, errori) = ValidatoreImportDomande.Valida(json);
        if (errori.Count > 0)
        {
            MostraErrori(errori);
            return;
        }

        _pendingNuovaMateriaNome = nome;
        _pendingMateriaEsistenteId = null;
        _pendingDomande = domande;
        MostraAnteprima($"Nuova materia \"{nome}\" — {domande.Count} domande", domande);
    }

    private void OnConfermaImportClick(object? sender, RoutedEventArgs e)
    {
        if (_pendingDomande == null || _contenutiUtente == null) return;

        if (_pendingNuovaMateriaNome != null)
        {
            _contenutiUtente.CreaMateria(_pendingNuovaMateriaNome, _pendingDomande);
            EsitoNuovaMateria.Text = $"Materia \"{_pendingNuovaMateriaNome}\" creata con {_pendingDomande.Count} domande.";
            NomeNuovaMateria.Text = "";
        }

        PannelloAnteprima.IsVisible = false;
        _pendingDomande = null;
        _pendingNuovaMateriaNome = null;
        _pendingMateriaEsistenteId = null;
        ContenutiCambiati?.Invoke(this, EventArgs.Empty);
    }
```

(`OnConfermaImportClick` gestisce già anche il ramo "materia esistente": completato nel Task 10, che aggiunge l'`else if` — qui il ramo esiste già come no-op se `_pendingMateriaEsistenteId` è valorizzato, perché in questo task quel campo non viene mai impostato.)

- [ ] **Step 4: Build app**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 5: Verifica manuale desktop end-to-end**

1. Preparare un file `prova.json` con 2 domande valide (schema standard).
2. `dotnet run --project wsa.quiz.desktop` → tab "Gestione contenuti" → campo nome "Prova E2E" → "Scegli file JSON e importa" → selezionare `prova.json`.
3. Verificare: pannello anteprima mostra 2 righe + "Conferma".
4. Click "Conferma": la materia "Prova E2E" appare nell'elenco (badge "Importata", bottone Elimina) e nel selettore materie della Home; avviare un quiz su quella materia e verificare che le domande siano quelle importate.
5. Ripetere con un file JSON con un errore (es. `rispostaCorretta: 9`): verificare che il pannello mostri l'errore e NON compaia "Conferma".

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.app/Views/GestioneContenutiView.axaml wsa.quiz.app/Views/GestioneContenutiView.axaml.cs
git commit -m "feat(app): flusso nuova materia da JSON in Gestione contenuti"
```

---

### Task 10: Flusso "Aggiungi a materia esistente"

**Files:**
- Modify: `wsa.quiz.app/Views/GestioneContenutiView.axaml` (form selezione materia)
- Modify: `wsa.quiz.app/Views/GestioneContenutiView.axaml.cs` (handler, completamento `OnConfermaImportClick`)

**Interfaces:**
- Consumes: `ContenutiUtenteService.AggiungiDomande` (Task 4).
- Produces: secondo flusso completo (selezione materia → file → validazione → anteprima → conferma con riepilogo duplicati).

- [ ] **Step 1: XAML — sezione "Aggiungi a materia esistente"**

In `GestioneContenutiView.axaml`, tra la sezione "Nuova materia" e `PannelloAnteprima`, aggiungere:

```xml
            <Border Background="{DynamicResource SystemRegionBrush}"
                    BorderBrush="{DynamicResource SystemBaseLowColor}"
                    BorderThickness="1" CornerRadius="6" Padding="16">
                <StackPanel Spacing="10">
                    <TextBlock Text="Aggiungi domande a una materia esistente" FontWeight="SemiBold" FontSize="15"/>
                    <ComboBox x:Name="SelettoreMateria" ItemsSource="{Binding Sessioni}"
                              PlaceholderText="Scegli materia">
                        <ComboBox.ItemTemplate>
                            <DataTemplate DataType="state:MateriaListItem">
                                <TextBlock Text="{Binding Nome}"/>
                            </DataTemplate>
                        </ComboBox.ItemTemplate>
                    </ComboBox>
                    <Button Content="Scegli file JSON e importa" Click="OnScegliFileAggiungiClick"
                            Classes="accent" Padding="{DynamicResource SzBtnPadding}"/>
                    <TextBlock x:Name="EsitoAggiungi" FontSize="12" TextWrapping="Wrap"
                               Foreground="{DynamicResource SystemBaseMediumColor}"/>
                </StackPanel>
            </Border>
```

- [ ] **Step 2: Code-behind — handler "Aggiungi a materia esistente"**

```csharp
    private async void OnScegliFileAggiungiClick(object? sender, RoutedEventArgs e)
    {
        if (SelettoreMateria.SelectedItem is not MateriaListItem materia)
        {
            EsitoAggiungi.Text = "Scegli prima la materia a cui aggiungere le domande.";
            return;
        }

        string? json = await ScegliFileJsonAsync();
        if (json == null) return;

        var (domande, errori) = ValidatoreImportDomande.Valida(json);
        if (errori.Count > 0)
        {
            MostraErrori(errori);
            return;
        }

        _pendingNuovaMateriaNome = null;
        _pendingMateriaEsistenteId = materia.Id;
        _pendingMateriaEsistenteNome = materia.Nome;
        _pendingDomande = domande;
        MostraAnteprima($"Aggiungi a \"{materia.Nome}\" — {domande.Count} domande nel file", domande);
    }
```

- [ ] **Step 3: Code-behind — completare `OnConfermaImportClick`**

Sostituire il corpo di `OnConfermaImportClick` (Task 9, Step 3) con la versione completa:

```csharp
    private void OnConfermaImportClick(object? sender, RoutedEventArgs e)
    {
        if (_pendingDomande == null || _contenutiUtente == null) return;

        if (_pendingNuovaMateriaNome != null)
        {
            _contenutiUtente.CreaMateria(_pendingNuovaMateriaNome, _pendingDomande);
            EsitoNuovaMateria.Text = $"Materia \"{_pendingNuovaMateriaNome}\" creata con {_pendingDomande.Count} domande.";
            NomeNuovaMateria.Text = "";
        }
        else if (_pendingMateriaEsistenteId != null)
        {
            var esistenti = _tutteDomande.Where(d => d.MateriaId == _pendingMateriaEsistenteId);
            var (aggiunte, duplicati) = _contenutiUtente.AggiungiDomande(_pendingMateriaEsistenteId, _pendingDomande, esistenti);
            EsitoAggiungi.Text = $"{aggiunte} domande aggiunte a \"{_pendingMateriaEsistenteNome}\", {duplicati} duplicate saltate.";
        }

        PannelloAnteprima.IsVisible = false;
        _pendingDomande = null;
        _pendingNuovaMateriaNome = null;
        _pendingMateriaEsistenteId = null;
        ContenutiCambiati?.Invoke(this, EventArgs.Empty);
    }
```

- [ ] **Step 4: Build app**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 5: Verifica manuale desktop end-to-end**

1. Preparare `prova2.json` con 1 domanda identica (stesso testo) a una già presente in una materia built-in + 1 domanda nuova.
2. Tab "Gestione contenuti" → selezionare la materia dal ComboBox → "Scegli file JSON e importa" → `prova2.json`.
3. Verificare anteprima (2 domande nel file), Conferma.
4. Verificare messaggio: "1 domande aggiunte a ..., 1 duplicate saltate."
5. Verificare che la nuova domanda compaia effettivamente facendo un quiz su quella materia.

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.app/Views/GestioneContenutiView.axaml wsa.quiz.app/Views/GestioneContenutiView.axaml.cs
git commit -m "feat(app): flusso aggiungi domande a materia esistente"
```

---

### Task 11: Riquadro guida + download guida/motore

**Files:**
- Modify: `wsa.quiz.app/Views/GestioneContenutiView.axaml` (riquadro guida)
- Modify: `wsa.quiz.app/Views/GestioneContenutiView.axaml.cs` (download handler)

**Interfaces:**
- Consumes: risorse `avares://Wsa.Quiz.App/Assets/Guida/*` (Task 5).

- [ ] **Step 1: XAML — riquadro guida**

In `GestioneContenutiView.axaml`, dopo la sezione "Aggiungi a materia esistente" (prima di `PannelloAnteprima`), aggiungere:

```xml
            <Border Background="{DynamicResource SystemRegionBrush}"
                    BorderBrush="{DynamicResource SystemBaseLowColor}"
                    BorderThickness="1" CornerRadius="6" Padding="16">
                <StackPanel Spacing="10">
                    <TextBlock Text="Generare domande bilanciate con un'IA" FontWeight="SemiBold" FontSize="15"/>
                    <TextBlock TextWrapping="Wrap" FontSize="12"
                               Foreground="{DynamicResource SystemBaseMediumColor}"
                               Text="Il motore 'quiz-engine.js' controlla che le domande generate non abbiano il bias 'la risposta piu' lunga e' quella giusta': verifica la distribuzione delle lunghezze e mescola posizioni e ordine. La guida spiega come usarlo insieme a un'IA per scrivere le domande; il JSON prodotto si importa qui sopra."/>
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <Button Content="Scarica guida" Click="OnScaricaGuidaClick" Padding="{DynamicResource SzBtnPadding}"/>
                        <Button Content="Scarica motore" Click="OnScaricaMotoreClick" Padding="{DynamicResource SzBtnPadding}"/>
                    </StackPanel>
                </StackPanel>
            </Border>
```

- [ ] **Step 2: Code-behind — download**

Aggiungere `using Avalonia.Platform;` in cima al file (per `AssetLoader`). Aggiungere gli handler:

```csharp
    private async void OnScaricaGuidaClick(object? sender, RoutedEventArgs e) =>
        await ScaricaFile("avares://Wsa.Quiz.App/Assets/Guida/GUIDA_GENERAZIONE_DOMANDE.md", "GUIDA_GENERAZIONE_DOMANDE.md");

    private async void OnScaricaMotoreClick(object? sender, RoutedEventArgs e) =>
        await ScaricaFile("avares://Wsa.Quiz.App/Assets/Guida/quiz-engine.js", "quiz-engine.js");

    private async Task ScaricaFile(string uriAsset, string nomeSuggerito)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } provider) return;

        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = nomeSuggerito });
        if (file == null) return;

        using var origine = AssetLoader.Open(new Uri(uriAsset));
        await using var destinazione = await file.OpenWriteAsync();
        await origine.CopyToAsync(destinazione);
    }
```

- [ ] **Step 3: Build app**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 4: Verifica manuale desktop**

`dotnet run --project wsa.quiz.desktop` → tab "Gestione contenuti" → "Scarica guida" → salvare in una cartella temporanea → aprire il file salvato e verificare che il contenuto coincida con `generazione-domande/GUIDA_GENERAZIONE_DOMANDE.md`. Ripetere per "Scarica motore".

- [ ] **Step 5: Commit**

```bash
git add wsa.quiz.app/Views/GestioneContenutiView.axaml wsa.quiz.app/Views/GestioneContenutiView.axaml.cs
git commit -m "feat(app): download guida e motore generazione domande dalla app"
```

---

### Task 12: Aggiornamento README

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Riscrivere la sezione "Aggiungere materie, categorie e domande"**

Sostituire l'intera sezione (righe 28-85 del README attuale, da `## Aggiungere materie, categorie e domande` fino a prima di `## Generare nuove domande con un'IA`) con:

```markdown
## Aggiungere materie, categorie e domande

Due modi, complementari:

### 1. Dall'app — "Gestione contenuti" (consigliato)

Nella GUI (desktop o Android), la 5ª tab **"Gestione contenuti"** permette di:

- **Creare una nuova materia**: nome + upload di un file `.json` con le sue domande (schema sotto). Validato prima dell'import: se anche una sola domanda non è conforme, l'intero file viene rifiutato e gli errori sono elencati.
- **Aggiungere domande a una materia esistente** (built-in o già importata): stesso upload, con anteprima prima di confermare. Le domande già presenti (stesso testo) vengono saltate automaticamente.
- **Rimuovere** una materia importata (le materie built-in non sono rimovibili da qui).

I contenuti importati restano **locali al dispositivo** (cartella utente, vedi sotto): non modificano i file del repo. Per farli entrare nel repo/APK ufficiale vanno copiati a mano nella cartella `domande/` (metodo 2).

### 2. A mano, editando i JSON nella root del repo

I dati si modificano via file JSON nella root del progetto. A ogni build i file vengono copiati negli output `bin/` (desktop/CLI) o incorporati nell'APK (Android); basta rilanciare l'eseguibile per vedere le modifiche.

#### Materie — `materie.json`

Elenco di oggetti, uno per materia. La `cartella` è relativa alla root e contiene tutti i `.json` con le domande di quella materia.

```json
[
  {
    "id": "cs",
    "nome": "C#",
    "cartella": "domande/cs",
    "formato": "standard"
  }
]
```

| Campo      | Tipo   | Note                                                                 |
|------------|--------|------------------------------------------------------------------------|
| `id`       | string | Identificatore breve, usato internamente. Univoco.                   |
| `nome`     | string | Etichetta mostrata in Home e nella cronologia.                       |
| `cartella` | string | Path relativo alla root. La cartella deve esistere.                  |
| `formato`  | string | Per ora sempre `"standard"`. Riservato per formati alternativi.      |

#### Domande — `domande/<materia>/<qualsiasi>.json`

Ogni file è un array di domande. Si possono avere più file per materia (es. `csharp_domande.json`, `csharp_domande_2.json`): il loader li unisce tutti. La **categoria** è un campo libero su ciascuna domanda — non va dichiarata altrove. La Home offre i filtri per materia e categoria automaticamente.

```json
[
  {
    "id": 302,
    "categoria": "Basi del linguaggio",
    "domanda": "Quale tipo di dato si usa in C# per rappresentare numeri interi?",
    "risposte": [
      "int",
      "char",
      "double",
      "float"
    ],
    "rispostaCorretta": 0,
    "spiegazione": "Il tipo `int` rappresenta numeri interi in C# (es. 42, -10, 0). `double` è per numeri decimali, `float` è un decimale a precisione singola, `char` rappresenta un singolo carattere."
  }
]
```

| Campo              | Tipo     | Note                                                                                  |
|--------------------|----------|-----------------------------------------------------------------------------------------|
| `id`               | int      | Ignorato: l'app calcola un id stabile a partire dal contenuto della domanda.          |
| `categoria`        | string   | Etichetta libera. Stessa stringa = stessa categoria nei filtri.                       |
| `domanda`          | string   | Testo della domanda.                                                                  |
| `risposte`         | string[] | Esattamente **4** opzioni (l'import da app accetta anche solo 2, ma lo standard è 4). |
| `rispostaCorretta` | int      | Indice 0..3 della risposta giusta in `risposte`.                                      |
| `spiegazione`      | string   | Mostrata dopo aver risposto. Markdown leggero (apici, backtick, a-capo) consentito.   |

Internamente le 4 risposte vengono **rimescolate ad ogni partita** (in modo che A/B/C/D non siano sempre nello stesso ordine), e l'indice corretto viene ricalcolato di conseguenza.
```

- [ ] **Step 2: Riscrivere la sezione "Generare nuove domande con un'IA"**

Sostituire l'intera sezione attuale (da `## Generare nuove domande con un'IA` fino a prima di `## Architettura`) con:

```markdown
## Generare nuove domande con un'IA

Le risposte multiple sono soggette a due bias tipici quando vengono generate da modelli di linguaggio:

1. **Bias della risposta più lunga**: l'IA tende a produrre la risposta corretta più dettagliata e i distrattori più brevi. Chi conosce il trucco indovina senza sapere la materia.
2. **Bias di posizione**: la risposta corretta finisce statisticamente più spesso in alcuni indici (tipicamente A o l'ultimo).

Il repo include una **guida** (`generazione-domande/GUIDA_GENERAZIONE_DOMANDE.md`) e un **motore** (`generazione-domande/quiz-engine.js`) pensati apposta per evitarli:

- La guida è un prompt riutilizzabile da dare a un'IA (o da seguire a mano): spiega come scrivere le domande, come bilanciare le lunghezze delle risposte in 4 categorie (più lunga / più corta / intermedia / simile, ~25% ciascuna) e gli errori tipici in cui un'IA cade sistematicamente.
- Il motore (`node`) prende il file dati scritto seguendo la guida, **valida** (campi mancanti, indici fuori range, duplicati), **misura** la distribuzione delle lunghezze e, solo se ok, **scrive** il JSON finale nello schema sopra, mescolando posizione delle risposte e ordine delle domande.

Uso da riga di comando:

```bash
# DRY RUN — controlla le quote senza scrivere
node "generazione-domande/quiz-engine.js" miamateria.data.js out.json

# Genera il JSON definitivo
node "generazione-domande/quiz-engine.js" miamateria.data.js miamateria_domande.json --write
```

Entrambi i file sono anche **scaricabili direttamente dall'app**, dalla tab "Gestione contenuti" (vedi sopra) — utile su Android, dove il repo non è raggiungibile. Il JSON generato si importa poi dalla stessa schermata (nuova materia, o aggiunta a una esistente).
```

- [ ] **Step 3: Aggiornare la panoramica in cima al file**

Sostituire il paragrafo introduttivo (righe 1-8 del README attuale) con:

```markdown
# WSA Quiz

App di quiz multi-materia pensata per ripassare il programma del corso **Web & Software Architect (WSA)**. Tre eseguibili condividono lo stesso Core, gli stessi dati e la stessa cronologia:

- **`wsa.quiz.cli`** — versione console. Pratica per ripassare senza distrazioni o da terminale.
- **`wsa.quiz.desktop`** — versione grafica desktop (Avalonia 12).
- **`wsa.quiz.android`** — stessa GUI, impacchettata come APK Android (layout touch-friendly, bottom navigation a icone con swipe, safe-area edge-to-edge).

La GUI (desktop e Android) ha 5 sezioni: Home (configurazione + Avvia), Cronologia (sessioni passate con drill-down), Sospesi (sessioni in pausa, riprendibili), Statistiche (heatmap per materia/categoria), Gestione contenuti (import di materie/domande da JSON, guida per generarle con un'IA).

Una sessione di quiz può essere **classica** (ogni domanda chiesta una volta) o in **modalità rotazione** (le sbagliate tornano in coda finché non vengono indovinate). Si possono filtrare le domande per materia e categoria, limitare a N domande, randomizzare l'ordine, attivare/disattivare il cronometro. Si può mettere in pausa e riprendere dopo. La cronologia mostra punteggio, durata, % corrette e — al doppio click — il dettaglio domanda per domanda.
```

- [ ] **Step 4: Aggiornare la sezione "Architettura"**

Sostituire (attuale, verso la fine del file):

```markdown
Tre progetti .NET 8 nella stessa solution:

```
Wsa.Quiz.sln
├── wsa.quiz.core/      Libreria. Modelli (Domanda, Materia, RisultatoQuiz, ...) e servizi
│                        (QuizService per shuffle/punteggio, StorageService per cronologia/sospesi).
├── wsa.quiz.cli/       Console. Tutta l'interazione utente in Program.cs e Services/ConsoleUI.cs.
└── wsa.quiz.app/       GUI Avalonia. State machine event-driven in State/SessioneQuiz.cs,
                         viste in Views/.
```
```

con:

```markdown
Cinque progetti .NET 10 nella stessa solution:

```
Wsa.Quiz.sln
├── wsa.quiz.core/      Libreria. Modelli (Domanda, Materia, RisultatoQuiz, ...) e servizi
│                        (QuizService per shuffle/punteggio, StorageService per cronologia/sospesi,
│                        ContenutiUtenteService per l'import da "Gestione contenuti").
├── wsa.quiz.cli/       Console. Tutta l'interazione utente in Program.cs e Services/ConsoleUI.cs.
├── wsa.quiz.app/       Libreria UI condivisa Avalonia. State machine event-driven in
│                        State/SessioneQuiz.cs, viste in Views/. Usata sia da Desktop che Android.
├── wsa.quiz.desktop/   Head desktop (Window, ApplicationLifetime classico).
└── wsa.quiz.android/   Head Android (single-view, APK).
```
```

- [ ] **Step 5: Verifica manuale**

Aprire `README.md` renderizzato (es. anteprima Markdown dell'editor) e controllare che i blocchi di codice siano ben formattati e i link interni (se presenti) coerenti.

- [ ] **Step 6: Commit**

```bash
git add README.md
git commit -m "docs: aggiornare README con Gestione contenuti, guida/motore, port Android"
```

---

### Task 13: Verifica finale end-to-end

**Files:** nessuna modifica — solo verifica.

- [ ] **Step 1: Build completo**

```bash
dotnet build Wsa.Quiz.sln -c Debug
```
Expected: `Errori: 0` su tutti i progetti (Core, Cli, App, Desktop; Android si builda solo in Release, vedi Step 3).

- [ ] **Step 2: Verifica desktop completa**

`dotnet run --project wsa.quiz.desktop`. Percorrere l'intero elenco "Verifica" della spec (`docs/superpowers/specs/2026-07-02-import-domande-json-design.md`):

1. Nuova materia con JSON valido → compare in elenco, badge "Importata", quizzabile da Home.
2. Aggiunta domande a materia built-in esistente → domande unite, duplicati saltati e conteggiati.
3. JSON con errori strutturali → import rifiutato, errori elencati, nessun file scritto (verificare `%APPDATA%\WsaQuiz\ContenutiUtente\` invariato).
4. Rimozione materia importata → sparisce da elenco e dai filtri Home; built-in intatte.
5. Riavvio dell'app → i contenuti importati (non rimossi) sono ancora presenti.
6. "Scarica guida"/"Scarica motore" → file salvati e leggibili.
7. Le altre 4 tab (Home/Cronologia/Sospesi/Statistiche) restano invariate: nessuna regressione.

- [ ] **Step 3: Build APK + install + verifica device**

```bash
$env:ANDROID_HOME = "C:\Users\<utente>\AppData\Local\Android\Sdk"
dotnet build wsa.quiz.android -c Release
adb install -r wsa.quiz.android\bin\Release\net10.0-android\it.eduits.wsaquiz-Signed.apk
```

Ripetere sul device fisico (o emulatore, ora disponibile da PC locale) i punti 1-4 e 6 dello Step 2, più:

8. Swipe e bottom-nav raggiungono correttamente le 5 sezioni, safe-area invariata (nessuna regressione sulle Trappole 17-18).
9. Il file picker Android (Storage Access Framework, mai usato prima nel repo) apre e legge correttamente un JSON dalla memoria del telefono.

- [ ] **Step 4: Aggiornare CLAUDE.md**

Segnare lo Step 12 della roadmap (`CLAUDE.md`, sezione "Import domande/materie da GUI") come completato, con una sintesi di 3-5 righe (schermata "Gestione contenuti", storage locale in `ContenutiUtente/`, i due flussi, guida/motore bundlati) — stesso stile delle sezioni "✅ Port Android — M1/M2" già presenti.

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: Step 12 (import contenuti da GUI) completato"
```

---

## Self-Review

- **Spec coverage**: storage/merge (Task 4, 6), 5ª tab (Task 8), nuova materia (Task 9), aggiungi a esistente (Task 10), validazione strutturale/rifiuta-tutto (Task 3), duplicati-salta (Task 4/10), anteprima (Task 9/10), rimozione (Task 7), guida+motore bundlati e scaricabili (Task 5/11), README (Task 12). Tutti i punti della sezione "Decisioni" della spec sono coperti.
- **Placeholder scan**: nessun TODO/TBD; ogni step ha codice completo o comando esatto.
- **Type consistency**: `ContenutiUtenteService` (Task 4) esposto identico in Task 6/7/8/9/10; `MateriaListItem` (Task 7) usato identico in Task 7/10 (XAML `state:MateriaListItem` + code-behind); `ValidatoreImportDomande.Valida` firma `(List<Domanda>, List<string>)` usata identica in Task 9/10; `GestioneContenutiView.Inizializza(StorageService, ContenutiUtenteService, List<Materia>, List<Domanda>)` dichiarata in Task 7, chiamata identica in Task 8 Step 9; evento `ContenutiCambiati` dichiarato Task 7, sottoscritto Task 8 Step 9, sollevato Task 7 (`EliminaDaStorage`) e Task 9/10 (`OnConfermaImportClick`).
