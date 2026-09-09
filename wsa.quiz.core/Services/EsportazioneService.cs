using System.Globalization;
using System.Text;
using System.Text.Json;
using Wsa.Quiz.Core.Models;

namespace Wsa.Quiz.Core.Services;

/// <summary>
/// Serializza la cronologia in CSV o JSON (step 11). Puro: restituisce una
/// stringa e non tocca il disco — chi chiama decide dove scriverla.
/// </summary>
public static class EsportazioneService
{
    /// <summary>
    /// Separatore di colonna. Punto e virgola e non virgola: e' quello che si
    /// aspetta Excel con impostazioni italiane, dove la virgola e' il separatore
    /// decimale.
    /// </summary>
    private const char Separatore = ';';

    private static readonly string[] Intestazione =
    [
        "Data", "Modalita", "Materia", "Materie", "Totale", "Corrette", "Errate",
        "Percentuale", "Durata", "DurataSecondi", "Stato", "Punteggio",
        "Rotazione", "Cronometro",
    ];

    private static readonly JsonSerializerOptions OpzioniJson = new() { WriteIndented = true };

    // ------------------------------------------------------------------ CSV

    public static string VersoCsv(IEnumerable<RisultatoQuiz> cronologia)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(Separatore, Intestazione)).Append("\r\n");

        foreach (var r in cronologia)
        {
            string[] campi =
            [
                r.DataOra.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                r.Modalita,
                r.MateriaNome,
                string.Join(", ", MaterieCoinvolte(r)),
                r.TotaleDomande.ToString(CultureInfo.InvariantCulture),
                r.RisposteCorrette.ToString(CultureInfo.InvariantCulture),
                r.RisposteErrate.ToString(CultureInfo.InvariantCulture),
                Decimale(r.PercentualeCorrette),
                QuizService.FormattaDurata(r.DurataQuiz),
                ((int)r.DurataQuiz.TotalSeconds).ToString(CultureInfo.InvariantCulture),
                Stato(r),
                r.Punteggio.ToString(CultureInfo.InvariantCulture),
                SiNo(r.ModalitaRotazione),
                SiNo(r.CronometroAttivo),
            ];

            sb.Append(string.Join(Separatore, campi.Select(Quota))).Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Quoting RFC4180: si quota solo se serve (separatore, virgolette, a capo);
    /// le virgolette interne si raddoppiano.
    /// </summary>
    private static string Quota(string campo)
    {
        if (campo.IndexOf(Separatore) < 0
            && !campo.Contains('"')
            && !campo.Contains('\n')
            && !campo.Contains('\r'))
            return campo;

        return $"\"{campo.Replace("\"", "\"\"")}\"";
    }

    /// <summary>Virgola decimale, senza zeri inutili: 90 -> "90", 55,5 -> "55,5".</summary>
    private static string Decimale(double v)
        => v.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');

    // ------------------------------------------------------------------ JSON

    public static string VersoJson(IEnumerable<RisultatoQuiz> cronologia)
    {
        var righe = cronologia.Select(r => new
        {
            r.Id,
            Data = r.DataOra.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            r.Modalita,
            Materia = r.MateriaNome,
            Materie = MaterieCoinvolte(r),
            Totale = r.TotaleDomande,
            Corrette = r.RisposteCorrette,
            Errate = r.RisposteErrate,
            Percentuale = r.PercentualeCorrette,
            Durata = QuizService.FormattaDurata(r.DurataQuiz),
            DurataSecondi = (int)r.DurataQuiz.TotalSeconds,
            Stato = Stato(r),
            r.Punteggio,
            Rotazione = r.ModalitaRotazione,
            Cronometro = r.CronometroAttivo,
        });

        return JsonSerializer.Serialize(righe, OpzioniJson);
    }

    // ------------------------------------------------------------------ COMUNI

    /// <summary>
    /// Materie realmente toccate dalla partita, nell'ordine di comparsa. Per il
    /// Quizzone <see cref="RisultatoQuiz.MateriaNome"/> vale "Quizzone", quindi le
    /// materie vere si leggono dai dettagli; la label resta il fallback per i
    /// record che non ne hanno (vedi <see cref="FiltroCronologia"/>).
    /// </summary>
    private static List<string> MaterieCoinvolte(RisultatoQuiz r)
    {
        if (r.Dettagli.Count == 0)
            return string.IsNullOrWhiteSpace(r.MateriaNome) ? [] : [r.MateriaNome];

        return r.Dettagli
            .Select(d => d.MateriaNome)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Stesso ordine di precedenza dell'etichetta mostrata in cronologia.</summary>
    private static string Stato(RisultatoQuiz r)
        => r.Abbandonato ? "Abbandonato"
         : r.TempoScaduto ? "Tempo scaduto"
         : "Concluso";

    private static string SiNo(bool v) => v ? "Si" : "No";
}
