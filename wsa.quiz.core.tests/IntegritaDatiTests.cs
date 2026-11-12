using System.Text.Json;
using System.Text.RegularExpressions;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.Core.Tests;

/// <summary>
/// Custodi dei dati in domande/ e traduzioni/: formato del codice coerente e
/// traduzioni agganciate alle domande giuste (l'ID dipende dal testo).
/// </summary>
public class IntegritaDatiTests
{
    private static readonly string[] Linguaggi =
        { "python", "csharp", "cpp", "c", "sql", "html", "css", "js", "bash", "json", "http" };

    private static readonly JsonSerializerOptions Opzioni = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Wsa.Quiz.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Wsa.Quiz.sln non trovato");
    }

    private sealed record MateriaJson(string Id, string Nome, string Cartella);

    /// <summary>Materia id -> domande, nello stesso ordine e con gli stessi ID di StorageService.</summary>
    private static Dictionary<string, List<Domanda>> Banchi()
    {
        string root = Radice();
        var materie = JsonSerializer.Deserialize<List<MateriaJson>>(
            File.ReadAllText(Path.Combine(root, "materie.json")), Opzioni)!;
        var risultato = new Dictionary<string, List<Domanda>>();
        foreach (var m in materie)
        {
            var domande = new List<Domanda>();
            foreach (var f in Directory.GetFiles(Path.Combine(root, m.Cartella), "*.json").OrderBy(x => x, StringComparer.Ordinal))
                domande.AddRange(JsonSerializer.Deserialize<List<Domanda>>(File.ReadAllText(f), Opzioni)!);
            var visti = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in domande)
            {
                string baseId = StorageService.GeneraId(d), id = baseId;
                for (int n = 2; !visti.Add(id); n++) id = $"{baseId}-{n}";
                d.Id = id;
            }
            risultato[m.Id] = domande;
        }
        return risultato;
    }

    private static IEnumerable<string> Testi(Domanda d) =>
        d.Risposte.Append(d.DomandaTesto).Append(d.Spiegazione ?? "");

    [Fact]
    public void NessunFenceNonChiuso()
    {
        var errati = Banchi().SelectMany(b => b.Value.Select(d => (b.Key, d)))
            .Where(x => Testi(x.d).Any(t => Regex.Matches(t, "```").Count % 2 != 0))
            .Select(x => $"{x.Key}/{x.d.Id}").ToList();
        Assert.Empty(errati);
    }

    [Fact]
    public void Python_CodiceSempreInBlocco()
    {
        var errati = Banchi()["python"]
            .Where(d => d.DomandaTesto.Contains("\n\n"))
            .Where(d => !TestoConCodice.Analizza(d.DomandaTesto)
                .Any(s => s.Tipo == TipoSegmento.Blocco && s.Linguaggio == "python"))
            .Select(d => d.Id).ToList();
        Assert.Empty(errati);
    }

    [Fact]
    public void Blocchi_HannoLinguaggio()
    {
        var errati = Banchi().SelectMany(b => b.Value.Select(d => (b.Key, d)))
            .Where(x => Testi(x.d).SelectMany(TestoConCodice.Analizza)
                .Any(s => s.Tipo == TipoSegmento.Blocco && !Linguaggi.Contains(s.Linguaggio)))
            .Select(x => $"{x.Key}/{x.d.Id}").ToList();
        Assert.Empty(errati);
    }

    [Fact]
    public void Traduzioni_NessunaChiaveOrfana()
    {
        string dirEn = Path.Combine(Radice(), "traduzioni", "en");
        var banchi = Banchi();
        var orfane = new List<string>();
        foreach (var f in Directory.GetFiles(dirEn, "*.json"))
        {
            string materia = Path.GetFileNameWithoutExtension(f);
            var t = JsonSerializer.Deserialize<TraduzioneMateria>(File.ReadAllText(f), Opzioni)!;
            var ids = banchi[materia].Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
            orfane.AddRange(t.Domande.Keys.Where(k => !ids.Contains(k)).Select(k => $"{materia}/{k}"));
        }
        Assert.Empty(orfane);
    }
}
