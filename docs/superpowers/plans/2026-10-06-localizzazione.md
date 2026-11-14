# Localizzazione Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Quaestio disponibile in italiano e inglese (estendibile ad altre lingue), con il formato dei file di traduzione delle domande collaudato su una materia (Cybersecurity).

**Architecture:** Stringhe UI in `Strings.resx`/`Strings.en.resx` dentro `Wsa.Quiz.Core` (classe tipizzata generata da MSBuild). Lingua = `CultureInfo` di processo, scelta da `preferenze.json` e applicata all'avvio; cambio a runtime ricreando la `MainView`. Traduzioni domande in `traduzioni/<lingua>/<materiaId>.json` applicate come overlay al caricamento; nomi di materie e categorie restano chiavi canoniche italiane e si traducono solo in visualizzazione tramite `EtichetteContenuti`.

**Tech Stack:** .NET 10, Avalonia 12.0.2, resx + MSBuild strongly-typed resources, harness headless (`Avalonia.Headless`) fuori repo.

**Spec:** `docs/superpowers/specs/2026-10-06-localizzazione-design.md`

## Global Constraints

- Lingua base (neutral) = italiano (`Strings.resx`); altre lingue = `Strings.<codice>.resx`. Lingue supportate ora: `it` "Italiano", `en` "English".
- Prima apertura: `PreferenzeUtente.Lingua == ""` → lingua di sistema se supportata, altrimenti `en`.
- La CLI resta in italiano (forza la cultura `it`).
- `Id`, `RispostaCorretta`, `Categoria`, `MateriaId`, `MateriaNome`, `Materia.Nome` NON vengono mai tradotti/modificati: sono chiavi. Si traduce solo in visualizzazione.
- Traduzioni domande in `traduzioni/<lingua>/<materiaId>.json`, **mai** dentro `domande/` (Android enumera ricorsivamente).
- Errori nelle traduzioni mai bloccanti: fallback all'italiano + avviso su `Console.Error`.
- Stringhe con parametri: segnaposto `{0}` nella risorsa + `string.Format`; mai concatenare frammenti tradotti.
- Cartella dati utente e identificatori interni (`WsaQuiz`, `Wsa.Quiz.*`) invariati.
- Code-behind + INPC, niente MVVM; `DataContext` assegnato solo nel costruttore (Trappola 3).
- Commit **e push** a fine di ogni task (preferenza di Luca).

## Ambiente (ogni comando bash)

Su Fedora nulla è nel PATH per le shell non interattive. Ogni comando che usa
`dotnet` va preceduto da:

```bash
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
```

Per l'head Android serve anche `export JAVA_HOME="$HOME/.local/jdk/jdk-17.0.20.1+1" ANDROID_HOME="$HOME/Android/Sdk"`.

`H` = cartella degli harness, **fuori dal repo**: la cartella scratchpad della
sessione + `/l10n` (es. `H=/tmp/claude-1000/<progetto>/<sessione>/scratchpad/l10n`).
Se in una sessione nuova la cartella non c'è, ricrearla dal Task 1 Step 1 e dal
Task 4 Step 1.

Gli harness che istanziano `MainView` vanno lanciati con
`XDG_CONFIG_HOME=$H/cfg` così `StorageService.CartellaUtenteDefault()` punta a
una cartella temporanea e **non tocca `~/.config/WsaQuiz`**.

Se una build sembra ignorare codice appena scritto (`CS0246` su un tipo che
esiste): insidia dell'orologio, usare `dotnet build --no-incremental`.

## Review Focus

1. **Cambio lingua con una partita in pausa nei Sospesi** → la ripresa trova le domande per `Id` e le mostra nella nuova lingua; le risposte già date restano nella lingua originale. Test: Task 2 (Id invariati fra `it` ed `en`).
2. **Statistiche e filtro cronologia dopo partite in due lingue** → una sola riga "Sicurezza Informatica" in statistiche, mostrata come "Cybersecurity" in inglese. Test: Task 2 (`MateriaNome` canonico dopo overlay) + Task 7 (filtro con chiave canonica).
3. **Primo avvio su sistema in inglese (il PC Fedora è `en_GB`)** → l'app parte in inglese senza che l'utente abbia scelto nulla; la scelta esplicita "Italiano" poi persiste. Test: Task 1 (`Risolvi("", en-GB) == "en"`) + Task 4 (riavvio con `Lingua="it"`).
4. **Toggle del tema dopo il primo avvio** → non deve congelare la lingua "segui il sistema" in `preferenze.json`. Test: Task 4.
5. **CSV esportato in inglese aperto in Excel inglese** → separatore `,`, decimale `.`; in italiano resta `;` e `,`. Test: Task 3.

---

### Task 1: Infrastruttura risorse + lingue + CLI

**Files:**
- Create: `wsa.quiz.core/Risorse/Strings.resx`, `wsa.quiz.core/Risorse/Strings.en.resx`, `wsa.quiz.core/Risorse/Lingue.cs`
- Modify: `wsa.quiz.core/Wsa.Quiz.Core.csproj`, `wsa.quiz.cli/Program.cs` (inizio di `Main`)
- Test: `$H/core/` (harness console fuori repo)

**Interfaces:**
- Produces:
  - `Wsa.Quiz.Core.Risorse.Strings` — classe pubblica generata, una proprietà `static string` per chiave.
  - `Wsa.Quiz.Core.Risorse.VoceLingua(string Codice, string Nome)` — record, `ToString() => Nome`.
  - `Lingue.Base` (`"it"`), `Lingue.Supportate` (`IReadOnlyList<VoceLingua>`), `Lingue.Risolvi(string? preferenza, CultureInfo sistema) : string`, `Lingue.Applica(string codice)`, `Lingue.Corrente : string`.
  - Harness `$H/core` con `Verifica.Check(string nome, bool ok)` e `Verifica.Fallite`.

- [ ] **Step 1: Crea l'harness Core**

```bash
mkdir -p $H/core && cd $H/core
cat > core.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup><ProjectReference Include="<repo>/wsa.quiz.core/Wsa.Quiz.Core.csproj" /></ItemGroup>
</Project>
EOF
cat > Verifica.cs <<'EOF'
public static class Verifica
{
    public static int Fallite;
    public static void Check(string nome, bool ok)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {nome}");
        if (!ok) Fallite++;
    }
}
EOF
cat > Program.cs <<'EOF'
TestLingue.Esegui();
Console.WriteLine(Verifica.Fallite == 0 ? "TUTTO OK" : $"{Verifica.Fallite} FALLITI");
return Verifica.Fallite == 0 ? 0 : 1;
EOF
```

- [ ] **Step 2: Scrivi il test**

`$H/core/TestLingue.cs`:

```csharp
using System.Globalization;
using System.Resources;
using System.Xml.Linq;
using Wsa.Quiz.Core.Risorse;

public static class TestLingue
{
    public static void Esegui()
    {
        Verifica.Check("Risolvi: preferenza valida vince", Lingue.Risolvi("it", new CultureInfo("en-GB")) == "it");
        Verifica.Check("Risolvi: vuoto + sistema en-GB -> en", Lingue.Risolvi("", new CultureInfo("en-GB")) == "en");
        Verifica.Check("Risolvi: vuoto + sistema it-IT -> it", Lingue.Risolvi("", new CultureInfo("it-IT")) == "it");
        Verifica.Check("Risolvi: vuoto + sistema de-DE -> en", Lingue.Risolvi("", new CultureInfo("de-DE")) == "en");
        Verifica.Check("Risolvi: preferenza sconosciuta -> sistema", Lingue.Risolvi("xx", new CultureInfo("it-IT")) == "it");

        Lingue.Applica("en");
        Verifica.Check("Applica en -> Corrente en", Lingue.Corrente == "en");
        Verifica.Check("Strings in inglese", Strings.Lingua_Nome == "Language");
        Lingue.Applica("it");
        Verifica.Check("Strings in italiano", Strings.Lingua_Nome == "Lingua");

        // Parita' delle chiavi: ogni chiave della base esiste, non vuota, in ogni lingua.
        string dir = "<repo>/wsa.quiz.core/Risorse";
        var baseKeys = Chiavi(Path.Combine(dir, "Strings.resx"));
        foreach (var l in Lingue.Supportate.Where(l => l.Codice != Lingue.Base))
        {
            var altre = Chiavi(Path.Combine(dir, $"Strings.{l.Codice}.resx"));
            var mancanti = baseKeys.Where(k => !altre.TryGetValue(k.Key, out var v) || string.IsNullOrWhiteSpace(v)).Select(k => k.Key).ToList();
            Verifica.Check($"Parita' chiavi {l.Codice} ({baseKeys.Count} chiavi){(mancanti.Count > 0 ? " mancano: " + string.Join(", ", mancanti) : "")}", mancanti.Count == 0);
            var extra = altre.Keys.Except(baseKeys.Keys).ToList();
            Verifica.Check($"Nessuna chiave orfana in {l.Codice}{(extra.Count > 0 ? ": " + string.Join(", ", extra) : "")}", extra.Count == 0);
        }
    }

    static Dictionary<string, string> Chiavi(string file)
        => XDocument.Load(file).Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => (string?)e.Element("value") ?? "");
}
```

- [ ] **Step 3: Esegui, deve fallire**

Run: `cd $H/core && dotnet run`
Expected: errore di compilazione, `Wsa.Quiz.Core.Risorse` non esiste.

- [ ] **Step 4: Aggiungi le risorse al csproj di Core**

In `wsa.quiz.core/Wsa.Quiz.Core.csproj`, dopo il `PropertyGroup`:

```xml
  <!-- Stringhe user-facing (localizzazione, 2026-10-06). La classe tipizzata
       Strings e' generata da MSBuild in obj/ a ogni build: niente Designer.cs
       nel repo, funziona con la dotnet CLI senza Visual Studio. -->
  <ItemGroup>
    <EmbeddedResource Update="Risorse\Strings.resx">
      <StronglyTypedLanguage>CSharp</StronglyTypedLanguage>
      <StronglyTypedClassName>Strings</StronglyTypedClassName>
      <StronglyTypedNamespace>Wsa.Quiz.Core.Risorse</StronglyTypedNamespace>
      <StronglyTypedFileName>$(IntermediateOutputPath)Strings.Designer.cs</StronglyTypedFileName>
      <PublicClass>true</PublicClass>
    </EmbeddedResource>
  </ItemGroup>
```

(Verificato con un prototipo il 2026-10-06: la generazione funziona con `dotnet run` e il satellite `en` viene risolto.)

- [ ] **Step 5: Crea i due resx**

`wsa.quiz.core/Risorse/Strings.resx` — intestazione standard resx più le prime chiavi (le chiavi `Lingua_*` sostituiscono i testi del segnaposto in Impostazioni):

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <data name="Lingua_Nome" xml:space="preserve"><value>Lingua</value></data>
  <data name="Lingua_Descrizione" xml:space="preserve"><value>Lingua dell'interfaccia. Le domande senza traduzione restano in italiano.</value></data>
  <data name="Lingua_BloccataDuranteQuiz" xml:space="preserve"><value>Termina o metti in pausa il quiz per cambiare lingua.</value></data>
</root>
```

`Strings.en.resx`: stessa intestazione, stesse chiavi:

```xml
  <data name="Lingua_Nome" xml:space="preserve"><value>Language</value></data>
  <data name="Lingua_Descrizione" xml:space="preserve"><value>Interface language. Questions without a translation stay in Italian.</value></data>
  <data name="Lingua_BloccataDuranteQuiz" xml:space="preserve"><value>Finish or pause the quiz to change language.</value></data>
```

**Convenzione per tutte le chiavi del piano:** `<Area>_<Descrizione>` in PascalCase (`Home_AvviaQuiz`, `Comune_Annulla`, `Cronologia_FiltroMateria`). Testi riusati in più view → prefisso `Comune_`. Coppie singolare/plurale → suffissi `_Uno` / `_Molti`. Caratteri XML (`&`, `<`) vanno escapati (`&amp;`).

- [ ] **Step 6: Crea `Lingue.cs`**

`wsa.quiz.core/Risorse/Lingue.cs`:

```csharp
using System.Globalization;

namespace Wsa.Quiz.Core.Risorse;

/// <summary>Una lingua selezionabile. ToString() = nome nativo, per i ComboBox.</summary>
public sealed record VoceLingua(string Codice, string Nome)
{
    public override string ToString() => Nome;
}

/// <summary>
/// Lingue dell'app. La lingua e' globale al processo: e' la CultureInfo corrente,
/// letta dalla classe Strings generata e dalla formattazione di date e numeri.
/// Aggiungere una lingua = un Strings.&lt;codice&gt;.resx + una riga in Supportate.
/// </summary>
public static class Lingue
{
    public const string Base = "it";

    public static readonly IReadOnlyList<VoceLingua> Supportate =
    [
        new("it", "Italiano"),
        new("en", "English"),
    ];

    /// <summary>Lingua da usare: preferenza se supportata, poi quella di sistema, poi inglese.</summary>
    public static string Risolvi(string? preferenza, CultureInfo sistema)
    {
        if (!string.IsNullOrEmpty(preferenza) && EsisteLingua(preferenza)) return preferenza;
        string sys = sistema.TwoLetterISOLanguageName;
        return EsisteLingua(sys) ? sys : "en";
    }

    /// <summary>Imposta la lingua per tutto il processo (thread corrente e futuri).</summary>
    public static void Applica(string codice)
    {
        var c = new CultureInfo(codice);
        CultureInfo.CurrentUICulture = c;
        CultureInfo.CurrentCulture = c;
        CultureInfo.DefaultThreadCurrentUICulture = c;
        CultureInfo.DefaultThreadCurrentCulture = c;
    }

    /// <summary>Codice della lingua attiva, ricondotto a una supportata (fallback: base).</summary>
    public static string Corrente
    {
        get
        {
            string c = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return EsisteLingua(c) ? c : Base;
        }
    }

    private static bool EsisteLingua(string codice) => Supportate.Any(l => l.Codice == codice);
}
```

- [ ] **Step 7: CLI in italiano**

In `wsa.quiz.cli/Program.cs`, prima riga di `Main` dopo l'`OutputEncoding`:

```csharp
        // La CLI resta in italiano anche su un sistema in un'altra lingua:
        // le stringhe di Core seguono la cultura corrente.
        Wsa.Quiz.Core.Risorse.Lingue.Applica("it");
```

- [ ] **Step 8: Esegui, deve passare**

Run: `cd $H/core && dotnet run`
Expected: tutte PASS, `TUTTO OK`.
Poi: `cd <repo> && dotnet build wsa.quiz.cli && dotnet build wsa.quiz.desktop` → verdi.

- [ ] **Step 9: Commit e push**

```bash
git add wsa.quiz.core/Risorse wsa.quiz.core/Wsa.Quiz.Core.csproj wsa.quiz.cli/Program.cs
git commit -m "feat(l10n): risorse Strings it/en in Core, Lingue, CLI forzata in italiano"
git push
```

---

### Task 2: Overlay traduzioni domande + EtichetteContenuti

**Files:**
- Create: `wsa.quiz.core/Models/TraduzioneMateria.cs`, `wsa.quiz.core/Services/EtichetteContenuti.cs`
- Modify: `wsa.quiz.core/Services/StorageService.cs` (`CaricaDomandeMateria`, ~righe 125-165; nuove proprietà vicino a `CartellaUtente`)
- Test: `$H/core/TestTraduzioni.cs`

**Interfaces:**
- Consumes: `Lingue.Base` (Task 1).
- Produces:
  - `StorageService.LinguaContenuti { get; set; }` (`string`, default `"it"`).
  - `StorageService.Etichette` (`EtichetteContenuti`, riempita durante `CaricaDomandeMateria`).
  - `EtichetteContenuti.Correnti` (static get/set), `Materia(string nomeCanonico) : string`, `Categoria(string nomeMateriaCanonico, string categoria) : string`, `Registra(string nomeMateriaCanonico, string? nome, IReadOnlyDictionary<string,string> categorie)`.

- [ ] **Step 1: Scrivi il test**

`$H/core/TestTraduzioni.cs` (costruisce un mini repo dati in una cartella temporanea):

```csharp
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Services;

public static class TestTraduzioni
{
    public static void Esegui()
    {
        string root = Path.Combine(Path.GetTempPath(), "l10n-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "domande", "x"));
        Directory.CreateDirectory(Path.Combine(root, "traduzioni", "en"));
        File.WriteAllText(Path.Combine(root, "materie.json"),
            """[{"id":"x","nome":"Sicurezza","cartella":"domande/x","formato":"standard"}]""");
        File.WriteAllText(Path.Combine(root, "domande", "x", "a.json"), """
        [
          {"categoria":"Fondamenti","domanda":"Uno?","risposte":["a","b","c","d"],"rispostaCorretta":1,"spiegazione":"S1"},
          {"categoria":"Fondamenti","domanda":"Due?","risposte":["a","b","c","d"],"rispostaCorretta":2,"spiegazione":"S2"},
          {"categoria":"Altro","domanda":"Tre?","risposte":["a","b","c","d"],"rispostaCorretta":0,"spiegazione":"S3"}
        ]
        """);
        string utente = Path.Combine(root, "utente");

        // Caricamento italiano: riferimento per gli invarianti.
        var it = new StorageService(root, utente);
        var domIt = it.CaricaTutteLeDomande(it.CaricaMaterie());
        string id1 = domIt[0].Id, id2 = domIt[1].Id, id3 = domIt[2].Id;

        File.WriteAllText(Path.Combine(root, "traduzioni", "en", "x.json"), $$"""
        {
          "nome": "Security",
          "categorie": { "Fondamenti": "Basics" },
          "domande": {
            "{{id1}}": { "domanda": "One?", "risposte": ["A","B","C","D"], "spiegazione": "E1" },
            "{{id2}}": { "domanda": "Two?", "risposte": ["A","B","C"], "spiegazione": "E2" },
            "ffffffffffff": { "domanda": "Orphan?" }
          }
        }
        """);

        var en = new StorageService(root, utente) { LinguaContenuti = "en" };
        var domEn = en.CaricaTutteLeDomande(en.CaricaMaterie());

        Verifica.Check("overlay: testo tradotto", domEn[0].DomandaTesto == "One?");
        Verifica.Check("overlay: risposte tradotte", domEn[0].Risposte[0] == "A");
        Verifica.Check("overlay: spiegazione tradotta", domEn[0].Spiegazione == "E1");
        Verifica.Check("invariante: Id uguali a it", domEn.Select(d => d.Id).SequenceEqual(new[] { id1, id2, id3 }));
        Verifica.Check("invariante: RispostaCorretta", domEn[0].RispostaCorretta == 1);
        Verifica.Check("invariante: Categoria canonica", domEn[0].Categoria == "Fondamenti");
        Verifica.Check("invariante: MateriaNome canonico", domEn[0].MateriaNome == "Sicurezza");
        Verifica.Check("risposte di numero diverso -> resta in italiano", domEn[1].DomandaTesto == "Due?");
        Verifica.Check("domanda senza traduzione -> italiano", domEn[2].DomandaTesto == "Tre?");
        Verifica.Check("etichetta materia", en.Etichette.Materia("Sicurezza") == "Security");
        Verifica.Check("etichetta categoria", en.Etichette.Categoria("Sicurezza", "Fondamenti") == "Basics");
        Verifica.Check("etichetta categoria non tradotta -> chiave", en.Etichette.Categoria("Sicurezza", "Altro") == "Altro");
        Verifica.Check("etichetta materia sconosciuta -> chiave", en.Etichette.Materia("Quizzone") == "Quizzone");

        // Lingua senza file: tutto in italiano, nessuna eccezione.
        var fr = new StorageService(root, utente) { LinguaContenuti = "fr" };
        var domFr = fr.CaricaTutteLeDomande(fr.CaricaMaterie());
        Verifica.Check("lingua senza file -> italiano", domFr[0].DomandaTesto == "Uno?");

        // File malformato: italiano, nessuna eccezione.
        File.WriteAllText(Path.Combine(root, "traduzioni", "en", "x.json"), "{ non json");
        var rotto = new StorageService(root, utente) { LinguaContenuti = "en" };
        var domRotto = rotto.CaricaTutteLeDomande(rotto.CaricaMaterie());
        Verifica.Check("file malformato -> italiano", domRotto[0].DomandaTesto == "Uno?");

        // In italiano l'overlay non si applica nemmeno se il file c'e'.
        Verifica.Check("lingua it -> nessun overlay", domIt[0].DomandaTesto == "Uno?");

        Directory.Delete(root, true);
    }
}
```

In `$H/core/Program.cs` aggiungi `TestTraduzioni.Esegui();` dopo `TestLingue.Esegui();`.

- [ ] **Step 2: Esegui, deve fallire**

Run: `cd $H/core && dotnet run`
Expected: errore di compilazione (`LinguaContenuti`, `Etichette` non esistono).

- [ ] **Step 3: Modello della traduzione**

`wsa.quiz.core/Models/TraduzioneMateria.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Wsa.Quiz.Core.Models;

/// <summary>
/// File traduzioni/&lt;lingua&gt;/&lt;materiaId&gt;.json. Ogni campo e' opzionale:
/// cio' che manca resta in italiano. Le domande sono indicizzate per Domanda.Id,
/// calcolato sempre sul testo italiano.
/// </summary>
public class TraduzioneMateria
{
    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    /// <summary>Categoria canonica (italiana) -> nome visualizzato.</summary>
    [JsonPropertyName("categorie")]
    public Dictionary<string, string> Categorie { get; set; } = new();

    [JsonPropertyName("domande")]
    public Dictionary<string, TraduzioneDomanda> Domande { get; set; } = new();
}

public class TraduzioneDomanda
{
    [JsonPropertyName("domanda")]
    public string? Domanda { get; set; }

    /// <summary>Stesso ordine e stesso numero dell'originale: RispostaCorretta non si traduce.</summary>
    [JsonPropertyName("risposte")]
    public List<string>? Risposte { get; set; }

    [JsonPropertyName("spiegazione")]
    public string? Spiegazione { get; set; }
}
```

- [ ] **Step 4: `EtichetteContenuti`**

`wsa.quiz.core/Services/EtichetteContenuti.cs`:

```csharp
namespace Wsa.Quiz.Core.Services;

/// <summary>
/// Nomi visualizzati di materie e categorie nella lingua corrente. Materia.Nome,
/// Domanda.MateriaNome e Domanda.Categoria restano chiavi canoniche (italiane):
/// statistiche e filtri cronologia raggruppano su quelle. Senza traduzione si
/// restituisce la chiave stessa.
/// </summary>
public sealed class EtichetteContenuti
{
    // ponytail: istanza globale perche' la lingua e' globale al processo;
    // MainView la sostituisce a ogni caricamento dei dati.
    public static EtichetteContenuti Correnti { get; set; } = new();

    private readonly Dictionary<string, string> _materie = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Materia, string Categoria), string> _categorie = new();

    public void Registra(string nomeMateriaCanonico, string? nome, IReadOnlyDictionary<string, string> categorie)
    {
        if (!string.IsNullOrWhiteSpace(nome))
            _materie[nomeMateriaCanonico] = nome;
        foreach (var (chiave, visualizzato) in categorie)
            if (!string.IsNullOrWhiteSpace(visualizzato))
                _categorie[(nomeMateriaCanonico, chiave)] = visualizzato;
    }

    public string Materia(string nomeCanonico)
        => _materie.TryGetValue(nomeCanonico, out var n) ? n : nomeCanonico;

    public string Categoria(string nomeMateriaCanonico, string categoria)
        => _categorie.TryGetValue((nomeMateriaCanonico, categoria), out var n) ? n : categoria;
}
```

- [ ] **Step 5: Overlay in `StorageService`**

Accanto alla proprietà `CartellaUtente` aggiungi:

```csharp
    /// <summary>
    /// Lingua dei contenuti. Diversa da quella base -> al caricamento si applicano
    /// le traduzioni in traduzioni/&lt;lingua&gt;/. Default "it": la CLI non la tocca.
    /// </summary>
    public string LinguaContenuti { get; set; } = Risorse.Lingue.Base;

    /// <summary>Nomi tradotti di materie e categorie raccolti durante il caricamento.</summary>
    public EtichetteContenuti Etichette { get; } = new();
```

In `CaricaDomandeMateria`, subito prima di `return risultato;` (dopo il ciclo che assegna gli Id):

```csharp
        if (LinguaContenuti != Risorse.Lingue.Base)
            ApplicaTraduzione(materia, risultato);
```

E il metodo privato, dopo `GeneraId`:

```csharp
    /// <summary>
    /// Sovrappone testo, risposte e spiegazione tradotti. Mai bloccante: file
    /// assente o rotto -> materia in italiano; voci incoerenti -> quella domanda
    /// in italiano. Id, RispostaCorretta, Categoria e MateriaNome non si toccano.
    /// </summary>
    private void ApplicaTraduzione(Materia materia, List<Domanda> domande)
    {
        string percorso = $"traduzioni/{LinguaContenuti}/{materia.Id}.json";
        if (!_fonte.Esiste(percorso)) return;

        TraduzioneMateria? t;
        try
        {
            t = JsonSerializer.Deserialize<TraduzioneMateria>(_fonte.LeggiTesto(percorso), OpzioniLettura);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ATTENZIONE] Traduzione '{percorso}' illeggibile: {ex.Message}");
            return;
        }
        if (t == null) return;

        Etichette.Registra(materia.Nome, t.Nome, t.Categorie);

        var perId = domande.ToDictionary(d => d.Id);
        foreach (var (id, td) in t.Domande)
        {
            if (!perId.TryGetValue(id, out var d))
            {
                Console.Error.WriteLine($"[ATTENZIONE] {percorso}: id '{id}' senza domanda corrispondente.");
                continue;
            }
            if (td.Risposte != null && td.Risposte.Count != d.Risposte.Count)
            {
                Console.Error.WriteLine($"[ATTENZIONE] {percorso}: id '{id}' ha {td.Risposte.Count} risposte invece di {d.Risposte.Count}.");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(td.Domanda)) d.DomandaTesto = td.Domanda;
            if (td.Risposte != null) d.Risposte = td.Risposte;
            if (!string.IsNullOrWhiteSpace(td.Spiegazione)) d.Spiegazione = td.Spiegazione;
        }
    }
```

(Se `ToDictionary` dovesse lanciare per Id duplicati: impossibile, il ciclo precedente garantisce Id unici con il suffisso `-2`, `-3`.)

- [ ] **Step 6: Esegui, deve passare**

Run: `cd $H/core && dotnet run`
Expected: tutte PASS (Task 1 + Task 2). Gli avvisi `[ATTENZIONE]` su stderr per l'id orfano, le risposte di numero diverso e il file malformato sono attesi.

- [ ] **Step 7: Commit e push**

```bash
git add wsa.quiz.core/Models/TraduzioneMateria.cs wsa.quiz.core/Services/EtichetteContenuti.cs wsa.quiz.core/Services/StorageService.cs
git commit -m "feat(l10n): overlay delle traduzioni domande ed etichette di materie/categorie"
git push
```

---

### Task 3: Stringhe user-facing di Core

**Files:**
- Modify: `wsa.quiz.core/Services/ValidatoreImportDomande.cs`, `wsa.quiz.core/Services/EsportazioneService.cs`, più ogni altro file di Core con stringhe che arrivano all'utente (vedi Step 1)
- Modify: `wsa.quiz.core/Risorse/Strings.resx`, `Strings.en.resx`
- Test: `$H/core/TestCore.cs`

**Interfaces:**
- Consumes: `Strings`, `Lingue.Applica` (Task 1).
- Produces: `EsportazioneService.VersoCsv` invariato nella firma, ma separatore e decimale seguono `CultureInfo.CurrentCulture`; intestazioni CSV, `Stato` e `Si/No` da `Strings`. `VersoJson`: chiavi invariate (formato dati), valore di `Stato` localizzato.

- [ ] **Step 1: Inventario**

Run:
```bash
grep -nE '"[^"]*[A-Za-zà-ù]{3,}[^"]*"' wsa.quiz.core/Models/*.cs wsa.quiz.core/Models/Statistiche/*.cs wsa.quiz.core/Services/*.cs \
  | grep -v '///' | grep -v 'JsonPropertyName' | grep -v 'Console.Error'
```
Regola per ogni riga: **si traduce** se il testo arriva all'utente (messaggi di `ValidatoreImportDomande`, intestazioni/valori CSV, eccezioni il cui `Message` viene mostrato in un pannello d'errore dell'app — verificarlo cercando il chiamante con grep). **Non si traduce**: nomi di file, chiavi JSON, log su `Console.Error`, eccezioni di programmazione (`ArgumentException` su path), `FiltroCronologia.EtichettaQuizzone` (`"Quizzone"` è una chiave salvata in cronologia).

- [ ] **Step 2: Scrivi il test**

`$H/core/TestCore.cs`:

```csharp
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

public static class TestCore
{
    public static void Esegui()
    {
        var r = new RisultatoQuiz
        {
            DataOra = new DateTime(2026, 10, 6, 10, 0, 0), Modalita = "M", MateriaNome = "SQL",
            TotaleDomande = 2, RisposteCorrette = 1, RisposteErrate = 1,
            PercentualeCorrette = 55.5, DurataQuiz = TimeSpan.FromSeconds(90), TempoScaduto = true,
        };

        Lingue.Applica("it");
        string csvIt = EsportazioneService.VersoCsv([r]);
        string[] righeIt = csvIt.Split("\r\n");
        Verifica.Check("CSV it: separatore ;", righeIt[0].StartsWith("Data;"));
        Verifica.Check("CSV it: decimale con virgola", righeIt[1].Contains(";55,5;"));
        Verifica.Check("CSV it: stato", righeIt[1].Contains("Tempo scaduto"));

        Lingue.Applica("en");
        string csvEn = EsportazioneService.VersoCsv([r]);
        string[] righeEn = csvEn.Split("\r\n");
        Verifica.Check("CSV en: separatore ,", righeEn[0].StartsWith("Date,"));
        Verifica.Check("CSV en: decimale col punto", righeEn[1].Contains(",55.5,"));
        Verifica.Check("CSV en: stato", righeEn[1].Contains("Time expired"));
        Verifica.Check("JSON: chiavi invariate", EsportazioneService.VersoJson([r]).Contains("\"Percentuale\""));

        var (_, erroriEn) = ValidatoreImportDomande.Valida("[]");
        Verifica.Check("Validatore en: messaggio in inglese", erroriEn.Any(e => e.Contains("no questions")));
        Lingue.Applica("it");
        var (_, erroriIt) = ValidatoreImportDomande.Valida("[]");
        Verifica.Check("Validatore it: messaggio in italiano", erroriIt.Any(e => e.Contains("non contiene domande")));
    }
}
```

(`ValidatoreImportDomande.Valida(string json)` restituisce `(List<Domanda> domande, List<string> errori)`.) Aggiungi `TestCore.Esegui();` in `Program.cs`.

- [ ] **Step 3: Esegui, deve fallire**

Run: `cd $H/core && dotnet run`
Expected: FAIL su "CSV en" e "Validatore en".

- [ ] **Step 4: `EsportazioneService`**

Sostituisci la costante `Separatore`, l'array `Intestazione`, `Quota`, `Decimale`, `Stato` e `SiNo` così:

```csharp
    /// <summary>
    /// Separatore di colonna secondo la cultura: dove la virgola e' il separatore
    /// decimale (italiano) Excel si aspetta il punto e virgola, altrove la virgola.
    /// </summary>
    private static char Separatore
        => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? ';' : ',';

    private static string[] Intestazione =>
    [
        Strings.Csv_Data, Strings.Csv_Modalita, Strings.Csv_Materia, Strings.Csv_Materie,
        Strings.Csv_Totale, Strings.Csv_Corrette, Strings.Csv_Errate, Strings.Csv_Percentuale,
        Strings.Csv_Durata, Strings.Csv_DurataSecondi, Strings.Csv_Stato, Strings.Csv_Punteggio,
        Strings.Csv_Rotazione, Strings.Csv_Cronometro,
    ];
```

In `VersoCsv` leggi il separatore una volta (`char sep = Separatore;`) e passalo: `string.Join(sep, ...)` e `campi.Select(c => Quota(c, sep))`. `Quota(string campo, char sep)` usa `campo.IndexOf(sep)`.

```csharp
    /// <summary>Decimale nella cultura corrente, senza zeri inutili: 90 -> "90", 55.5 -> "55,5" / "55.5".</summary>
    private static string Decimale(double v) => v.ToString("0.##", CultureInfo.CurrentCulture);

    private static string Stato(RisultatoQuiz r)
        => r.Abbandonato ? Strings.Stato_Abbandonato
         : r.TempoScaduto ? Strings.Stato_TempoScaduto
         : Strings.Stato_Concluso;

    private static string SiNo(bool v) => v ? Strings.Comune_Si : Strings.Comune_No;
```

Aggiungi `using Wsa.Quiz.Core.Risorse;`. Chiavi resx:

| Chiave | it | en |
|---|---|---|
| `Csv_Data` | Data | Date |
| `Csv_Modalita` | Modalita | Mode |
| `Csv_Materia` | Materia | Subject |
| `Csv_Materie` | Materie | Subjects |
| `Csv_Totale` | Totale | Total |
| `Csv_Corrette` | Corrette | Correct |
| `Csv_Errate` | Errate | Wrong |
| `Csv_Percentuale` | Percentuale | Percentage |
| `Csv_Durata` | Durata | Duration |
| `Csv_DurataSecondi` | DurataSecondi | DurationSeconds |
| `Csv_Stato` | Stato | Status |
| `Csv_Punteggio` | Punteggio | Score |
| `Csv_Rotazione` | Rotazione | Rotation |
| `Csv_Cronometro` | Cronometro | Timer |
| `Stato_Abbandonato` | Abbandonato | Abandoned |
| `Stato_TempoScaduto` | Tempo scaduto | Time expired |
| `Stato_Concluso` | Concluso | Completed |
| `Comune_Si` | Si | Yes |
| `Comune_No` | No | No |

Le chiavi `Stato_*` vanno riusate (non duplicate) dalle view di cronologia nel Task 7.

- [ ] **Step 5: `ValidatoreImportDomande` e restanti file dell'inventario**

Ogni messaggio diventa una chiave `Import_*` con segnaposto. Esempio completo per il validatore:

```csharp
errori.Add(string.Format(Strings.Import_JsonNonValido, ex.Message));
errori.Add(Strings.Import_NessunaDomanda);
errori.Add(string.Format(Strings.Import_DomandaMancante, n));
errori.Add(string.Format(Strings.Import_AlmenoDueRisposte, n));
errori.Add(string.Format(Strings.Import_CorrettaFuoriRange, n));
errori.Add(string.Format(Strings.Import_RisposteDuplicate, n));
errori.Add(string.Format(Strings.Import_SpiegazioneMancante, n));
errori.Add(string.Format(Strings.Import_DomandaDuplicata, i + 1, Tronca(testi[i])));
```

| Chiave | it | en |
|---|---|---|
| `Import_JsonNonValido` | JSON non valido: {0} | Invalid JSON: {0} |
| `Import_NessunaDomanda` | Il file non contiene domande (array JSON vuoto o assente). | The file contains no questions (empty or missing JSON array). |
| `Import_DomandaMancante` | #{0}: domanda mancante | #{0}: missing question |
| `Import_AlmenoDueRisposte` | #{0}: servono almeno 2 risposte | #{0}: at least 2 answers are required |
| `Import_CorrettaFuoriRange` | #{0}: rispostaCorretta fuori range | #{0}: rispostaCorretta out of range |
| `Import_RisposteDuplicate` | #{0}: risposte duplicate nella stessa domanda | #{0}: duplicate answers in the same question |
| `Import_SpiegazioneMancante` | #{0}: spiegazione mancante | #{0}: missing explanation |
| `Import_DomandaDuplicata` | #{0}: domanda duplicata nel file ("{1}") | #{0}: duplicate question in the file ("{1}") |

Per le righe restanti dell'inventario applica la regola dello Step 1, stessa tecnica.

- [ ] **Step 6: Esegui, deve passare**

Run: `cd $H/core && dotnet run` → `TUTTO OK` (compresa la parità delle chiavi).
Run: `dotnet build wsa.quiz.cli && dotnet build wsa.quiz.desktop` → verdi.

- [ ] **Step 7: Commit e push**

```bash
git add wsa.quiz.core
git commit -m "feat(l10n): stringhe di Core localizzate, CSV con separatori della lingua"
git push
```

---

### Task 4: Lingua all'avvio, selettore in Impostazioni, ricreazione della vista

**Files:**
- Modify: `wsa.quiz.core/Models/PreferenzeUtente.cs`, `wsa.quiz.app/App.axaml.cs`, `wsa.quiz.app/MainView.axaml.cs` (`Caricamento` ~riga 321, costruttore ~riga 70), `wsa.quiz.app/Views/ImpostazioniView.axaml(.cs)`
- Test: `$H/app/` (harness headless)

**Interfaces:**
- Consumes: `Lingue.*` (Task 1), `StorageService.LinguaContenuti`, `StorageService.Etichette`, `EtichetteContenuti.Correnti` (Task 2).
- Produces:
  - `PreferenzeUtente.Lingua` (`string`, default `""`).
  - `App.LinguaIniziale()` (applica la lingua dalle preferenze), `App.SostituisciVistaPrincipale(MainView vecchia)`.
  - `ImpostazioniView.QuizInCorso` (`bool`, set da `MainView`), `LingueDisponibili`, `LinguaSelezionata`, `LinguaModificabile => !QuizInCorso`.
  - Harness `$H/app` riusato dai Task 5–8.

- [ ] **Step 1: Crea l'harness App**

```bash
mkdir -p $H/app $H/cfg && cd $H/app
cat > app.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia.Headless" Version="12.0.2" />
    <ProjectReference Include="<repo>/wsa.quiz.app/Wsa.Quiz.App.csproj" />
  </ItemGroup>
  <!-- Dati read-only nel bin come per il desktop. -->
  <ItemGroup>
    <None Include="<repo>/materie.json" CopyToOutputDirectory="PreserveNewest" />
    <None Include="<repo>/domande/**/*.json" CopyToOutputDirectory="PreserveNewest" Link="domande/%(RecursiveDir)%(Filename)%(Extension)" />
    <None Include="<repo>/traduzioni/**/*.json" CopyToOutputDirectory="PreserveNewest" Link="traduzioni/%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>
</Project>
EOF
cat > Program.cs <<'EOF'
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;

int fallite = 0;
void Check(string nome, bool ok) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {nome}"); if (!ok) fallite++; }
void Pompa() { for (int i = 0; i < 20; i++) Dispatcher.UIThread.RunJobs(); }

AppBuilder.Configure<Wsa.Quiz.App.App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();

TestApp.Esegui(Check, Pompa);

Console.WriteLine(fallite == 0 ? "TUTTO OK" : $"{fallite} FALLITI");
return fallite == 0 ? 0 : 1;
EOF
```

Lanciare sempre con: `cd $H/app && rm -rf $H/cfg && XDG_CONFIG_HOME=$H/cfg dotnet run`.
(`rm -rf $H/cfg` = preferenze pulite a ogni giro; `SetupWithoutStarting` non esegue `OnFrameworkInitializationCompleted`, quindi l'harness chiama da sé `App.LinguaIniziale()` e crea la finestra.)

- [ ] **Step 2: Scrivi il test**

`$H/app/TestApp.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.VisualTree;
using Wsa.Quiz.App;
using Wsa.Quiz.App.Views;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

public static class TestApp
{
    public static void Esegui(Action<string, bool> Check, Action Pompa)
    {
        string cfg = Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")!, "WsaQuiz");
        var storage = new StorageService(AppContext.BaseDirectory, cfg);

        // 1. Avvio con preferenza "en".
        storage.SalvaPreferenze(new PreferenzeUtente { Lingua = "en", TabIniziale = 5 });
        App.LinguaIniziale();
        Check("avvio: lingua da preferenze", Lingue.Corrente == "en");

        var window = new Window { Width = 1080, Height = 760, Content = new MainView() };
        window.Show();
        Pompa();
        var imp = window.GetVisualDescendants().OfType<ImpostazioniView>().First();
        Check("selettore: voce corrente = English", imp.LinguaSelezionata?.Codice == "en");
        Check("selettore abilitato senza quiz", imp.LinguaModificabile);

        // 2. Toggle tema: non deve scrivere la lingua se l'utente non l'ha scelta.
        storage.SalvaPreferenze(new PreferenzeUtente { Lingua = "", TabIniziale = 5 });
        imp.TemaScuro = !imp.TemaScuro;
        Check("toggle tema non congela la lingua", storage.CaricaPreferenze().Lingua == "");

        // 3. Cambio lingua -> preferenza salvata, vista sostituita, riaperta su Impostazioni.
        var vecchia = window.Content;
        imp.LinguaSelezionata = imp.LingueDisponibili.First(l => l.Codice == "it");
        Pompa();
        Check("cambio: preferenza salvata", storage.CaricaPreferenze().Lingua == "it");
        Check("cambio: cultura applicata", Lingue.Corrente == "it");
        Check("cambio: MainView sostituita", window.Content is MainView && !ReferenceEquals(window.Content, vecchia));
        var imp2 = window.GetVisualDescendants().OfType<ImpostazioniView>().First();
        Check("cambio: nuova Impostazioni in italiano", imp2.LinguaSelezionata?.Codice == "it");

        // 4. Quiz in corso -> selettore disabilitato.
        imp2.QuizInCorso = true;
        Check("quiz in corso -> selettore disabilitato", !imp2.LinguaModificabile);
        imp2.QuizInCorso = false;

        window.Close();
    }
}
```

- [ ] **Step 3: Esegui, deve fallire**

Run: `cd $H/app && rm -rf $H/cfg && XDG_CONFIG_HOME=$H/cfg dotnet run`
Expected: errore di compilazione (`PreferenzeUtente.Lingua`, `App.LinguaIniziale`, `LinguaSelezionata` non esistono). Se `traduzioni/` non esiste ancora, il glob `None` non trova nulla: va bene.

- [ ] **Step 4: Preferenza**

In `PreferenzeUtente.cs`, dopo `TemaScuro`:

```csharp
    /// <summary>Codice lingua ("it", "en"); vuoto = segui la lingua di sistema.</summary>
    public string Lingua { get; set; } = "";
```

- [ ] **Step 5: `App.axaml.cs`**

Aggiungi `using System.Globalization; using Avalonia.Controls.Primitives; using Wsa.Quiz.Core.Risorse; using Wsa.Quiz.Core.Services;` e i due metodi:

```csharp
    /// <summary>
    /// Applica la lingua da preferenze.json prima di creare qualunque vista.
    /// La lingua di sistema e' InstalledUICulture: resta quella del sistema anche
    /// dopo che Applica ha cambiato la cultura corrente.
    /// </summary>
    public static void LinguaIniziale()
    {
        string preferenza = "";
        try
        {
            // Solo la cartella utente conta qui; la fonte dati non viene letta.
            var storage = new StorageService(new FileSystemFonteDati(AppContext.BaseDirectory),
                                             StorageService.CartellaUtenteDefault());
            preferenza = storage.CaricaPreferenze().Lingua;
        }
        catch (Exception) { /* preferenze illeggibili: si segue il sistema */ }

        Lingue.Applica(Lingue.Risolvi(preferenza, CultureInfo.InstalledUICulture));
    }

    /// <summary>
    /// Mette una MainView nuova al posto di quella vecchia nel suo genitore: la
    /// finestra desktop (ContentControl), il LayoutTransformControl su Android
    /// (Decorator) o la finestra di un harness. La nuova istanza rilegge dati,
    /// traduzioni e preferenze, quindi si riapre sull'ultima sezione salvata.
    /// </summary>
    public static void SostituisciVistaPrincipale(MainView vecchia)
    {
        switch (vecchia.Parent)
        {
            case ContentControl cc: cc.Content = new MainView(); break;
            case Decorator dec: dec.Child = new MainView(); break;
        }
    }
```

In `OnFrameworkInitializationCompleted`, prima riga del metodo: `LinguaIniziale();`.

- [ ] **Step 6: `MainView`**

In `Caricamento()`, subito dopo `_storage = new StorageService(fonte, cartellaUtente);`:

```csharp
            _storage.LinguaContenuti = Lingue.Corrente;
```

e subito dopo il caricamento delle domande built-in (`CaricaTutteLeDomande`):

```csharp
            EtichetteContenuti.Correnti = _storage.Etichette;
```

Nel costruttore, dopo `InitializeComponent()`, una sola sottoscrizione che tiene aggiornato il blocco del selettore qualunque sia il punto che cambia la Home (avvio quiz, ripresa, riepilogo, ritorno):

```csharp
        _homeArea.PropertyChanged += (_, e) =>
        {
            if (e.Property == ContentControl.ContentProperty && _impostazioniView != null)
                _impostazioniView.QuizInCorso = _homeArea.Content is QuizView;
        };
```

(`_homeArea` è il `ContentControl` della Home; verificare il nome reale del campo, alle righe ~379/476 del file.) Aggiungi `using Wsa.Quiz.Core.Risorse;`.

- [ ] **Step 7: `ImpostazioniView`**

Code-behind: sostituisci la regione `LINGUA (segnaposto)` con:

```csharp
    // ------------------------------------------------------------------ LINGUA

    public IReadOnlyList<VoceLingua> LingueDisponibili => Lingue.Supportate;

    private VoceLingua? _linguaSelezionata =
        Lingue.Supportate.FirstOrDefault(l => l.Codice == Lingue.Corrente);

    /// <summary>
    /// Il setter scatta solo per una scelta dell'utente (il campo e' gia'
    /// inizializzato alla lingua attiva). Salva, applica e ricrea la MainView:
    /// Post perche' non si sostituisce la vista dentro un suo stesso binding.
    /// </summary>
    public VoceLingua? LinguaSelezionata
    {
        get => _linguaSelezionata;
        set
        {
            if (value == null || value.Codice == _linguaSelezionata?.Codice) return;
            _linguaSelezionata = value;
            Raise();
            Salva(p => p.Lingua = value.Codice);
            Lingue.Applica(value.Codice);
            var main = this.FindAncestorOfType<MainView>();
            if (main != null)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => App.SostituisciVistaPrincipale(main));
        }
    }

    private bool _quizInCorso;
    /// <summary>Impostato dalla MainView: durante un quiz cambiare lingua lo farebbe perdere.</summary>
    public bool QuizInCorso
    {
        get => _quizInCorso;
        set
        {
            if (_quizInCorso == value) return;
            _quizInCorso = value;
            Raise();
            Raise(nameof(LinguaModificabile));
        }
    }

    public bool LinguaModificabile => !_quizInCorso;
```

Sostituisci `Salva()` con una versione che scrive solo il campo cambiato (così il toggle del tema non congela `Lingua == ""`):

```csharp
    private void Salva(Action<PreferenzeUtente> modifica)
    {
        if (_storage == null) return;
        var prefs = _storage.CaricaPreferenze();
        modifica(prefs);
        _storage.SalvaPreferenze(prefs);
    }
```

e nel setter di `TemaScuro`: `Salva(p => p.TemaScuro = _temaScuro);`. Aggiungi `using System.Linq; using Avalonia.VisualTree; using Wsa.Quiz.Core.Risorse;`.

XAML: aggiungi al root `xmlns:r="clr-namespace:Wsa.Quiz.Core.Risorse;assembly=Wsa.Quiz.Core"` e sostituisci il riquadro segnaposto della lingua:

```xml
                <Border Background="{DynamicResource SystemRegionBrush}"
                        BorderBrush="{DynamicResource SystemBaseLowColor}"
                        BorderThickness="1"
                        CornerRadius="6"
                        Padding="16,14">
                    <Grid ColumnDefinitions="*,Auto">
                        <StackPanel Spacing="2" VerticalAlignment="Center">
                            <TextBlock Text="{x:Static r:Strings.Lingua_Nome}" FontWeight="SemiBold"/>
                            <TextBlock Text="{x:Static r:Strings.Lingua_Descrizione}"
                                       FontSize="12" TextWrapping="Wrap"
                                       Foreground="{DynamicResource SystemBaseMediumColor}"/>
                            <TextBlock Text="{x:Static r:Strings.Lingua_BloccataDuranteQuiz}"
                                       IsVisible="{Binding QuizInCorso}"
                                       FontSize="12" TextWrapping="Wrap"
                                       Foreground="#B8860B"/>
                        </StackPanel>
                        <ComboBox Grid.Column="1"
                                  MinWidth="130"
                                  VerticalAlignment="Center"
                                  Margin="12,0,0,0"
                                  ItemsSource="{Binding LingueDisponibili}"
                                  SelectedItem="{Binding LinguaSelezionata}"
                                  IsEnabled="{Binding LinguaModificabile}"/>
                    </Grid>
                </Border>
```

(Ambra `#B8860B` = `QuizColors` ambra, hex esplicito come da Trappola 20.)

- [ ] **Step 8: Esegui, deve passare**

Run: `cd $H/app && rm -rf $H/cfg && XDG_CONFIG_HOME=$H/cfg dotnet run` → `TUTTO OK`.
Run: `cd $H/core && dotnet run` → `TUTTO OK`.

- [ ] **Step 9: Commit e push**

```bash
git add wsa.quiz.core/Models/PreferenzeUtente.cs wsa.quiz.app
git commit -m "feat(l10n): lingua da preferenze all'avvio, selettore attivo, MainView ricreata al cambio"
git push
```

---

## Procedura comune ai Task 5–8 (estrazione stringhe)

Ogni task copre un gruppo di file. Per **ogni** file del gruppo:

1. **XAML**: aggiungi al root `xmlns:r="clr-namespace:Wsa.Quiz.Core.Risorse;assembly=Wsa.Quiz.Core"`. Ogni attributo `Text`, `Content`, `Header`, `ToolTip.Tip`, `OnContent`, `OffContent`, `Title` con testo letterale diventa `"{x:Static r:Strings.<Chiave>}"`. `Watermark` (obsoleto) diventa `PlaceholderText="{x:Static r:Strings.<Chiave>}"`. I `<Run Text="..."/>` letterali idem.
2. **`StringFormat` con testo** (es. `{Binding N, StringFormat='{}{0} domande'}`): sposta in una proprietà calcolata nel code-behind/item, `string.Format(Strings.<Chiave>, N)`, e lega `Text` a quella.
3. **Code-behind / State**: ogni letterale user-facing → `Strings.<Chiave>`; interpolazioni → `string.Format(Strings.<Chiave>, a, b)` con `{0}`, `{1}` nella risorsa. Plurali → coppia `_Uno`/`_Molti`.
4. **Non si traducono**: lettere `A/B/C/D`, simboli (`↻`, `⏱`, `—`, `·`, frecce), hex, nomi di file, URI `avares://`, log `Console.Error`, la label `"Quizzone"` (chiave in cronologia), il brand `Quaestio`, il percorso dati.
5. **Nomi di materie e categorie mostrati** → `EtichetteContenuti.Correnti.Materia(nomeCanonico)` / `.Categoria(nomeMateriaCanonico, categoria)`. Mai sovrascrivere il valore canonico dove viene usato come chiave (filtri, `OpzioniQuiz.Categorie`, `SezioneEsclusa`, `RisultatoQuiz.MateriaNome`, `DettaglioRisposta`).
6. **Formattazione di numeri e date** (`P0`, `N1`, date): passa sempre `Lingue.Cultura` — `x.ToString("P0", Lingue.Cultura)`, `string.Format(Lingue.Cultura, Strings.X, ...)`, `ToString("g", Lingue.Cultura)`. **Mai** affidarsi a `CultureInfo.CurrentCulture`: è AsyncLocal, e i callback accodati prima di un cambio lingua (timer, job del dispatcher) la vedrebbero vecchia (ruling Task 4).
7. Aggiungi ogni chiave a **entrambi** i resx (traduzione inglese naturale, non letterale; tono dell'app: diretto, seconda persona).

**Controllo di copertura** a fine task, sui soli file del gruppo (sostituire `FILES`):

```bash
# XAML: deve stampare 0
grep -nE '(Text|Content|Header|ToolTip\.Tip|OnContent|OffContent|Title|Watermark|PlaceholderText)="[^"{]*[A-Za-zÀ-ÿ]{2,}[^"]*"' FILES_AXAML | wc -l
# C#: ogni riga rimasta va giustificata dalla regola 4
grep -nE '"[^"]*[a-zà-ù]{3,}[^"]*"' FILES_CS | grep -v '///' | grep -v 'Console.Error' | grep -v 'nameof' | grep -v 'avares://' | grep -v 'Strings\.'
```

Poi: `cd $H/core && dotnet run` (parità chiavi) e il test App del task.

---

### Task 5: Shell, Home, Impostazioni

**Files:**
- Modify: `wsa.quiz.app/MainView.axaml(.cs)`, `wsa.quiz.app/Views/HomeView.axaml(.cs)`, `wsa.quiz.app/Views/ImpostazioniView.axaml(.cs)` (resto della view), `wsa.quiz.app/Views/PlaceholderView.axaml(.cs)`, `wsa.quiz.app/State/MateriaSelezionabile.cs`, `wsa.quiz.app/State/CategoriaSelezionabile.cs`, resx
- Test: `$H/app/TestApp.cs` (estensione)

**Interfaces:**
- Consumes: Task 1–4.
- Produces: chiavi `Shell_*`, `Home_*`, `Impostazioni_*`, `Comune_*` (es. `Comune_Annulla`, riusata dai task successivi). `OpzioniQuiz.NomeModalita` costruito in `HomeView` con `Strings` (finisce in cronologia come testo nella lingua giocata: atteso). `MateriaLabel` e chiavi categoria in `OpzioniQuiz` restano canoniche.

- [ ] **Step 1: Estendi il test**

In fondo a `TestApp.Esegui`, prima di `window.Close()`:

```csharp
        // Shell e Home in inglese.
        storage.SalvaPreferenze(new PreferenzeUtente { Lingua = "en", TabIniziale = 0 });
        App.LinguaIniziale();
        window.Content = new MainView();
        Pompa();
        var testi = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
        Check("shell en: nessun 'Avvia quiz' italiano", !testi.Any(t => t.Contains("Avvia quiz")));
        Check("shell en: c'e' 'Start quiz'", testi.Any(t => t.Contains("Start quiz")) && Strings.Home_AvviaQuiz == "Start quiz");
```

(Se in `HomeView` il bottone "Avvia" ha un testo diverso da "Avvia quiz", adatta sia la chiave sia l'asserzione al testo reale; il senso del test è: il testo italiano non compare più, quello inglese sì.)

- [ ] **Step 2: Esegui, deve fallire**

Run: `cd $H/app && rm -rf $H/cfg && XDG_CONFIG_HOME=$H/cfg dotnet run`
Expected: errore di compilazione (`Strings.Home_AvviaQuiz` non esiste) o FAIL.

- [ ] **Step 3: Estrai le stringhe** dei file del gruppo seguendo la Procedura comune. In `HomeView`, la lista materie mostra `EtichetteContenuti.Correnti.Materia(m.Nome)` (aggiungi a `MateriaSelezionabile` una proprietà `Etichetta` e lega il `CheckBox.Content` a quella); le categorie mostrano `EtichetteContenuti.Correnti.Categoria(d.MateriaNome, categoria)` (proprietà `Etichetta` su `CategoriaSelezionabile`). Il raggruppamento e i filtri (`HomeView.axaml.cs` ~righe 255, 314) continuano a usare `d.Categoria`. Nell'header di `MainView` anche il conteggio "N materie · M domande" passa da `Strings` con segnaposto. I nomi delle tab/voci della bottom-nav pure.

- [ ] **Step 4: Controllo di copertura + test**

Run il controllo di copertura sui file del gruppo (0 righe XAML; righe C# solo giustificate), poi `cd $H/core && dotnet run` e il test App → `TUTTO OK`.

- [ ] **Step 5: Commit e push**

```bash
git add wsa.quiz.app wsa.quiz.core/Risorse
git commit -m "feat(l10n): shell, Home e Impostazioni localizzate"
git push
```

---

### Task 6: Quiz, riepilogo, dialoghi

**Files:**
- Modify: `wsa.quiz.app/Views/QuizView.axaml(.cs)`, `wsa.quiz.app/State/SessioneQuiz.cs`, `wsa.quiz.app/State/RispostaItem.cs`, `wsa.quiz.app/Views/RiepilogoView.axaml(.cs)`, `wsa.quiz.app/Views/PausaDialog.axaml(.cs)`, `wsa.quiz.app/Views/ConfermaDialog.axaml(.cs)`, `wsa.quiz.app/Views/OverlayDialogBase.cs`, `wsa.quiz.app/QuizColors.cs` (solo se contiene testo), resx
- Test: `$H/app/TestApp.cs` (estensione)

**Interfaces:**
- Consumes: Task 1–5 (`Comune_*`).
- Produces: chiavi `Quiz_*`, `Riepilogo_*`, `Pausa_*`, `Conferma_*`.

- [ ] **Step 1: Estendi il test**

```csharp
        // Dialogo pausa in inglese: nessun testo italiano noto.
        var pausa = new PausaDialog();
        var hostP = new Window { Content = pausa };
        hostP.Show(); Pompa();
        var testiP = pausa.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
        Check("pausa en: niente 'Abbandona'", !testiP.Any(t => t.Contains("Abbandona")));
        Check("pausa en: niente 'Salva e esci'", !testiP.Any(t => t.Contains("Salva e esci")));
        hostP.Close();
```

(`PausaDialog` ha un costruttore senza argomenti.)

- [ ] **Step 2: Esegui, deve fallire** — FAIL su "pausa en".

- [ ] **Step 3: Estrai le stringhe** seguendo la Procedura comune. In `SessioneQuiz`:
  - `CategoriaCorrente` (righe ~334 e ~714) mostra `EtichetteContenuti.Correnti.Categoria(<materiaNome>, <categoria>)` — a riga 334 da `_domandaCorrente.Originale`, a riga 714 dal `DettaglioRisposta` (`d.MateriaNome`, `d.Categoria`).
  - A riga ~379 il `DettaglioRisposta.Categoria` resta `Originale.Categoria` (chiave per le statistiche) e `MateriaNome` resta canonico.
  - L'esito "Tempo scaduto" e gli altri titoli del riepilogo riusano `Stato_*` dove il testo coincide.
  - In `RiepilogoView`, la categoria delle sbagliate (`<Run Text="{Binding Categoria}"/>` riga ~122) va legata a una proprietà di visualizzazione che passa da `EtichetteContenuti`.

- [ ] **Step 4: Controllo di copertura + test** → `TUTTO OK`.

- [ ] **Step 5: Commit e push**

```bash
git add wsa.quiz.app wsa.quiz.core/Risorse
git commit -m "feat(l10n): quiz, riepilogo e dialoghi localizzati"
git push
```

---

### Task 7: Cronologia e Sospesi

**Files:**
- Modify: `wsa.quiz.app/Views/CronologiaView.axaml(.cs)`, `wsa.quiz.app/Views/CronologiaDettaglioView.axaml(.cs)`, `wsa.quiz.app/Views/SospesiView.axaml(.cs)`, `wsa.quiz.app/Views/TabellaViewBase.axaml.cs`, `wsa.quiz.app/State/RisultatoCronologiaItem.cs`, `wsa.quiz.app/State/SessioneSospesaItem.cs`, `wsa.quiz.app/State/DettaglioRispostaItem.cs`, resx
- Create: `wsa.quiz.app/State/VoceMateria.cs`
- Test: `$H/app/TestApp.cs` (estensione)

**Interfaces:**
- Consumes: `Stato_*` (Task 3), `Comune_*`, `EtichetteContenuti`.
- Produces: `VoceMateria(string Chiave, string Etichetta)` per il filtro materia; chiavi `Cronologia_*`, `Sospesi_*`, `Dettaglio_*`.

- [ ] **Step 1: Estendi il test**

```csharp
        // Filtro materia: mostra l'etichetta, filtra con la chiave canonica.
        var voce = new Wsa.Quiz.App.State.VoceMateria("Sicurezza Informatica", "Cybersecurity");
        Check("VoceMateria: ToString = etichetta", voce.ToString() == "Cybersecurity");
        Check("VoceMateria: chiave canonica", voce.Chiave == "Sicurezza Informatica");
```

- [ ] **Step 2: Esegui, deve fallire** — `VoceMateria` non esiste.

- [ ] **Step 3: `VoceMateria`**

`wsa.quiz.app/State/VoceMateria.cs`:

```csharp
namespace Wsa.Quiz.App.State;

/// <summary>
/// Voce del filtro materia in Cronologia: il ComboBox mostra l'etichetta nella
/// lingua corrente, il filtro usa la chiave canonica salvata in cronologia.
/// </summary>
public sealed record VoceMateria(string Chiave, string Etichetta)
{
    public override string ToString() => Etichetta;
}
```

In `CronologiaView.axaml.cs` la collezione delle materie del filtro diventa `ObservableCollection<VoceMateria>` costruita da `FiltroCronologia.MaterieDisponibili` con `new VoceMateria(n, EtichettaMateria(n))`, dove `EtichettaMateria(n) = EtichetteContenuti.Correnti.Materia(n)`; la voce "tutte le materie" è una `VoceMateria` con `Chiave == ""` ed `Etichetta == Strings.Cronologia_TutteLeMaterie` (adattare a come oggi è rappresentata "nessun filtro" nel `FiltroCronologia`). Al filtro si passa `voce.Chiave`. **Attenzione Trappola 23**: la selezione va salvata *prima* della `Clear()` e ripristinata per `Chiave` (non per riferimento, le istanze sono nuove).

- [ ] **Step 4: Estrai le stringhe** seguendo la Procedura comune. Badge e stati (Abbandonato / Tempo scaduto / Concluso) riusano `Stato_*`. In `RisultatoCronologiaItem` la colonna materia mostra `EtichetteContenuti.Correnti.Materia(r.MateriaNome)` (con `"Quizzone"` passa invariato). In `CronologiaDettaglioView` e `DettaglioRispostaItem` la categoria mostrata passa da `EtichetteContenuti.Correnti.Categoria(Dettaglio.MateriaNome, Dettaglio.Categoria)`. Testo di domanda/risposte/spiegazione salvati: invariati (sono nella lingua giocata, accettato dalla spec). Le date formattate con `ToString("...")` a formato fisso italiano (es. `"dd/MM/yyyy HH:mm"`) diventano `ToString("g")` (formato breve della cultura corrente).

- [ ] **Step 5: Controllo di copertura + test** → `TUTTO OK`.

- [ ] **Step 6: Commit e push**

```bash
git add wsa.quiz.app wsa.quiz.core/Risorse
git commit -m "feat(l10n): cronologia e sospesi localizzati, filtro materia su chiave canonica"
git push
```

---

### Task 8: Statistiche e Gestione contenuti

**Files:**
- Modify: `wsa.quiz.app/Views/StatisticheView.axaml(.cs)`, `wsa.quiz.app/Views/CategoriaDettaglioView.axaml(.cs)`, `wsa.quiz.app/Views/GestioneContenutiView.axaml(.cs)`, `wsa.quiz.app/State/CategoriaCellaItem.cs`, `wsa.quiz.app/State/MateriaRigaItem.cs`, `wsa.quiz.app/State/MateriaListItem.cs`, `wsa.quiz.app/State/SezioneListItem.cs`, resx
- Test: `$H/app/TestApp.cs` (estensione)

**Interfaces:**
- Consumes: Task 1–7.
- Produces: chiavi `Statistiche_*`, `Gestione_*`.

- [ ] **Step 1: Estendi il test**

```csharp
        // Gestione contenuti in inglese: nessun testo italiano noto.
        var gest = new GestioneContenutiView();
        var hostG = new Window { Content = gest };
        hostG.Show(); Pompa();
        var testiG = gest.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
        Check("gestione en: niente 'Importa domande'", !testiG.Any(t => t.Contains("Importa domande")));
        hostG.Close();
```

(`GestioneContenutiView` ha un costruttore senza argomenti; i testi statici non richiedono inizializzazione. Se "Importa domande" non è il testo reale del bottone, usa il testo italiano reale.)

- [ ] **Step 2: Esegui, deve fallire.**

- [ ] **Step 3: Estrai le stringhe** seguendo la Procedura comune:
  - `CategoriaCellaItem.Etichetta` (`$"{Stat.Categoria} {Stat.PctErrore:P0}"`) → `$"{EtichetteContenuti.Correnti.Categoria(<materia>, Stat.Categoria)} {Stat.PctErrore:P0}"`, dove `<materia>` è il nome canonico della materia della riga (passarlo all'item se oggi non lo ha).
  - Il nome materia della riga heatmap → `EtichetteContenuti.Correnti.Materia(...)`.
  - `CategoriaDettaglioView` titolo (riga ~43) → `string.Format(Strings.Statistiche_TitoloDettaglio, materia, categoria, stat.Errori, stat.Visti, stat.PctErrore)` con `{4:P0}` nella risorsa, materia e categoria passate da `EtichetteContenuti`.
  - `GestioneContenutiView`: `StringFormat='{}{0} domande'` e `'{}{0} dom.'` → proprietà calcolate su `MateriaListItem`/`SezioneListItem`; `Watermark` → `PlaceholderText`; nomi di materia e sezione mostrati via `EtichetteContenuti`, mentre `NascondiSezione`/`RipristinaSezione` (righe ~93, ~102) continuano a ricevere `sez.Categoria` canonica; anteprima import (riga ~149) mostra la categoria del file importato così com'è.
  - Il `RadioButton` "Tutto"/"Ultime N" in Statistiche: solo testo, **non** reintrodurre `GroupName` (Trappola 19).

- [ ] **Step 4: Controllo di copertura + test** → `TUTTO OK`.

- [ ] **Step 5: Commit e push**

```bash
git add wsa.quiz.app wsa.quiz.core/Risorse
git commit -m "feat(l10n): statistiche e gestione contenuti localizzate"
git push
```

---

### Task 9: Cartella traduzioni nei build + materia pilota Cybersecurity

**Files:**
- Create: `traduzioni/en/cybersecurity.json`
- Modify: `wsa.quiz.app/Wsa.Quiz.App.csproj` (~riga 30), `wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj` (~riga 29)
- Test: `$H/core/TestPilota.cs`, `$H/app/TestApp.cs`

**Interfaces:**
- Consumes: overlay (Task 2), `AvaloniaResourceFonteDati`.
- Produces: file di traduzione reale; `traduzioni/` impacchettata su desktop e Android.

- [ ] **Step 1: Includi la cartella nei build**

`Wsa.Quiz.App.csproj`, accanto alla riga delle domande:

```xml
    <AvaloniaResource Include="..\traduzioni\**\*.json" Link="Assets\traduzioni\%(RecursiveDir)%(Filename)%(Extension)" />
```

`Wsa.Quiz.Desktop.csproj`, dopo il `None` delle domande:

```xml
    <None Include="..\traduzioni\**\*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <Link>traduzioni\%(RecursiveDir)%(Filename)%(Extension)</Link>
    </None>
```

La CLI **non** la include (resta in italiano).

- [ ] **Step 2: Genera lo scheletro**

Script usa-e-getta (non entra nel repo) `$H/scheletro/`: console che referenzia Core, carica la materia `cybersecurity` con `new StorageService("<repo>", Path.GetTempPath())` e scrive in `$H/scheletro/cybersecurity.it.json`:

```csharp
using System.Text.Json;
using System.Text.Encodings.Web;
using Wsa.Quiz.Core.Services;

var s = new StorageService("<repo>", Path.Combine(Path.GetTempPath(), "scheletro-utente"));
var materia = s.CaricaMaterie().Single(m => m.Id == "cybersecurity");
var domande = s.CaricaDomandeMateria(materia);
var scheletro = new
{
    nome = materia.Nome,
    categorie = domande.Select(d => d.Categoria).Distinct().ToDictionary(c => c, c => c),
    domande = domande.ToDictionary(d => d.Id, d => new { domanda = d.DomandaTesto, risposte = d.Risposte, spiegazione = d.Spiegazione }),
};
var opz = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
File.WriteAllText("cybersecurity.it.json", JsonSerializer.Serialize(scheletro, opz));
Console.WriteLine($"{domande.Count} domande, {scheletro.categorie.Count} categorie");
```

Expected: `60 domande, 11 categorie`.

- [ ] **Step 3: Traduci**

Copia lo scheletro in `traduzioni/en/cybersecurity.json` e traduci in inglese **ogni** valore (`nome` → `"Cybersecurity"`, i valori di `categorie`, `domanda`, ogni elemento di `risposte` **nello stesso ordine**, `spiegazione`). Le chiavi (Id e nomi canonici delle categorie) **non** si toccano. Terminologia tecnica standard inglese (GDPR, NIS2, DORA, XSS, VPN restano sigle). JSON salvato in UTF-8 senza escape degli accenti.

- [ ] **Step 4: Test sul file reale**

`$H/core/TestPilota.cs`:

```csharp
using Wsa.Quiz.Core.Services;

public static class TestPilota
{
    public static void Esegui()
    {
        string repo = "<repo>";
        string utente = Path.Combine(Path.GetTempPath(), "pilota-utente");
        var it = new StorageService(repo, utente);
        var m = it.CaricaMaterie().Single(x => x.Id == "cybersecurity");
        var domIt = it.CaricaDomandeMateria(m);

        var en = new StorageService(repo, utente) { LinguaContenuti = "en" };
        var domEn = en.CaricaDomandeMateria(en.CaricaMaterie().Single(x => x.Id == "cybersecurity"));

        int tradotte = domEn.Zip(domIt).Count(p => p.First.DomandaTesto != p.Second.DomandaTesto);
        Verifica.Check($"pilota: 60/60 domande tradotte ({tradotte})", tradotte == 60 && domEn.Count == 60);
        Verifica.Check("pilota: spiegazioni tradotte", domEn.Zip(domIt).All(p => p.First.Spiegazione != p.Second.Spiegazione));
        Verifica.Check("pilota: Id invariati", domEn.Select(d => d.Id).SequenceEqual(domIt.Select(d => d.Id)));
        Verifica.Check("pilota: nome materia", en.Etichette.Materia("Sicurezza Informatica") == "Cybersecurity");
        var categorie = domIt.Select(d => d.Categoria).Distinct().ToList();
        Verifica.Check("pilota: tutte le categorie tradotte",
            categorie.All(c => en.Etichette.Categoria("Sicurezza Informatica", c) != c));
    }
}
```

Aggiungi `TestPilota.Esegui();` in `$H/core/Program.cs`. Run: `cd $H/core && dotnet run` → `TUTTO OK`, **nessun** `[ATTENZIONE]` su stderr per `cybersecurity.json`.

Nel test App, nel blocco "Shell e Home in inglese" del Task 5, aggiungi:

```csharp
        Check("home en: materia pilota col nome tradotto", testi.Any(t => t == "Cybersecurity") || window.GetVisualDescendants().OfType<CheckBox>().Any(c => (c.Content as string) == "Cybersecurity"));
```

e verifica anche il caricamento da **risorse embedded** (il percorso di Android):

```csharp
        var fonteRes = new Wsa.Quiz.App.AvaloniaResourceFonteDati();
        Check("risorse embedded: traduzione pilota presente", fonteRes.Esiste("traduzioni/en/cybersecurity.json"));
```

Run il test App → `TUTTO OK`.

- [ ] **Step 5: Commit e push**

```bash
git add traduzioni wsa.quiz.app/Wsa.Quiz.App.csproj wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj
git commit -m "feat(l10n): traduzioni nei build + Cybersecurity in inglese (materia pilota)"
git push
```

---

### Task 10: Verifica finale, Android, documentazione

**Files:**
- Modify: `CLAUDE.md` (stato attuale, nuova sezione "Localizzazione", "Per ripartire", idea aperta → chiusa per la parte UI)

- [ ] **Step 1: Copertura globale**

```bash
cd <repo>
grep -rnE '(Text|Content|Header|ToolTip\.Tip|OnContent|OffContent|Title|Watermark|PlaceholderText)="[^"{]*[A-Za-zÀ-ÿ]{2,}[^"]*"' --include=*.axaml wsa.quiz.app | wc -l
```
Expected: `0` (oppure solo eccezioni della regola 4 — es. `Text="Quaestio"` — elencate nel messaggio di commit).

- [ ] **Step 2: Build di tutti i progetti**

```bash
export JAVA_HOME="$HOME/.local/jdk/jdk-17.0.20.1+1" ANDROID_HOME="$HOME/Android/Sdk"
dotnet build Wsa.Quiz.sln
dotnet build wsa.quiz.android -c Release
```
Expected: verdi, nessun warning nuovo rispetto ai 5 noti.

- [ ] **Step 3: Satellite e traduzioni nell'APK**

```bash
unzip -l wsa.quiz.android/bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk | grep -iE 'resources\.dll|cybersecurity' 
```
Expected: compare l'assembly satellite inglese di `Wsa.Quiz.Core` (in .NET Android può essere dentro un blob `assemblies*`: se `unzip -l` non lo mostra, verificare con `grep -c "Wsa.Quiz.Core.resources"` sull'output di `unzip -p ... | strings`) e la risorsa `traduzioni/en/cybersecurity.json` dentro gli asset Avalonia. Se il satellite manca, l'app su Android resterebbe in italiano: fermarsi e indagare prima di proseguire.

- [ ] **Step 4: Harness completi**

```bash
cd $H/core && dotnet run
cd $H/app && rm -rf $H/cfg && XDG_CONFIG_HOME=$H/cfg dotnet run
```
Expected: entrambi `TUTTO OK`.

- [ ] **Step 5: Prova a vista desktop** (Luca)

`dotnet run --project wsa.quiz.desktop`. Su questo PC (`en_GB`) al primo avvio senza `Lingua` l'app parte in **inglese**. Giro: Impostazioni → Italiano → torna in italiano sulla stessa sezione; una partita di Sicurezza Informatica in inglese; Statistiche mostra una sola riga per la materia.

- [ ] **Step 6: Documentazione**

In `CLAUDE.md`:
- riquadro "Stato attuale": localizzazione fatta (UI it/en + pilota Cybersecurity), data;
- nuova sezione roadmap "✅ Localizzazione (2026-10-06)" con: dove stanno le risorse, convenzione chiavi, come aggiungere una lingua (resx + `Lingue.Supportate` + `traduzioni/<codice>/`), formato `traduzioni/`, regola "nomi di materie e categorie sono chiavi canoniche, si traducono solo via `EtichetteContenuti`", primo avvio in lingua di sistema;
- "💡 Idea aperta — app in inglese": sostituire con il residuo: traduzione delle altre 9 materie (progetto separato);
- sezione "Aggiungere materie/domande": nota che modificare il testo italiano di una domanda tradotta cambia l'Id e la traduzione va riallineata (l'avviso `[ATTENZIONE] ... senza domanda corrispondente` lo segnala).

- [ ] **Step 7: Commit e push**

```bash
git add CLAUDE.md
git commit -m "docs: localizzazione it/en chiusa, come aggiungere una lingua"
git push
```
