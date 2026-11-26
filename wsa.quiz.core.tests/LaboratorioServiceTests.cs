using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Models.Laboratorio;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.Core.Tests;

public class LaboratorioServiceTests
{
    private static SchedaLab CreaScheda(string id, string livello = LivelliLab.Base, int numVarianti = 2)
    {
        var varianti = Enumerable.Range(1, numVarianti)
            .Select(i => new VarianteLab { Id = $"v{i}" })
            .ToList();
        return new SchedaLab
        {
            Id = id,
            Titolo = id,
            Livello = livello,
            Linguaggio = "cs",
            Varianti = varianti
        };
    }

    private static RisultatoQuiz CreaSessione(
        DateTime data,
        string schedaId,
        string varianteId,
        params (string Passo, bool Corretta, bool SecondoTentativo)[] passi)
    {
        return new RisultatoQuiz
        {
            DataOra = data,
            Modalita = LaboratorioService.Modalita,
            Dettagli = passi.Select(p => new DettaglioRisposta
            {
                IdDomanda = $"lab:cs:{schedaId}:{varianteId}:{p.Passo}",
                Corretta = p.Corretta,
                SecondoTentativo = p.SecondoTentativo,
                Tentativi = 1
            }).ToList()
        };
    }

    [Fact]
    public void IdPasso_EHelperIdentificazione()
    {
        var s = CreaScheda("media-voti");
        var v = s.Varianti[0];

        string id = LaboratorioService.IdPasso(s, v, "output");
        Assert.Equal("lab:cs:media-voti:v1:output", id);

        Assert.True(LaboratorioService.EDettaglioLab(new DettaglioRisposta { IdDomanda = id }));
        Assert.False(LaboratorioService.EDettaglioLab(new DettaglioRisposta { IdDomanda = "abc123def456" }));

        Assert.True(LaboratorioService.ERisultatoLab(new RisultatoQuiz { Modalita = LaboratorioService.Modalita }));
        Assert.False(LaboratorioService.ERisultatoLab(new RisultatoQuiz { Modalita = "Completo" }));
    }

    [Fact]
    public void Prepara_CronologiaVuota_RestituisceSoloVariantiNuove()
    {
        var schede = new[]
        {
            CreaScheda("s1"),
            CreaScheda("s2"),
            CreaScheda("s3"),
            CreaScheda("s4", LivelliLab.Media)
        };

        var scelte = LaboratorioService.Prepara(
            schede, LivelliLab.Base, ripassa: false, quante: 10,
            Array.Empty<RisultatoQuiz>(), new Random(42));

        Assert.Equal(3, scelte.Count);
        Assert.All(scelte, x => Assert.Equal(LivelliLab.Base, x.Scheda.Livello));
        Assert.Equal(3, scelte.Select(x => x.Scheda.Id).Distinct().Count());
    }

    [Fact]
    public void Prepara_EsauriteVarianti_PrendeLaMenoRecenteEPrimaLeSchedeConNovita()
    {
        var s1 = CreaScheda("s1", numVarianti: 2);
        var s2 = CreaScheda("s2", numVarianti: 2);
        var t0 = new DateTime(2026, 11, 1, 10, 0, 0);

        // s1: viste entrambe (v1 più vecchia di v2)
        // s2: vista solo v1 (v2 mai vista)
        var cronologia = new[]
        {
            CreaSessione(t0, "s1", "v1", ("output", true, false)),
            CreaSessione(t0.AddHours(1), "s1", "v2", ("output", true, false)),
            CreaSessione(t0.AddHours(2), "s2", "v1", ("output", true, false)),
        };

        var scelte = LaboratorioService.Prepara(
            new[] { s1, s2 }, LivelliLab.Base, ripassa: false, quante: 5,
            cronologia, new Random(7));

        Assert.Equal(2, scelte.Count);
        // Prima s2 (ha una variante mai vista: v2), poi s1 (tutte viste: prende v1 che è la meno recente)
        Assert.Equal(("s2", "v2"), (scelte[0].Scheda.Id, scelte[0].Variante.Id));
        Assert.Equal(("s1", "v1"), (scelte[1].Scheda.Id, scelte[1].Variante.Id));
    }

    [Fact]
    public void Prepara_Ripassa_PrendeSoloUltimiTentativiSbagliatiDallaMenoRecente()
    {
        var s1 = CreaScheda("s1", numVarianti: 2);
        var s2 = CreaScheda("s2", numVarianti: 2);
        var s3 = CreaScheda("s3", numVarianti: 2);
        var t0 = new DateTime(2026, 11, 1, 10, 0, 0);

        var cronologia = new[]
        {
            // s1/v1: sbagliata, poi rifatta giusta -> non deve comparire
            CreaSessione(t0, "s1", "v1", ("output", false, false)),
            CreaSessione(t0.AddHours(5), "s1", "v1", ("output", true, false), ("bug", true, false), ("input", true, false)),
            // s2/v1: sbagliata al passo bug (più recente di s3/v2)
            CreaSessione(t0.AddHours(3), "s2", "v1", ("output", true, false), ("bug", false, false), ("input", true, false)),
            // s3/v2: giusta al secondo tentativo (PesoErrore = 0.5 > 0) e più vecchia di s2/v1
            CreaSessione(t0.AddHours(1), "s3", "v2", ("output", true, false), ("bug", true, true), ("input", true, false)),
        };

        var scelte = LaboratorioService.Prepara(
            new[] { s1, s2, s3 }, LivelliLab.Base, ripassa: true, quante: 10,
            cronologia, new Random(1));

        Assert.Equal(2, scelte.Count);
        Assert.Equal(("s3", "v2"), (scelte[0].Scheda.Id, scelte[0].Variante.Id));
        Assert.Equal(("s2", "v1"), (scelte[1].Scheda.Id, scelte[1].Variante.Id));
    }

    [Fact]
    public void Prepara_MaiDueVariantiDellaStessaScheda_ERispettaQuante()
    {
        var s1 = CreaScheda("s1", numVarianti: 2);
        var s2 = CreaScheda("s2", numVarianti: 2);
        var s3 = CreaScheda("s3", numVarianti: 2);
        var t0 = new DateTime(2026, 11, 1, 10, 0, 0);

        // s1 ha ENTRAMBE le varianti sbagliate: in ripassa ne compare comunque una sola (la meno recente, v1).
        var cronologia = new[]
        {
            CreaSessione(t0, "s1", "v1", ("output", false, false)),
            CreaSessione(t0.AddHours(1), "s1", "v2", ("output", false, false)),
            CreaSessione(t0.AddHours(2), "s2", "v1", ("bug", false, false)),
            CreaSessione(t0.AddHours(3), "s3", "v1", ("input", false, false)),
        };

        var scelteRipassa = LaboratorioService.Prepara(
            new[] { s1, s2, s3 }, LivelliLab.Base, ripassa: true, quante: 2,
            cronologia, new Random(1));

        Assert.Equal(2, scelteRipassa.Count);
        Assert.Equal(2, scelteRipassa.Select(x => x.Scheda.Id).Distinct().Count());
        Assert.Equal(("s1", "v1"), (scelteRipassa[0].Scheda.Id, scelteRipassa[0].Variante.Id));
        Assert.Equal(("s2", "v1"), (scelteRipassa[1].Scheda.Id, scelteRipassa[1].Variante.Id));

        var scelteNormali = LaboratorioService.Prepara(
            new[] { s1, s2, s3 }, LivelliLab.Base, ripassa: false, quante: 2,
            Array.Empty<RisultatoQuiz>(), new Random(1));
        Assert.Equal(2, scelteNormali.Count);
        Assert.Equal(2, scelteNormali.Select(x => x.Scheda.Id).Distinct().Count());
    }

    [Fact]
    public void Conteggi_CalcolaNuoveTotaleDaRipassare()
    {
        var s1 = CreaScheda("s1", numVarianti: 2);
        var s2 = CreaScheda("s2", numVarianti: 2);
        var t0 = new DateTime(2026, 11, 1, 10, 0, 0);

        var cronologia = new[]
        {
            CreaSessione(t0, "s1", "v1", ("output", false, false)),
            CreaSessione(t0.AddHours(1), "s1", "v2", ("output", true, false)),
        };

        var (nuove, totale, daRipassare) = LaboratorioService.Conteggi(
            new[] { s1, s2 }, LivelliLab.Base, cronologia);

        Assert.Equal(1, nuove);       // s2 ha varianti mai viste; s1 no
        Assert.Equal(2, totale);
        Assert.Equal(1, daRipassare); // s1/v1 sbagliata
    }

    private const string SchedaEsempioJson = """
        {
          "id": "somma-due",
          "titolo": "Titolo IT",
          "livello": "base",
          "requisito": "Requisito IT",
          "chiamata": "Console.WriteLine(Somma(2, 3));",
          "codiceSano": [ "int Somma(int a, int b) => a + b;" ],
          "varianti": [
            {
              "id": "v1",
              "righe": [ "int Somma(int a, int b) => a - b;" ],
              "output": [ "-1" ],
              "esche": [ "5", "6" ],
              "difetti": [
                {
                  "riga": 1,
                  "tipo": "OperandoSbagliato",
                  "indizio": "Indizio v1 IT",
                  "spiegazione": "Spiegazione v1 IT",
                  "correzione": "int Somma(int a, int b) => a + b;",
                  "trappole": [ "int Somma(int a, int b) => a * b;", "int Somma(int a, int b) => b - a;" ]
                }
              ],
              "input": [
                { "chiamata": "Console.WriteLine(Somma(1, 2));", "atteso": "3", "effettivo": "-1" },
                { "chiamata": "Console.WriteLine(Somma(5, 0));", "atteso": "5", "effettivo": "5" },
                { "chiamata": "Console.WriteLine(Somma(0, 0));", "atteso": "0", "effettivo": "0" }
              ]
            },
            {
              "id": "v2",
              "righe": [ "int Somma(int a, int b) => a + 1;" ],
              "output": [ "3" ],
              "esche": [ "5", "4" ],
              "difetti": [
                {
                  "riga": 1,
                  "tipo": "OperandoSbagliato",
                  "indizio": "Indizio v2 IT",
                  "spiegazione": "Spiegazione v2 IT"
                }
              ],
              "input": [
                { "chiamata": "Console.WriteLine(Somma(1, 2));", "atteso": "3", "effettivo": "2" },
                { "chiamata": "Console.WriteLine(Somma(5, 1));", "atteso": "6", "effettivo": "6" },
                { "chiamata": "Console.WriteLine(Somma(0, 1));", "atteso": "1", "effettivo": "1" }
              ]
            }
          ]
        }
        """;

    private static string CreaDirTemporaneaConScheda(string? pathTraduzioneRelativo = null, string? jsonTraduzione = null)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"quaestio-lab-test-{Guid.NewGuid():N}");
        string dirLab = Path.Combine(dir, "laboratorio", "cs");
        Directory.CreateDirectory(dirLab);
        File.WriteAllText(Path.Combine(dirLab, "somma-due.json"), SchedaEsempioJson);

        if (pathTraduzioneRelativo != null && jsonTraduzione != null)
        {
            string fullTrad = Path.Combine(dir, pathTraduzioneRelativo);
            Directory.CreateDirectory(Path.GetDirectoryName(fullTrad)!);
            File.WriteAllText(fullTrad, jsonTraduzione);
        }
        return dir;
    }

    [Fact]
    public void CaricaSchede_LinguaEn_SovrapponeSoloIQuattroTesti()
    {
        const string tradJson = """
            {
              "titolo": "Sum of two",
              "requisito": "Requirement EN",
              "varianti": {
                "v1": {
                  "difetti": [
                    { "indizio": "Hint v1 EN", "spiegazione": "Explanation v1 EN" }
                  ]
                },
                "v2": {
                  "difetti": [
                    { "indizio": "Hint v2 EN", "spiegazione": "Explanation v2 EN" }
                  ]
                }
              }
            }
            """;

        string dir = CreaDirTemporaneaConScheda(
            Path.Combine("traduzioni", "en", "laboratorio", "cs", "somma-due.json"),
            tradJson);
        try
        {
            var schede = LaboratorioService.CaricaSchede(new FileSystemFonteDati(dir), "en");
            var s = Assert.Single(schede);

            Assert.Equal("Sum of two", s.Titolo);
            Assert.Equal("Requirement EN", s.Requisito);
            Assert.Equal("Hint v1 EN", s.Varianti[0].Difetti[0].Indizio);
            Assert.Equal("Explanation v1 EN", s.Varianti[0].Difetti[0].Spiegazione);
            Assert.Equal("Hint v2 EN", s.Varianti[1].Difetti[0].Indizio);
            Assert.Equal("Explanation v2 EN", s.Varianti[1].Difetti[0].Spiegazione);

            // Tutto il resto resta identico all'originale italiano
            Assert.Equal("somma-due", s.Id);
            Assert.Equal("base", s.Livello);
            Assert.Equal("cs", s.Linguaggio);
            Assert.Equal("Console.WriteLine(Somma(2, 3));", s.Chiamata);
            Assert.Equal(new[] { "int Somma(int a, int b) => a + b;" }, s.CodiceSano);

            Assert.Equal(new[] { "int Somma(int a, int b) => a - b;" }, s.Varianti[0].Righe);
            Assert.Equal(new[] { "-1" }, s.Varianti[0].Output);
            Assert.Equal(new[] { "5", "6" }, s.Varianti[0].Esche);
            Assert.Equal(1, s.Varianti[0].Difetti[0].Riga);
            Assert.Equal("OperandoSbagliato", s.Varianti[0].Difetti[0].Tipo);
            Assert.Equal("int Somma(int a, int b) => a + b;", s.Varianti[0].Difetti[0].Correzione);
            Assert.Equal(2, s.Varianti[0].Difetti[0].Trappole.Count);
            Assert.Equal("Console.WriteLine(Somma(1, 2));", s.Varianti[0].Input[0].Chiamata);
            Assert.Equal("3", s.Varianti[0].Input[0].Atteso);
            Assert.Equal("-1", s.Varianti[0].Input[0].Effettivo);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CaricaSchede_NumeroDifettiDiverso_VarianteRestaInItaliano()
    {
        const string tradJson = """
            {
              "titolo": "Sum of two",
              "requisito": "Requirement EN",
              "varianti": {
                "v1": {
                  "difetti": [
                    { "indizio": "Hint v1 EN", "spiegazione": "Explanation v1 EN" },
                    { "indizio": "Extra", "spiegazione": "Extra" }
                  ]
                },
                "v2": {
                  "difetti": [
                    { "indizio": "Hint v2 EN", "spiegazione": "Explanation v2 EN" }
                  ]
                }
              }
            }
            """;

        string dir = CreaDirTemporaneaConScheda(
            Path.Combine("traduzioni", "en", "laboratorio", "cs", "somma-due.json"),
            tradJson);
        try
        {
            var schede = LaboratorioService.CaricaSchede(new FileSystemFonteDati(dir), "en");
            var s = Assert.Single(schede);

            Assert.Equal("Sum of two", s.Titolo);
            Assert.Equal("Requirement EN", s.Requisito);
            // v1 ha numero di difetti diverso -> resta in italiano
            Assert.Equal("Indizio v1 IT", s.Varianti[0].Difetti[0].Indizio);
            Assert.Equal("Spiegazione v1 IT", s.Varianti[0].Difetti[0].Spiegazione);
            // v2 ha numero di difetti corretto -> tradotta
            Assert.Equal("Hint v2 EN", s.Varianti[1].Difetti[0].Indizio);
            Assert.Equal("Explanation v2 EN", s.Varianti[1].Difetti[0].Spiegazione);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CaricaSchede_LinguaBase_NonLeggeTraduzioni()
    {
        const string tradJson = """
            {
              "titolo": "Non dovrebbe essere letto",
              "requisito": "Nemmeno questo",
              "varianti": {
                "v1": {
                  "difetti": [
                    { "indizio": "Indizio sovrascritto", "spiegazione": "Spiegazione sovrascritta" }
                  ]
                }
              }
            }
            """;

        string dir = CreaDirTemporaneaConScheda(
            Path.Combine("traduzioni", Lingue.Base, "laboratorio", "cs", "somma-due.json"),
            tradJson);
        try
        {
            var schede = LaboratorioService.CaricaSchede(new FileSystemFonteDati(dir), Lingue.Base);
            var s = Assert.Single(schede);

            Assert.Equal("Titolo IT", s.Titolo);
            Assert.Equal("Requisito IT", s.Requisito);
            Assert.Equal("Indizio v1 IT", s.Varianti[0].Difetti[0].Indizio);
            Assert.Equal("Spiegazione v1 IT", s.Varianti[0].Difetti[0].Spiegazione);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
