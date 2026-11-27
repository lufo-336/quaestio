// Motore delle schede del Laboratorio.
// dotnet run --project generazione-laboratorio/motore -- <bozza.json> <id linguaggio> [--write]
// Esegue il codice della bozza, ricalcola output / atteso / effettivo, controlla i vincoli
// che si sbagliano a mano e con --write scrive laboratorio/<ling>/<id>.json.
using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Wsa.Quiz.Core.Models.Laboratorio;
using Wsa.Quiz.Core.Services;
using Wsa.Quiz.LabMotore;

var idLinguaggi = LaboratorioService.Linguaggi.Select(l => l.Id).ToList();
if (args.Length < 2 || !idLinguaggi.Contains(args[1]))
{
    Console.Error.WriteLine($"Uso: motore <bozza.json> <{string.Join("|", idLinguaggi)}> [--write]");
    return 2;
}
string ling = args[1];
bool scrivi = args.Contains("--write");

var opzioni = new JsonSerializerOptions
{
    WriteIndented = true,
    IndentSize = 2,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    // Niente "correzione": "" o "trappole": [] nelle schede base/alta.
    TypeInfoResolver = new DefaultJsonTypeInfoResolver
    {
        Modifiers =
        {
            ti =>
            {
                foreach (var p in ti.Properties)
                    p.ShouldSerialize = (_, v) => v is not (string { Length: 0 } or ICollection { Count: 0 });
            },
        },
    },
};

var scheda = JsonSerializer.Deserialize<SchedaLab>(File.ReadAllText(args[0]), opzioni)
             ?? throw new InvalidOperationException("Bozza vuota");
var errori = new List<string>();
var avvisi = new List<string>();

List<string>? Esegui(IEnumerable<string> righe, string chiamata, string dove)
{
    var (ok, righeOut, err) = EsecutoreLab.Esegui(righe, chiamata, ling);
    if (ok) return righeOut;
    errori.Add($"{dove}: non compila: {err}");
    return null;
}

foreach (var v in scheda.Varianti)
{
    var output = Esegui(v.Righe, scheda.Chiamata, $"{v.Id} output");
    if (output != null)
    {
        if (v.Output.Count > 0 && !v.Output.SequenceEqual(output))
            avvisi.Add($"{v.Id} output: la bozza diceva `{string.Join("\\n", v.Output)}`, corretto in `{string.Join("\\n", output)}`");
        v.Output = output;
    }

    if (v.Esche.Count < 2) errori.Add($"{v.Id}: servono almeno 2 esche");
    if (v.Esche.Distinct().Count() != v.Esche.Count) errori.Add($"{v.Id}: esche duplicate");
    foreach (var esca in v.Esche.Where(v.Output.Contains))
        errori.Add($"{v.Id}: l'esca `{esca}` è una riga vera dell'output");

    for (int i = 0; i < v.Input.Count; i++)
    {
        var inp = v.Input[i];
        var atteso = Esegui(scheda.CodiceSano, inp.Chiamata, $"{v.Id} input {i + 1} (codiceSano)");
        var effettivo = Esegui(v.Righe, inp.Chiamata, $"{v.Id} input {i + 1} (righe)");
        if (atteso != null) inp.Atteso = string.Join("\n", atteso);
        if (effettivo != null) inp.Effettivo = string.Join("\n", effettivo);
    }
    if (!v.Input.Any(i => i.Espone)) errori.Add($"{v.Id}: nessun input espone il difetto");
    if (!v.Input.Any(i => !i.Espone)) errori.Add($"{v.Id}: tutti gli input espongono il difetto, ne serve uno innocuo");

    // Ogni riga cambiata rispetto a codiceSano dev'essere un difetto dichiarato, o segnarla costa un falso allarme.
    var dichiarate = v.Difetti.Select(d => d.Riga).ToHashSet();
    if (v.Righe.Count == scheda.CodiceSano.Count)
    {
        var diverse = Enumerable.Range(1, v.Righe.Count)
            .Where(r => v.Righe[r - 1] != scheda.CodiceSano[r - 1]).ToHashSet();
        foreach (var r in diverse.Except(dichiarate))
            avvisi.Add($"{v.Id} riga {r}: diversa da codiceSano ma non dichiarata come difetto");
        foreach (var r in dichiarate.Except(diverse))
            errori.Add($"{v.Id} riga {r}: dichiarata come difetto ma identica a codiceSano");
    }
    else
    {
        avvisi.Add($"{v.Id}: {v.Righe.Count} righe contro {scheda.CodiceSano.Count} di codiceSano, " +
                   "controlla a mano che ogni differenza faccia parte di un difetto dichiarato");
    }

    if (scheda.Livello == LivelliLab.Media && v.Difetti.Count == 1)
    {
        var d = v.Difetti[0];
        var corrette = v.Righe.ToList();
        corrette[d.Riga - 1] = d.Correzione;
        for (int i = 0; i < v.Input.Count; i++)
        {
            var reale = Esegui(corrette, v.Input[i].Chiamata, $"{v.Id} correzione, input {i + 1}");
            if (reale != null && string.Join("\n", reale) != v.Input[i].Atteso)
                errori.Add($"{v.Id} correzione, input {i + 1}: stampa `{string.Join("\\n", reale)}` invece di `{v.Input[i].Atteso}`");
        }
        for (int t = 0; t < d.Trappole.Count; t++)
        {
            var conTrappola = v.Righe.ToList();
            conTrappola[d.Riga - 1] = d.Trappole[t];
            bool sbaglia = v.Input.Any(inp =>
            {
                var (ok, r, _) = EsecutoreLab.Esegui(conTrappola, inp.Chiamata, ling);
                return !ok || string.Join("\n", r) != inp.Atteso;
            });
            if (!sbaglia) errori.Add($"{v.Id} trappola {t + 1}: dà il risultato giusto su tutti gli input");
        }
    }

    Console.WriteLine($"== {v.Id}  output: {string.Join(" | ", v.Output)}");
    foreach (var inp in v.Input)
        Console.WriteLine($"   {(inp.Espone ? "ESPONE " : "innocuo")}  {inp.Chiamata.Replace("\n", " ; ")}  →  atteso `{inp.Atteso}`, effettivo `{inp.Effettivo}`");
}

foreach (var a in avvisi) Console.WriteLine($"AVVISO  {a}");
foreach (var e in errori) Console.WriteLine($"ERRORE  {e}");
if (errori.Count > 0) return 1;

if (!scrivi)
{
    Console.WriteLine("Dry run: nessun file scritto. Aggiungi --write per scrivere la scheda.");
    return 0;
}

var radice = new DirectoryInfo(Directory.GetCurrentDirectory());
while (radice != null && !File.Exists(Path.Combine(radice.FullName, "Wsa.Quiz.sln"))) radice = radice.Parent;
if (radice == null) throw new InvalidOperationException("Lancia il motore da dentro il repository (Wsa.Quiz.sln non trovato)");

string destinazione = Path.Combine(radice.FullName, "laboratorio", ling, scheda.Id + ".json");
Directory.CreateDirectory(Path.GetDirectoryName(destinazione)!);
File.WriteAllText(destinazione, JsonSerializer.Serialize(scheda, opzioni) + "\n");
Console.WriteLine($"Scritta {Path.GetRelativePath(radice.FullName, destinazione)}");
Console.WriteLine($"Ora: traduzioni/en/laboratorio/{ling}/{scheda.Id}.json e `dotnet test wsa.quiz.core.tests --filter Laboratorio`.");
return 0;
