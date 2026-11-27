using Wsa.Quiz.Core.Models.Laboratorio;

namespace Wsa.Quiz.Core.Tests;

/// <summary>
/// Esegue ogni variante delle schede del Laboratorio (C# con Roslyn, Python con l'interprete)
/// per verificare che output, valori attesi/effettivi e correzioni siano reali.
/// L'esecutore è in generazione-laboratorio/motore/EsecutoreLab.cs, condiviso col motore.
/// </summary>
public class LaboratorioEsecuzioneTests
{
    private static (bool Compilato, List<string> Righe, string? ErroreCompilazione) Esegui(
        IEnumerable<string> righe, string chiamata, string linguaggio) =>
        Wsa.Quiz.LabMotore.EsecutoreLab.Esegui(righe, chiamata, linguaggio);

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
