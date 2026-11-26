using System.Text.Json.Serialization;

namespace Wsa.Quiz.Core.Models.Laboratorio;

/// <summary>
/// Livelli di difficoltà ammessi per una <see cref="SchedaLab"/>.
/// </summary>
public static class LivelliLab
{
    public const string Base = "base";
    public const string Media = "media";
    public const string Alta = "alta";

    /// <summary>Tutti i livelli ammessi, in ordine crescente di difficoltà.</summary>
    public static readonly string[] Tutti = { Base, Media, Alta };
}

/// <summary>
/// Scheda del Laboratorio di codice: un requisito, una chiamata d'esempio,
/// il codice sano di riferimento (usato solo dai test) e 2–4 varianti difettose.
/// </summary>
public class SchedaLab
{
    /// <summary>Identificativo kebab-case, unico nel linguaggio (es. "media-voti").</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Titolo breve mostrato in testa alla scheda.</summary>
    [JsonPropertyName("titolo")]
    public string Titolo { get; set; } = string.Empty;

    /// <summary>Livello di difficoltà: "base", "media" o "alta" (vedi <see cref="LivelliLab"/>).</summary>
    [JsonPropertyName("livello")]
    public string Livello { get; set; } = string.Empty;

    /// <summary>Descrizione di cosa deve fare il codice (1–3 frasi).</summary>
    [JsonPropertyName("requisito")]
    public string Requisito { get; set; } = string.Empty;

    /// <summary>
    /// Istruzioni C# che invocano il codice e stampano con <c>Console.WriteLine</c>
    /// (chiamata d'esempio usata nel passo 1 "Prevedi l'output").
    /// </summary>
    [JsonPropertyName("chiamata")]
    public string Chiamata { get; set; } = string.Empty;

    /// <summary>
    /// Codice C# corretto, una riga per elemento. Non mostrato all'utente:
    /// serve al test di esecuzione per verificare gli output attesi.
    /// Scritto come script C# (funzioni a livello superiore, niente <c>class Program</c>/<c>Main</c>),
    /// con using impliciti <c>System</c>, <c>System.Linq</c>, <c>System.Collections.Generic</c>, <c>System.Text</c>.
    /// </summary>
    [JsonPropertyName("codiceSano")]
    public List<string> CodiceSano { get; set; } = new();

    /// <summary>Varianti difettose della scheda (2–4).</summary>
    [JsonPropertyName("varianti")]
    public List<VarianteLab> Varianti { get; set; } = new();

    /// <summary>
    /// Identificativo del linguaggio (es. "cs"). Non presente nel JSON:
    /// valorizzato da <see cref="Services.LaboratorioService.CaricaSchede"/> in base alla cartella.
    /// </summary>
    [JsonIgnore]
    public string Linguaggio { get; set; } = string.Empty;
}

/// <summary>
/// Singola variante difettosa di una <see cref="SchedaLab"/>.
/// </summary>
public class VarianteLab
{
    /// <summary>Identificativo della variante nella scheda (es. "v1", "v2").</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Codice C# con il difetto, una riga per elemento (è il codice mostrato all'utente).
    /// Scritto come script C# (funzioni a livello superiore, niente <c>class Program</c>/<c>Main</c>),
    /// con using impliciti <c>System</c>, <c>System.Linq</c>, <c>System.Collections.Generic</c>, <c>System.Text</c>.
    /// </summary>
    [JsonPropertyName("righe")]
    public List<string> Righe { get; set; } = new();

    /// <summary>
    /// Righe stampate da <see cref="Righe"/> + <see cref="SchedaLab.Chiamata"/>.
    /// Convenzione eccezioni: se il codice lancia un'eccezione non gestita,
    /// l'output è ciò che è stato stampato fino a quel punto seguito da una riga
    /// <c>!NomeEccezione</c> (es. <c>!DivideByZeroException</c>), sia qui sia in
    /// <see cref="InputLab.Effettivo"/> / <see cref="InputLab.Atteso"/>.
    /// Cultura: l'esecuzione avviene con <see cref="System.Globalization.CultureInfo.InvariantCulture"/>
    /// impostata sul thread (decimali con il punto, identici su ogni macchina).
    /// </summary>
    [JsonPropertyName("output")]
    public List<string> Output { get; set; } = new();

    /// <summary>Righe plausibili ma false usate come distrattori nel menu o nelle tessere.</summary>
    [JsonPropertyName("esche")]
    public List<string> Esche { get; set; } = new();

    /// <summary>
    /// Difetti presenti in <see cref="Righe"/>, elencati dal più grave al meno grave
    /// (il primo fornisce la categoria canonica del dettaglio in cronologia).
    /// </summary>
    [JsonPropertyName("difetti")]
    public List<DifettoLab> Difetti { get; set; } = new();

    /// <summary>Input candidati per il passo 3 "Rompi la funzione" (3–4).</summary>
    [JsonPropertyName("input")]
    public List<InputLab> Input { get; set; } = new();
}

/// <summary>
/// Singolo difetto presente in una <see cref="VarianteLab"/>.
/// </summary>
public class DifettoLab
{
    /// <summary>Numero di riga 1-based in <see cref="VarianteLab.Righe"/>.</summary>
    [JsonPropertyName("riga")]
    public int Riga { get; set; }

    /// <summary>Identificativo del tipo di difetto (vedi <see cref="TipiDifetto"/>).</summary>
    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Indizio per la seconda possibilità: indica dove guardare senza svelare il difetto.</summary>
    [JsonPropertyName("indizio")]
    public string Indizio { get; set; } = string.Empty;

    /// <summary>Spiegazione mostrata dopo la risposta e salvata in cronologia.</summary>
    [JsonPropertyName("spiegazione")]
    public string Spiegazione { get; set; } = string.Empty;

    /// <summary>Riga corretta che sostituisce <see cref="Riga"/> (obbligatoria a livello media).</summary>
    [JsonPropertyName("correzione")]
    public string Correzione { get; set; } = string.Empty;

    /// <summary>Due righe sbagliate ma plausibili usate come distrattori a livello media.</summary>
    [JsonPropertyName("trappole")]
    public List<string> Trappole { get; set; } = new();
}

/// <summary>
/// Caso di test candidato per il passo 3 "Rompi la funzione".
/// </summary>
public class InputLab
{
    /// <summary>Istruzioni C# con i valori di test che invocano il codice e stampano l'esito.</summary>
    [JsonPropertyName("chiamata")]
    public string Chiamata { get; set; } = string.Empty;

    /// <summary>Output atteso del codice sano (righe separate da <c>\n</c>, convenzione eccezioni invariata).</summary>
    [JsonPropertyName("atteso")]
    public string Atteso { get; set; } = string.Empty;

    /// <summary>Output effettivo della variante difettosa (righe separate da <c>\n</c>, convenzione eccezioni invariata).</summary>
    [JsonPropertyName("effettivo")]
    public string Effettivo { get; set; } = string.Empty;

    /// <summary>True se questo input espone un difetto (l'output effettivo differisce da quello atteso).</summary>
    [JsonIgnore]
    public bool Espone => Atteso != Effettivo;
}
