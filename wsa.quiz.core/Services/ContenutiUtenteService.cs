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
    private readonly string _fileSezioniEscluse;

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
        _fileSezioniEscluse = Path.Combine(_cartellaContenuti, "sezioni-escluse.json");
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

    /// <summary>
    /// Crea una nuova materia utente. <paramref name="idEsistenti"/> deve includere
    /// gli id di TUTTE le materie note al chiamante (built-in + già importate), non
    /// solo quelle utente: evita che lo slug generato collida con l'id di una
    /// materia built-in (es. utente crea "SQL" -> stesso id "sql" del built-in).
    /// </summary>
    public Materia CreaMateria(string nome, List<Domanda> domande, IEnumerable<string> idEsistenti)
    {
        var materie = CaricaMaterieUtente();
        var tuttiGliId = materie.Select(m => m.Id).Concat(idEsistenti);
        string id = GeneraIdMateria(nome, tuttiGliId);
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

    // ---------------------------------------------------------------- SEZIONI ESCLUSE

    /// <summary>
    /// Sezioni (categorie) nascoste dall'utente. Vale sia per built-in (che
    /// restano nell'APK ma vengono filtrate al caricamento) sia per importate.
    /// </summary>
    public List<SezioneEsclusa> CaricaSezioniEscluse()
    {
        if (!File.Exists(_fileSezioniEscluse)) return new List<SezioneEsclusa>();
        string json = StorageService.LeggiFileCondiviso(_fileSezioniEscluse);
        return JsonSerializer.Deserialize<List<SezioneEsclusa>>(json, OpzioniLettura) ?? new List<SezioneEsclusa>();
    }

    /// <summary>Nasconde una sezione. No-op se già nascosta (idempotente).</summary>
    public void NascondiSezione(string materiaId, string categoria)
    {
        var lista = CaricaSezioniEscluse();
        if (lista.Any(s => s.MateriaId == materiaId && s.Categoria == categoria)) return;
        lista.Add(new SezioneEsclusa { MateriaId = materiaId, Categoria = categoria });
        ScriviSezioniEscluse(lista);
    }

    /// <summary>Ripristina una sezione nascosta. No-op se non era nascosta.</summary>
    public void RipristinaSezione(string materiaId, string categoria)
    {
        var lista = CaricaSezioniEscluse();
        int rimosse = lista.RemoveAll(s => s.MateriaId == materiaId && s.Categoria == categoria);
        if (rimosse == 0) return;
        ScriviSezioniEscluse(lista);
    }

    private void ScriviSezioniEscluse(List<SezioneEsclusa> lista)
    {
        string json = JsonSerializer.Serialize(lista, OpzioniScrittura);
        StorageService.ScriviAtomico(_fileSezioniEscluse, json);
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
