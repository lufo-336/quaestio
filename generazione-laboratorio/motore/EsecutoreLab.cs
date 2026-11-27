using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Wsa.Quiz.LabMotore;

/// <summary>
/// Esegue il codice di una scheda del Laboratorio (C# con Roslyn, Python con l'interprete di sistema).
/// Condiviso fra il motore di generazione e LaboratorioEsecuzioneTests (file collegato nel csproj dei test).
/// </summary>
public static class EsecutoreLab
{
    private static readonly ScriptOptions OpzioniScript = ScriptOptions.Default
        .AddReferences(
            typeof(object).Assembly,
            typeof(Console).Assembly,
            typeof(Enumerable).Assembly,
            typeof(StringBuilder).Assembly)
        .AddImports("System", "System.Linq", "System.Collections.Generic", "System.Text");

    // Esegue il sorgente letto da stdin: SyntaxError (anche di indentazione) = "non compila" (exit 3),
    // un'eccezione a runtime = riga "!NomeEccezione" dopo l'output già stampato, come per C#.
    private const string HarnessPython = """
        import sys
        src = sys.stdin.read()
        try:
            c = compile(src, '<scheda>', 'exec')
        except SyntaxError as e:
            sys.stderr.write('SyntaxError: ' + str(e))
            sys.exit(3)
        try:
            exec(c, {'__name__': '__main__'})
        except Exception as e:
            sys.stdout.flush()
            print('!' + type(e).__name__)
        """;

    private static (bool Compilato, List<string> Righe, string? ErroreCompilazione) EseguiPython(
        IEnumerable<string> righe,
        string chiamata)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(OperatingSystem.IsWindows() ? "py" : "python3")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
        };
        if (OperatingSystem.IsWindows()) psi.ArgumentList.Add("-3");
        foreach (var a in new[] { "-X", "utf8", "-c", HarnessPython }) psi.ArgumentList.Add(a);

        using var p = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Python non trovato (serve per le schede laboratorio/py)");
        p.StandardInput.Write(string.Join("\n", righe) + "\n" + chiamata + "\n");
        p.StandardInput.Close();
        var stderr = p.StandardError.ReadToEndAsync();
        string stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode == 3) return (false, new List<string>(), stderr.Result);
        if (p.ExitCode != 0) throw new InvalidOperationException($"Python exit {p.ExitCode}: {stderr.Result}");
        return (true, EstraiRighe(stdout), null);
    }

    /// <summary>
    /// Esegue <paramref name="righe"/> + <paramref name="chiamata"/> nel linguaggio indicato.
    /// Per un linguaggio nuovo: aggiungere qui il suo esecutore, con le stesse convenzioni
    /// (output riga per riga, "!NomeEccezione" per un'eccezione, Compilato=false per un errore di sintassi).
    /// </summary>
    public static (bool Compilato, List<string> Righe, string? ErroreCompilazione) Esegui(
        IEnumerable<string> righe,
        string chiamata,
        string linguaggio) => linguaggio switch
    {
        "cs" => EseguiCSharp(righe, chiamata),
        "py" => EseguiPython(righe, chiamata),
        _ => throw new NotSupportedException($"Nessun esecutore per il linguaggio '{linguaggio}' (vedi EsecutoreLab.Esegui)"),
    };

    private static (bool Compilato, List<string> Righe, string? ErroreCompilazione) EseguiCSharp(
        IEnumerable<string> righe,
        string chiamata)
    {
        string codice = string.Join("\n", righe) + "\n" + chiamata;
        var script = CSharpScript.Create(codice, OpzioniScript);
        var errori = script.Compile()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        if (errori.Count > 0)
            return (false, new List<string>(), string.Join("; ", errori.Select(e => e.GetMessage())));

        var vecchioOut = Console.Out;
        var vecchiaCultura = CultureInfo.CurrentCulture;
        // Il FormatProvider di Console.WriteLine è quello del writer, fissato alla creazione:
        // senza InvariantCulture qui, su un PC italiano 7.5 diventa "7,5".
        using var sw = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        try
        {
            Console.SetOut(sw);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            script.RunAsync().GetAwaiter().GetResult();
            return (true, EstraiRighe(sw.ToString()), null);
        }
        catch (Exception ex)
        {
            var righeStampate = EstraiRighe(sw.ToString());
            righeStampate.Add($"!{ex.GetType().Name}");
            return (true, righeStampate, null);
        }
        finally
        {
            Console.SetOut(vecchioOut);
            CultureInfo.CurrentCulture = vecchiaCultura;
        }
    }

    private static List<string> EstraiRighe(string testo)
    {
        if (string.IsNullOrEmpty(testo)) return new List<string>();
        string normalizzato = testo.Replace("\r\n", "\n");
        if (normalizzato.EndsWith('\n'))
            normalizzato = normalizzato[..^1];
        return normalizzato.Split('\n').ToList();
    }
}
