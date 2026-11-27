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

    [Fact]
    public void OutputCorretto_ConfrontaRigheEOrdine()
    {
        var v = new VarianteLab { Output = new List<string> { "7", "False" } };

        Assert.True(LaboratorioService.OutputCorretto(v, new[] { "7", "False" }));
        Assert.False(LaboratorioService.OutputCorretto(v, new[] { "False", "7" }));
        Assert.False(LaboratorioService.OutputCorretto(v, new[] { "7" }));
        Assert.False(LaboratorioService.OutputCorretto(v, new[] { "7", "False", "0" }));
        Assert.False(LaboratorioService.OutputCorretto(v, Array.Empty<string>()));
        Assert.False(LaboratorioService.OutputCorretto(null, new[] { "7", "False" }));
    }

    [Fact]
    public void BugCorretto_VerificaRigaTipoECorrezione()
    {
        var v = new VarianteLab
        {
            Difetti = new List<DifettoLab>
            {
                new()
                {
                    Riga = 5,
                    Tipo = TipiDifetto.DivisioneIntera,
                    Correzione = "double m = (double)somma / voti.Length;",
                    Trappole = new List<string>
                    {
                        "double m = (double)(somma / voti.Length);",
                        "double m = somma / (voti.Length - 1);"
                    }
                }
            }
        };

        Assert.True(LaboratorioService.BugRigaCorretta(v, 5));
        Assert.False(LaboratorioService.BugRigaCorretta(v, 4));

        Assert.True(LaboratorioService.BugTipoCorretto(v, 5, TipiDifetto.DivisioneIntera));
        Assert.False(LaboratorioService.BugTipoCorretto(v, 4, TipiDifetto.DivisioneIntera));
        Assert.False(LaboratorioService.BugTipoCorretto(v, 5, TipiDifetto.Confine));

        Assert.True(LaboratorioService.BugCorrezioneCorretta(v, 5, "double m = (double)somma / voti.Length;"));
        Assert.False(LaboratorioService.BugCorrezioneCorretta(v, 4, "double m = (double)somma / voti.Length;"));
        Assert.False(LaboratorioService.BugCorrezioneCorretta(v, 5, v.Difetti[0].Trappole[0]));
    }

    [Fact]
    public void InputCorretto_VerificaEsponeEIndice()
    {
        var v = new VarianteLab
        {
            Input = new List<InputLab>
            {
                new() { Chiamata = "F(0);", Atteso = "0", Effettivo = "0" },
                new() { Chiamata = "F(1, 2);", Atteso = "1.5", Effettivo = "1" }
            }
        };

        Assert.False(LaboratorioService.InputCorretto(v, 0));
        Assert.True(LaboratorioService.InputCorretto(v, 1));
        Assert.False(LaboratorioService.InputCorretto(v, -1));
        Assert.False(LaboratorioService.InputCorretto(v, 2));
    }

    [Fact]
    public void SecondoTentativo_PesoMezzoInStatisticheEPunteggio()
    {
        var s = CreaScheda("media-voti", LivelliLab.Media, numVarianti: 1);
        var v = s.Varianti[0];

        var risultato = new RisultatoQuiz
        {
            DataOra = new DateTime(2026, 11, 26, 12, 0, 0),
            Modalita = LaboratorioService.Modalita,
            MateriaNome = "C#" + LaboratorioService.SuffissoMateria,
            Dettagli = new List<DettaglioRisposta>
            {
                new()
                {
                    IdDomanda = LaboratorioService.IdPasso(s, v, "output"),
                    MateriaNome = "C#" + LaboratorioService.SuffissoMateria,
                    Categoria = TipiDifetto.EtichettaCanonica(TipiDifetto.DivisioneIntera),
                    Corretta = true,
                    Tentativi = 1,
                    SecondoTentativo = false
                },
                new()
                {
                    IdDomanda = LaboratorioService.IdPasso(s, v, "bug"),
                    MateriaNome = "C#" + LaboratorioService.SuffissoMateria,
                    Categoria = TipiDifetto.EtichettaCanonica(TipiDifetto.DivisioneIntera),
                    Corretta = true,
                    Tentativi = 1,
                    SecondoTentativo = true
                },
                new()
                {
                    IdDomanda = LaboratorioService.IdPasso(s, v, "input"),
                    MateriaNome = "C#" + LaboratorioService.SuffissoMateria,
                    Categoria = TipiDifetto.EtichettaCanonica(TipiDifetto.DivisioneIntera),
                    Corretta = false,
                    Tentativi = 1,
                    SecondoTentativo = false
                }
            }
        };

        Assert.Equal(0.0, risultato.Dettagli[0].PesoErrore());
        Assert.Equal(0.5, risultato.Dettagli[1].PesoErrore());
        Assert.Equal(1.0, risultato.Dettagli[2].PesoErrore());
        Assert.Equal(17, QuizService.CalcolaPunteggio(risultato));

        var stat = new StatisticheService().Calcola(new[] { risultato });
        var mat = Assert.Single(stat);
        var cat = Assert.Single(mat.Categorie);
        Assert.Equal(3, cat.Visti);
        Assert.Equal(1.5, cat.Errori, 3);
        Assert.Equal(0.5, cat.PctErrore, 3);
    }

    private static VarianteLab CreaVarianteReview(params (int Riga, string Tipo)[] difetti) => new()
    {
        Difetti = difetti.Select(d => new DifettoLab { Riga = d.Riga, Tipo = d.Tipo }).ToList()
    };

    [Fact]
    public void PunteggioReview_TuttiDifettiColTipoGiusto_ValeUno()
    {
        var v = CreaVarianteReview(
            (4, TipiDifetto.Overflow),
            (9, TipiDifetto.DivisioneIntera),
            (15, TipiDifetto.NullVuoto));

        var segni = new Dictionary<int, string>
        {
            [4] = TipiDifetto.Overflow,
            [9] = TipiDifetto.DivisioneIntera,
            [15] = TipiDifetto.NullVuoto
        };

        Assert.Equal(1.0, LaboratorioService.PunteggioReview(v, segni), 6);
        Assert.Equal((true, false), LaboratorioService.EsitoReview(1.0));
    }

    [Fact]
    public void PunteggioReview_SegnareOgniRiga_ValeZero()
    {
        var v = CreaVarianteReview(
            (3, TipiDifetto.Overflow),
            (5, TipiDifetto.DivisioneIntera));

        var segni = new Dictionary<int, string>();
        for (int riga = 1; riga <= 10; riga++)
            segni[riga] = TipiDifetto.Overflow;

        Assert.Equal(0.0, LaboratorioService.PunteggioReview(v, segni), 6);
        Assert.Equal((false, false), LaboratorioService.EsitoReview(0.0));
    }

    [Fact]
    public void PunteggioReview_TipoSbagliato_ValeMezzoPerDifetto()
    {
        var v = CreaVarianteReview(
            (3, TipiDifetto.Overflow),
            (7, TipiDifetto.DivisioneIntera));

        // Entrambi sulla riga giusta, uno con tipo giusto (+1) e uno con tipo sbagliato (+0.5) => 1.5 / 2 = 0.75
        var unoSbagliato = new Dictionary<int, string>
        {
            [3] = TipiDifetto.Overflow,
            [7] = TipiDifetto.Confine
        };
        Assert.Equal(0.75, LaboratorioService.PunteggioReview(v, unoSbagliato), 6);

        // Entrambi sulla riga giusta ma tipo sbagliato => (0.5 + 0.5) / 2 = 0.5
        var entrambiSbagliati = new Dictionary<int, string>
        {
            [3] = TipiDifetto.Confine,
            [7] = TipiDifetto.Confine
        };
        Assert.Equal(0.5, LaboratorioService.PunteggioReview(v, entrambiSbagliati), 6);
    }

    [Fact]
    public void PunteggioReview_FalsoAllarmeToglie()
    {
        var v = CreaVarianteReview(
            (3, TipiDifetto.Overflow),
            (7, TipiDifetto.DivisioneIntera));

        // 2 giusti (+2) - 1 falso allarme (-1) = 1 / 2 = 0.5
        var conUnFalso = new Dictionary<int, string>
        {
            [3] = TipiDifetto.Overflow,
            [7] = TipiDifetto.DivisioneIntera,
            [12] = TipiDifetto.NullVuoto
        };
        Assert.Equal(0.5, LaboratorioService.PunteggioReview(v, conUnFalso), 6);

        // 2 giusti (+2) - 2 falsi allarmi (-2) = 0 / 2 = 0.0
        var conDueFalsi = new Dictionary<int, string>
        {
            [3] = TipiDifetto.Overflow,
            [7] = TipiDifetto.DivisioneIntera,
            [12] = TipiDifetto.NullVuoto,
            [18] = TipiDifetto.Confine
        };
        Assert.Equal(0.0, LaboratorioService.PunteggioReview(v, conDueFalsi), 6);
    }

    [Fact]
    public void EsitoReview_SogliaMezzoPerSecondoTentativo()
    {
        Assert.Equal((true, false), LaboratorioService.EsitoReview(1.0));
        Assert.Equal((true, true), LaboratorioService.EsitoReview(0.75));
        Assert.Equal((true, true), LaboratorioService.EsitoReview(0.5));
        Assert.Equal((false, false), LaboratorioService.EsitoReview(0.4999));
        Assert.Equal((false, false), LaboratorioService.EsitoReview(0.25));
        Assert.Equal((false, false), LaboratorioService.EsitoReview(0.0));

        var v = CreaVarianteReview(
            (3, TipiDifetto.Overflow),
            (7, TipiDifetto.DivisioneIntera),
            (11, TipiDifetto.NullVuoto));

        // 1 giusto (+1) + 1 tipo sbagliato (+0.5) + 1 mancato (0) => 1.5 / 3 = 0.5 -> SecondoTentativo (peso 0.5)
        double pSoglia = LaboratorioService.PunteggioReview(v, new Dictionary<int, string>
        {
            [3] = TipiDifetto.Overflow,
            [7] = TipiDifetto.Confine
        });
        Assert.Equal(0.5, pSoglia, 6);

        var (corretta, secondo) = LaboratorioService.EsitoReview(pSoglia);
        var dettaglio = new DettaglioRisposta { Corretta = corretta, Tentativi = 1, SecondoTentativo = secondo };
        Assert.True(dettaglio.Corretta);
        Assert.True(dettaglio.SecondoTentativo);
        Assert.Equal(0.5, dettaglio.PesoErrore());

        // Sotto soglia: 1 tipo sbagliato (+0.5) su 2 difetti => 0.25 -> sbagliato (peso 1.0)
        var v2 = CreaVarianteReview((3, TipiDifetto.Overflow), (7, TipiDifetto.DivisioneIntera));
        double pSotto = LaboratorioService.PunteggioReview(v2, new Dictionary<int, string> { [3] = TipiDifetto.Confine });
        Assert.Equal(0.25, pSotto, 6);
        var (correttaSotto, secondoSotto) = LaboratorioService.EsitoReview(pSotto);
        var dettaglioSotto = new DettaglioRisposta { Corretta = correttaSotto, Tentativi = 1, SecondoTentativo = secondoSotto };
        Assert.False(dettaglioSotto.Corretta);
        Assert.False(dettaglioSotto.SecondoTentativo);
        Assert.Equal(1.0, dettaglioSotto.PesoErrore());
    }

    [Fact]
    public void PauseLab_AndataERitornoSuCartellaTemporanea()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"quaestio-pausa-lab-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var storage = new StorageService(dir, dir);
            Assert.Empty(storage.CaricaPauseLab());

            var pausa = new PausaLaboratorio
            {
                SessioneId = "abc123",
                DataOraPausa = new DateTime(2026, 11, 27, 10, 30, 0),
                Linguaggio = "cs",
                Livello = LivelliLab.Media,
                Coppie = new List<string> { "sconto-ordine/v1", "verifica-accesso/v2", "verifica-buffer/v1" },
                IndiceScheda = 1,
                TempoTrascorso = TimeSpan.FromSeconds(95),
                Dettagli = new List<DettaglioRisposta>
                {
                    new()
                    {
                        IdDomanda = "lab:cs:sconto-ordine:v1:output",
                        MateriaNome = "C#" + LaboratorioService.SuffissoMateria,
                        Categoria = TipiDifetto.EtichettaCanonica(TipiDifetto.Confine),
                        TestoDomanda = "Sconto ordine — Prevedi l'output",
                        RispostaData = "100",
                        RispostaCorretta = "100",
                        Corretta = true,
                        Spiegazione = "Spiegazione",
                        Tentativi = 1
                    }
                }
            };

            storage.SalvaPausaLab(pausa);
            Assert.True(File.Exists(Path.Combine(dir, "laboratorio_in_pausa.json")));
            Assert.False(File.Exists(Path.Combine(dir, "quiz_in_pausa.json")));

            var ricaricate = new StorageService(dir, dir).CaricaPauseLab();
            var caricata = Assert.Single(ricaricate);
            Assert.Equal(pausa.SessioneId, caricata.SessioneId);
            Assert.Equal(pausa.DataOraPausa, caricata.DataOraPausa);
            Assert.Equal(pausa.Linguaggio, caricata.Linguaggio);
            Assert.Equal(pausa.Livello, caricata.Livello);
            Assert.Equal(pausa.Coppie, caricata.Coppie);
            Assert.Equal(pausa.IndiceScheda, caricata.IndiceScheda);
            Assert.Equal(pausa.TempoTrascorso, caricata.TempoTrascorso);
            var dett = Assert.Single(caricata.Dettagli);
            Assert.Equal("lab:cs:sconto-ordine:v1:output", dett.IdDomanda);
            Assert.True(dett.Corretta);

            // Aggiornamento sullo stesso SessioneId sostituisce senza duplicare
            pausa.IndiceScheda = 2;
            storage.SalvaPausaLab(pausa);
            var aggiornate = storage.CaricaPauseLab();
            Assert.Equal(2, Assert.Single(aggiornate).IndiceScheda);

            // Eliminazione rimuove la pausa e il file se vuoto
            storage.EliminaPausaLab(pausa.SessioneId);
            Assert.Empty(storage.CaricaPauseLab());
            Assert.False(File.Exists(Path.Combine(dir, "laboratorio_in_pausa.json")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PauseLab_NonModificaQuizInPausaEsistente()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"quaestio-pausa-mix-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var storage = new StorageService(dir, dir);
            var pausaQuiz = new SessionePausa
            {
                SessioneId = "quiz-1",
                DataOraPausa = new DateTime(2026, 11, 27, 9, 0, 0),
                Opzioni = new OpzioniQuiz { NomeModalita = "C# — Completo", MateriaNomeLabel = "C#" },
                CodaDomandeIds = new List<string> { "d1", "d2" },
                TempoTrascorso = TimeSpan.FromSeconds(42)
            };
            storage.SalvaPausa(pausaQuiz);

            string fileQuiz = Path.Combine(dir, "quiz_in_pausa.json");
            string contenutoPrima = File.ReadAllText(fileQuiz);

            var pausaLab = new PausaLaboratorio
            {
                SessioneId = "lab-1",
                Linguaggio = "cs",
                Livello = LivelliLab.Base,
                Coppie = new List<string> { "media-voti/v1" },
                IndiceScheda = 0
            };

            storage.SalvaPausaLab(pausaLab);
            Assert.Equal(contenutoPrima, File.ReadAllText(fileQuiz));

            storage.EliminaPausaLab("lab-1");
            Assert.Equal(contenutoPrima, File.ReadAllText(fileQuiz));

            var quizRicaricate = storage.CaricaPause();
            Assert.Equal("quiz-1", Assert.Single(quizRicaricate).SessioneId);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CaricaPauseLab_FileCorrotto_RestituisceListaVuota()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"quaestio-pausa-corrotta-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "laboratorio_in_pausa.json"), "{ json non valido [");
            var storage = new StorageService(dir, dir);

            var pause = storage.CaricaPauseLab();
            Assert.Empty(pause);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CoppieDaId_IgnoraSpariteEDeduplicaPerScheda()
    {
        var s1 = CreaScheda("s1", LivelliLab.Base, numVarianti: 2);
        var s2 = CreaScheda("s2", LivelliLab.Media, numVarianti: 2);
        var schede = new[] { s1, s2 };

        var idSbagliati = new[]
        {
            "lab:cs:sparita:v1:output",     // scheda sparita -> ignorata
            "lab:cs:s1:v99:bug",            // variante sparita -> ignorata
            "non-valido",                   // formato non lab -> ignorato
            "lab:cs:s1:v2:output",          // prima coppia valida per s1
            "lab:cs:s1:v2:bug",             // stesso passo/variante di s1 -> deduplicato
            "lab:cs:s1:v1:input",           // altra variante della stessa scheda s1 -> deduplicato
            "lab:cs:s2:v1:bug",             // prima coppia valida per s2
        };

        var coppie = LaboratorioService.CoppieDaId(schede, idSbagliati);

        Assert.Equal(2, coppie.Count);
        Assert.Equal(("s1", "v2"), (coppie[0].Scheda.Id, coppie[0].Variante.Id));
        Assert.Equal(("s2", "v1"), (coppie[1].Scheda.Id, coppie[1].Variante.Id));

        Assert.Empty(LaboratorioService.CoppieDaId(schede, new[] { "lab:cs:sparita:v1:output" }));
        Assert.Empty(LaboratorioService.CoppieDaId(schede, Array.Empty<string>()));
    }
}
