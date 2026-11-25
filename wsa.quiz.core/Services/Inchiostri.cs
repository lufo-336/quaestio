using System.Globalization;

namespace Wsa.Quiz.Core.Services;

/// <summary>
/// Definisce uno dei 16 inchiostri della tavolozza del brand ("lucafoglia"):
/// hex base, eventuale variante per sfondo chiaro (grigio), colore del testo
/// sopra, disponibilita' come colore utente e variante scura per testo esito
/// su fondo chiaro.
/// </summary>
public sealed record Inchiostro(
    string Id,
    string NomeRisorsa,
    string Hex,
    string? HexChiaro,
    bool TestoNero,
    bool Sceglibile,
    string? HexTestoSuChiaro);

/// <summary>
/// Tavolozza dei 16 inchiostri, migrazione degli id colore legacy e calcolo
/// del contrasto WCAG 2.x (luminanza relativa sRGB).
/// </summary>
public static class Inchiostri
{
    public const string Predefinito = "blu";

    public static IReadOnlyList<Inchiostro> Tutti { get; } =
    [
        // HexTestoSuChiaro per i tre esiti riservati contro fondo chiaro #e8e7e3
        // (L ≈ 0.7985 -> L_testo <= ~0.1386 per contrasto >= 4.5:1):
        // - rosso:      #d6414b -> #b83740 (L ≈ 0.1329, contrasto ≈ 4.64:1)
        // - ocra:       #9b7300 -> #836100 (L ≈ 0.1337, contrasto ≈ 4.62:1)
        // - verdeacqua: #33867c -> #2b7168 (L ≈ 0.1332, contrasto ≈ 4.63:1)
        new("rosso",      "Colore_Rosso",      "#d6414b", null,      true,  false, "#b83740"),
        new("terracotta", "Colore_Terracotta", "#e0703a", null,      true,  true,  null),
        new("cacao",      "Colore_Cacao",      "#8f5f3d", null,      false, true,  null),
        new("ocra",       "Colore_Ocra",       "#ebaf00", null,      true,  false, "#836100"),
        new("oliva",      "Colore_Oliva",      "#95a63f", null,      true,  true,  null),
        new("prato",      "Colore_Prato",      "#3fa66a", null,      true,  true,  null),
        new("verdeacqua", "Colore_VerdeAcqua", "#40a79b", null,      true,  false, "#2b7168"),
        new("petrolio",   "Colore_Petrolio",   "#308ea6", null,      true,  true,  null),
        new("azzurro",    "Colore_Azzurro",    "#539fd4", null,      true,  true,  null),
        new("blu",        "Colore_Blu",        "#3a6cb8", null,      false, true,  null),
        new("indaco",     "Colore_Indaco",     "#4d4dbc", null,      false, true,  null),
        new("viola",      "Colore_Viola",      "#8559b0", null,      false, true,  null),
        new("prugna",     "Colore_Prugna",     "#a63f8c", null,      false, true,  null),
        new("rosaantico", "Colore_RosaAntico", "#d06a95", null,      true,  true,  null),
        new("ardesia",    "Colore_Ardesia",    "#5c6370", null,      false, true,  null),
        new("grigio",     "Colore_Grigio",     "#d7d7d7", "#c4c3bd", true,  false, null),
    ];

    private static readonly Inchiostro _predefinito =
        Tutti.First(i => i.Id == Predefinito);

    private static readonly Dictionary<string, string> _migrazioniLegacy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blunotte"] = "indaco",
        ["magenta"] = "prugna",
        ["verdeacqua"] = "petrolio",
        ["grafite"] = "ardesia",
    };

    /// <summary>
    /// Restituisce l'inchiostro corrispondente all'id (case-insensitive),
    /// oppure l'inchiostro predefinito ("blu") se assente o sconosciuto.
    /// </summary>
    public static Inchiostro Trova(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            foreach (var inchiostro in Tutti)
            {
                if (string.Equals(inchiostro.Id, id, StringComparison.OrdinalIgnoreCase))
                    return inchiostro;
            }
        }
        return _predefinito;
    }

    /// <summary>
    /// Mappa un vecchio id di ColoreAccento o un id sconosciuto/non sceglibile
    /// verso il corrispondente id sceglibile della nuova tavolozza.
    /// </summary>
    public static string MigraIdColore(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Predefinito;

        if (_migrazioniLegacy.TryGetValue(id, out var migrato))
            return migrato;

        foreach (var inchiostro in Tutti)
        {
            if (inchiostro.Sceglibile && string.Equals(inchiostro.Id, id, StringComparison.OrdinalIgnoreCase))
                return inchiostro.Id;
        }

        return Predefinito;
    }

    /// <summary>
    /// Rapporto di contrasto WCAG 2.x tra due colori esadecimali sRGB
    /// (es. "#RRGGBB" o "#RGB"), compreso tra 1 e 21.
    /// </summary>
    public static double Contrasto(string hexA, string hexB)
    {
        double lumA = LuminanzaRelativa(hexA);
        double lumB = LuminanzaRelativa(hexB);
        double piuChiaro = Math.Max(lumA, lumB);
        double piuScuro = Math.Min(lumA, lumB);
        return (piuChiaro + 0.05) / (piuScuro + 0.05);
    }

    private static double LuminanzaRelativa(string hex)
    {
        var (r, g, b) = AnalizzaHex(hex);
        return 0.2126 * CanaleLineare(r) + 0.7152 * CanaleLineare(g) + 0.0722 * CanaleLineare(b);
    }

    private static double CanaleLineare(byte canale)
    {
        double c = canale / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static (byte r, byte g, byte b) AnalizzaHex(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);
        ReadOnlySpan<char> s = hex.AsSpan().Trim();
        if (s.Length > 0 && s[0] == '#')
            s = s[1..];

        if (s.Length == 3)
        {
            byte r3 = byte.Parse(s[0..1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g3 = byte.Parse(s[1..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b3 = byte.Parse(s[2..3], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ((byte)(r3 * 17), (byte)(g3 * 17), (byte)(b3 * 17));
        }

        if (s.Length == 6)
        {
            byte r = byte.Parse(s[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(s[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(s[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (r, g, b);
        }

        throw new FormatException($"Formato hex non valido: '{hex}'.");
    }
}
