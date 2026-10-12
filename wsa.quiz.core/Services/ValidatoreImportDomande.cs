using System.Text.Json;
using System.Linq;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Risorse;

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
            errori.Add(string.Format(Strings.Import_JsonNonValido, ex.Message));
            return (new List<Domanda>(), errori);
        }

        if (domande == null || domande.Count == 0)
        {
            errori.Add(Strings.Import_NessunaDomanda);
            return (new List<Domanda>(), errori);
        }

        for (int i = 0; i < domande.Count; i++)
        {
            var d = domande[i];
            int n = i + 1;
            if (string.IsNullOrWhiteSpace(d.DomandaTesto))
                errori.Add(string.Format(Strings.Import_DomandaMancante, n));
            if (d.Risposte == null || d.Risposte.Count < 2)
                errori.Add(string.Format(Strings.Import_AlmenoDueRisposte, n));
            else if (d.RispostaCorretta < 0 || d.RispostaCorretta >= d.Risposte.Count)
                errori.Add(string.Format(Strings.Import_CorrettaFuoriRange, n));
            if (d.Risposte != null && d.Risposte.Distinct().Count() != d.Risposte.Count)
                errori.Add(string.Format(Strings.Import_RisposteDuplicate, n));
            if (string.IsNullOrWhiteSpace(d.Spiegazione))
                errori.Add(string.Format(Strings.Import_SpiegazioneMancante, n));
        }

        var testi = domande.Select(d => d.DomandaTesto).ToList();
        for (int i = 0; i < testi.Count; i++)
        {
            if (testi.IndexOf(testi[i]) != i)
                errori.Add(string.Format(Strings.Import_DomandaDuplicata, i + 1, Tronca(testi[i])));
        }

        return errori.Count > 0 ? (new List<Domanda>(), errori) : (domande, errori);
    }

    private static string Tronca(string s) => s.Length <= 40 ? s : s[..40] + "...";
}
