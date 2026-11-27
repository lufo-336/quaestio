using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Wsa.Quiz.Core.Models.Laboratorio;

namespace Wsa.Quiz.Core.Tests;

/// <summary>
/// Compila ed esegue con Roslyn ogni variante C# delle schede del Laboratorio
/// per verificare che output, valori attesi/effettivi e correzioni siano reali.
/// </summary>
public class LaboratorioEsecuzioneTests
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

    private static (bool Compilato, List<string> Righe, string? ErroreCompilazione) Esegui(
        IEnumerable<string> righe,
        string chiamata,
        string linguaggio)
    {
        if (linguaggio == "py") return EseguiPython(righe, chiamata);
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

    [Theory]
    [MemberData(nameof(LaboratorioCoerenzaTests.FileSchede), MemberType = typeof(LaboratorioCoerenzaTests))]
    public void Scheda_OutputEInputCoerentiConEsecuzione(string percorsoRelativo)
    {
        var scheda = LaboratorioCoerenzaTests.CaricaScheda(percorsoRelativo);
        string ling = percorsoRelativo.Split('/')[1];

        foreach (var v in scheda.Varianti)
        {
            var resOutput = Esegui(v.Righe, scheda.Chiamata, ling);
            Assert.True(
                resOutput.Compilato,
                $"scheda {scheda.Id} {v.Id}, output: errore di compilazione: {resOutput.ErroreCompilazione}");
            if (!resOutput.Righe.SequenceEqual(v.Output))
            {
                Assert.Fail(
                    $"scheda {scheda.Id} {v.Id}, output: la scheda dice `{string.Join("\\n", v.Output)}`, il codice stampa `{string.Join("\\n", resOutput.Righe)}`");
            }

            for (int i = 0; i < v.Input.Count; i++)
            {
                var inp = v.Input[i];
                var resSano = Esegui(scheda.CodiceSano, inp.Chiamata, ling);
                Assert.True(
                    resSano.Compilato,
                    $"scheda {scheda.Id} {v.Id}, input {i + 1} (codiceSano): errore di compilazione: {resSano.ErroreCompilazione}");
                string attesoReale = string.Join("\n", resSano.Righe);
                if (attesoReale != inp.Atteso)
                {
                    Assert.Fail(
                        $"scheda {scheda.Id} {v.Id}, input {i + 1} (atteso): la scheda dice `{inp.Atteso}`, il codice stampa `{attesoReale}`");
                }

                var resEff = Esegui(v.Righe, inp.Chiamata, ling);
                Assert.True(
                    resEff.Compilato,
                    $"scheda {scheda.Id} {v.Id}, input {i + 1} (effettivo): errore di compilazione: {resEff.ErroreCompilazione}");
                string effettivoReale = string.Join("\n", resEff.Righe);
                if (effettivoReale != inp.Effettivo)
                {
                    Assert.Fail(
                        $"scheda {scheda.Id} {v.Id}, input {i + 1} (effettivo): la scheda dice `{inp.Effettivo}`, il codice stampa `{effettivoReale}`");
                }
            }

            if (scheda.Livello == LivelliLab.Media)
            {
                var d = v.Difetti[0];
                var righeCorrette = v.Righe.ToList();
                righeCorrette[d.Riga - 1] = d.Correzione;

                for (int i = 0; i < v.Input.Count; i++)
                {
                    var inp = v.Input[i];
                    var resCorr = Esegui(righeCorrette, inp.Chiamata, ling);
                    Assert.True(
                        resCorr.Compilato,
                        $"scheda {scheda.Id} {v.Id}, correzione input {i + 1}: errore di compilazione: {resCorr.ErroreCompilazione}");
                    string reale = string.Join("\n", resCorr.Righe);
                    if (reale != inp.Atteso)
                    {
                        Assert.Fail(
                            $"scheda {scheda.Id} {v.Id}, correzione input {i + 1}: la scheda attende `{inp.Atteso}`, la correzione stampa `{reale}`");
                    }
                }

                for (int t = 0; t < d.Trappole.Count; t++)
                {
                    var righeTrappola = v.Righe.ToList();
                    righeTrappola[d.Riga - 1] = d.Trappole[t];

                    bool almenoUnFallimento = false;
                    foreach (var inp in v.Input)
                    {
                        var resTrap = Esegui(righeTrappola, inp.Chiamata, ling);
                        if (!resTrap.Compilato || string.Join("\n", resTrap.Righe) != inp.Atteso)
                        {
                            almenoUnFallimento = true;
                            break;
                        }
                    }

                    Assert.True(
                        almenoUnFallimento,
                        $"scheda {scheda.Id} {v.Id}, trappola {t + 1}: produce lo stesso risultato di codiceSano su tutti gli input");
                }
            }
        }
    }
}
