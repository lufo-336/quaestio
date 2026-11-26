using System.Text.Json;
using Wsa.Quiz.Core.Models.Laboratorio;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.Core.Tests;

/// <summary>
/// Verifica la coerenza strutturale delle schede del Laboratorio in <c>laboratorio/*/*.json</c>.
/// </summary>
public class LaboratorioCoerenzaTests
{
    internal static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Wsa.Quiz.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Wsa.Quiz.sln non trovato");
    }

    public static TheoryData<string> FileSchede()
    {
        var data = new TheoryData<string>();
        string dirLab = Path.Combine(Radice(), "laboratorio");
        if (!Directory.Exists(dirLab)) return data;

        string root = Radice();
        foreach (var file in Directory.GetFiles(dirLab, "*.json", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
        }
        return data;
    }

    internal static SchedaLab CaricaScheda(string percorsoRelativo)
    {
        string path = Path.Combine(Radice(), percorsoRelativo);
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SchedaLab>(json, StorageService.OpzioniLettura)
               ?? throw new InvalidOperationException($"Scheda nulla in {percorsoRelativo}");
    }

    [Fact]
    public void IdSchede_UniciNelLinguaggio()
    {
        string dirLab = Path.Combine(Radice(), "laboratorio");
        Assert.True(Directory.Exists(dirLab), "Cartella laboratorio/ mancante.");

        var duplicati = new List<string>();
        foreach (var dirLing in Directory.GetDirectories(dirLab).OrderBy(d => d, StringComparer.Ordinal))
        {
            string ling = Path.GetFileName(dirLing);
            var visti = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(dirLing, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                var scheda = JsonSerializer.Deserialize<SchedaLab>(
                    File.ReadAllText(file), StorageService.OpzioniLettura);
                Assert.NotNull(scheda);
                Assert.False(string.IsNullOrWhiteSpace(scheda.Id), $"{ling}/{Path.GetFileName(file)}: id vuoto");
                if (!visti.Add(scheda.Id))
                    duplicati.Add($"{ling}/{scheda.Id}");
            }
        }

        Assert.Empty(duplicati);
    }

    [Theory]
    [MemberData(nameof(FileSchede))]
    public void Scheda_ECoerente(string percorsoRelativo)
    {
        var scheda = CaricaScheda(percorsoRelativo);

        Assert.False(string.IsNullOrWhiteSpace(scheda.Id), $"{percorsoRelativo}: id vuoto");
        Assert.False(scheda.Id.Contains(':'), $"{percorsoRelativo}: id '{scheda.Id}' non può contenere ':'");
        Assert.False(string.IsNullOrWhiteSpace(scheda.Titolo), $"{scheda.Id}: titolo vuoto");
        Assert.False(string.IsNullOrWhiteSpace(scheda.Requisito), $"{scheda.Id}: requisito vuoto");
        Assert.False(string.IsNullOrWhiteSpace(scheda.Chiamata), $"{scheda.Id}: chiamata vuota");
        Assert.NotEmpty(scheda.CodiceSano);
        Assert.Contains(scheda.Livello, LivelliLab.Tutti);

        Assert.InRange(scheda.Varianti.Count, 2, 4);

        var righeSaneTrim = scheda.CodiceSano.Select(r => r.Trim()).ToHashSet(StringComparer.Ordinal);
        var idVarianti = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in scheda.Varianti)
        {
            Assert.False(string.IsNullOrWhiteSpace(v.Id), $"{scheda.Id}: variante con id vuoto");
            Assert.False(v.Id.Contains(':'), $"{scheda.Id}: id variante '{v.Id}' non può contenere ':'");
            Assert.True(idVarianti.Add(v.Id), $"{scheda.Id}: variante duplicata '{v.Id}'");
            Assert.NotEmpty(v.Righe);
            Assert.NotEmpty(v.Output);

            Assert.True(v.Esche.Count >= 2, $"{scheda.Id} {v.Id}: servono almeno 2 esche");
            var escheViste = new HashSet<string>(StringComparer.Ordinal);
            foreach (var esca in v.Esche)
            {
                Assert.False(string.IsNullOrWhiteSpace(esca), $"{scheda.Id} {v.Id}: esca vuota");
                Assert.DoesNotContain(esca, v.Output);
                Assert.True(escheViste.Add(esca), $"{scheda.Id} {v.Id}: esca duplicata '{esca}'");
            }

            if (scheda.Livello is LivelliLab.Base or LivelliLab.Media)
                Assert.True(v.Difetti.Count == 1, $"{scheda.Id} {v.Id}: livello {scheda.Livello} richiede esattamente 1 difetto");
            else
                Assert.InRange(v.Difetti.Count, 2, 3);

            var righeDifetti = new HashSet<int>();
            foreach (var d in v.Difetti)
            {
                Assert.InRange(d.Riga, 1, v.Righe.Count);
                Assert.True(
                    righeDifetti.Add(d.Riga),
                    $"{scheda.Id} {v.Id} riga {d.Riga}: più difetti sulla stessa riga");

                string rigaDifetto = v.Righe[d.Riga - 1].Trim();
                Assert.False(
                    string.IsNullOrWhiteSpace(rigaDifetto) || rigaDifetto is "{" or "}",
                    $"{scheda.Id} {v.Id} riga {d.Riga}: la riga del difetto non può essere vuota o una sola parentesi ('{rigaDifetto}')");
                Assert.False(
                    righeSaneTrim.Contains(rigaDifetto),
                    $"{scheda.Id} {v.Id} riga {d.Riga}: la riga '{rigaDifetto}' compare identica in codiceSano");

                Assert.True(TipiDifetto.Valido(d.Tipo), $"{scheda.Id} {v.Id}: tipo difetto sconosciuto '{d.Tipo}'");
                Assert.False(string.IsNullOrWhiteSpace(d.Indizio), $"{scheda.Id} {v.Id}: indizio vuoto");
                Assert.False(string.IsNullOrWhiteSpace(d.Spiegazione), $"{scheda.Id} {v.Id}: spiegazione vuota");

                if (scheda.Livello == LivelliLab.Media)
                {
                    string correzione = d.Correzione.Trim();
                    Assert.False(string.IsNullOrWhiteSpace(correzione), $"{scheda.Id} {v.Id}: correzione mancante");
                    Assert.NotEqual(rigaDifetto, correzione);
                    Assert.True(d.Trappole.Count == 2, $"{scheda.Id} {v.Id}: servono esattamente 2 trappole");

                    string t1 = d.Trappole[0].Trim();
                    string t2 = d.Trappole[1].Trim();
                    Assert.False(string.IsNullOrWhiteSpace(t1), $"{scheda.Id} {v.Id}: trappola 1 vuota");
                    Assert.False(string.IsNullOrWhiteSpace(t2), $"{scheda.Id} {v.Id}: trappola 2 vuota");
                    Assert.NotEqual(t1, t2);
                    Assert.NotEqual(correzione, t1);
                    Assert.NotEqual(correzione, t2);
                    Assert.NotEqual(rigaDifetto, t1);
                    Assert.NotEqual(rigaDifetto, t2);
                }
            }

            Assert.InRange(v.Input.Count, 3, 4);
            var chiamateInputViste = new HashSet<string>(StringComparer.Ordinal);
            foreach (var inp in v.Input)
            {
                Assert.False(string.IsNullOrWhiteSpace(inp.Chiamata), $"{scheda.Id} {v.Id}: chiamata input vuota");
                Assert.True(
                    chiamateInputViste.Add(inp.Chiamata.Trim()),
                    $"{scheda.Id} {v.Id}: chiamata input duplicata '{inp.Chiamata.Trim()}'");
            }

            Assert.True(v.Input.Any(i => i.Espone), $"{scheda.Id} {v.Id}: serve almeno un input che espone il difetto");
            Assert.True(v.Input.Any(i => !i.Espone), $"{scheda.Id} {v.Id}: serve almeno un input che non espone il difetto");
        }
    }

    [Theory]
    [MemberData(nameof(FileSchede))]
    public void Scheda_HaTraduzioneEnCoerente(string percorsoRelativo)
    {
        var scheda = CaricaScheda(percorsoRelativo);
        string[] parti = percorsoRelativo.Split('/');
        string ling = parti[1];

        string pathTrad = Path.Combine(Radice(), "traduzioni", "en", "laboratorio", ling, $"{scheda.Id}.json");
        Assert.True(File.Exists(pathTrad), $"Traduzione inglese mancante: traduzioni/en/laboratorio/{ling}/{scheda.Id}.json");

        var trad = JsonSerializer.Deserialize<TraduzioneScheda>(
            File.ReadAllText(pathTrad), StorageService.OpzioniLettura);
        Assert.NotNull(trad);
        Assert.False(string.IsNullOrWhiteSpace(trad.Titolo), $"{scheda.Id} (en): titolo vuoto");
        Assert.False(string.IsNullOrWhiteSpace(trad.Requisito), $"{scheda.Id} (en): requisito vuoto");
        Assert.NotNull(trad.Varianti);

        var attese = scheda.Varianti.Select(v => v.Id).ToHashSet(StringComparer.Ordinal);
        var trovate = trad.Varianti.Keys.ToHashSet(StringComparer.Ordinal);
        Assert.True(
            attese.SetEquals(trovate),
            $"{scheda.Id} (en): varianti non corrispondenti (attese: [{string.Join(", ", attese)}], trovate: [{string.Join(", ", trovate)}])");

        foreach (var v in scheda.Varianti)
        {
            var tv = trad.Varianti[v.Id];
            Assert.NotNull(tv);
            Assert.NotNull(tv.Difetti);
            Assert.Equal(v.Difetti.Count, tv.Difetti.Count);

            for (int i = 0; i < v.Difetti.Count; i++)
            {
                var td = tv.Difetti[i];
                Assert.NotNull(td);
                Assert.False(string.IsNullOrWhiteSpace(td.Indizio), $"{scheda.Id} {v.Id} difetto {i + 1} (en): indizio vuoto");
                Assert.False(string.IsNullOrWhiteSpace(td.Spiegazione), $"{scheda.Id} {v.Id} difetto {i + 1} (en): spiegazione vuota");
            }
        }
    }

    [Fact]
    public void TraduzioniEnLab_NessunFileSenzaScheda()
    {
        string dirTradLab = Path.Combine(Radice(), "traduzioni", "en", "laboratorio");
        Assert.True(Directory.Exists(dirTradLab), "Cartella traduzioni/en/laboratorio/ mancante.");

        string dirLab = Path.Combine(Radice(), "laboratorio");
        var schedePerLing = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        if (Directory.Exists(dirLab))
        {
            foreach (var dirLing in Directory.GetDirectories(dirLab))
            {
                string ling = Path.GetFileName(dirLing);
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var file in Directory.GetFiles(dirLing, "*.json"))
                {
                    var s = JsonSerializer.Deserialize<SchedaLab>(
                        File.ReadAllText(file), StorageService.OpzioniLettura);
                    if (s != null && !string.IsNullOrWhiteSpace(s.Id))
                        ids.Add(s.Id);
                }
                schedePerLing[ling] = ids;
            }
        }

        var orfane = new List<string>();
        foreach (var dirLing in Directory.GetDirectories(dirTradLab).OrderBy(d => d, StringComparer.Ordinal))
        {
            string ling = Path.GetFileName(dirLing);
            schedePerLing.TryGetValue(ling, out var ids);
            foreach (var file in Directory.GetFiles(dirLing, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                string id = Path.GetFileNameWithoutExtension(file);
                if (ids == null || !ids.Contains(id))
                    orfane.Add($"{ling}/{id}.json");
            }
        }

        Assert.Empty(orfane);
    }
}
