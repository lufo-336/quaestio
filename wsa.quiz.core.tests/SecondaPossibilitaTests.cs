using System.Text.Json;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.Core.Tests;

public class SecondaPossibilitaTests
{
    [Fact]
    public void PesoErrore_QuattroCombinazioni()
    {
        // 1. Corretto primo colpo = 0
        var d1 = new DettaglioRisposta { Corretta = true, Tentativi = 1, SecondoTentativo = false };
        Assert.Equal(0.0, d1.PesoErrore());

        // 2. Corretto con SecondoTentativo = 0.5
        var d2 = new DettaglioRisposta { Corretta = true, Tentativi = 1, SecondoTentativo = true };
        Assert.Equal(0.5, d2.PesoErrore());

        // 3. Sbagliato = 1
        var d3 = new DettaglioRisposta { Corretta = false, Tentativi = 1, SecondoTentativo = false };
        Assert.Equal(1.0, d3.PesoErrore());
        var d3b = new DettaglioRisposta { Corretta = false, Tentativi = 1, SecondoTentativo = true };
        Assert.Equal(1.0, d3b.PesoErrore());

        // 4. Corretto con Tentativi 2 (rotazione) = 1
        var d4 = new DettaglioRisposta { Corretta = true, Tentativi = 2, SecondoTentativo = false };
        Assert.Equal(1.0, d4.PesoErrore());
    }

    [Fact]
    public void StatisticheService_Calcola_MezziErrori()
    {
        var dettagli = new List<DettaglioRisposta>();

        // 7 giusti (peso 0)
        for (int i = 0; i < 7; i++)
        {
            dettagli.Add(new DettaglioRisposta
            {
                IdDomanda = $"q_ok_{i}",
                MateriaNome = "CSharp",
                Categoria = "OOP",
                Corretta = true,
                Tentativi = 1,
                SecondoTentativo = false
            });
        }

        // 2 mezzi errori (peso 0.5 ciascuno = 1.0)
        for (int i = 0; i < 2; i++)
        {
            dettagli.Add(new DettaglioRisposta
            {
                IdDomanda = $"q_half_{i}",
                MateriaNome = "CSharp",
                Categoria = "OOP",
                Corretta = true,
                Tentativi = 1,
                SecondoTentativo = true
            });
        }

        // 1 errore pieno (peso 1.0)
        dettagli.Add(new DettaglioRisposta
        {
            IdDomanda = "q_err_0",
            MateriaNome = "CSharp",
            Categoria = "OOP",
            Corretta = false,
            Tentativi = 1,
            SecondoTentativo = false
        });

        var sessione = new RisultatoQuiz
        {
            MateriaNome = "CSharp",
            Dettagli = dettagli
        };

        var service = new StatisticheService();
        var risultato = service.Calcola(new[] { sessione });

        Assert.Single(risultato);
        var materiaStat = risultato[0];
        Assert.Equal("CSharp", materiaStat.Materia);
        Assert.Single(materiaStat.Categorie);

        var catStat = materiaStat.Categorie[0];
        Assert.Equal("OOP", catStat.Categoria);
        Assert.Equal(10, catStat.Visti);
        Assert.Equal(2.0, catStat.Errori);
        Assert.Equal(0.2, catStat.PctErrore, precision: 5);
        Assert.Equal(3, catStat.Sbagliate.Count); // 2 mezzi errori + 1 errore pieno
    }

    [Fact]
    public void CalcolaPunteggio_PuntiDiversi()
    {
        var r = new RisultatoQuiz
        {
            Dettagli = new List<DettaglioRisposta>
            {
                // Corretta primo colpo: 10 punti
                new() { Corretta = true, Tentativi = 1, SecondoTentativo = false },
                // Corretta al secondo tentativo: 7 punti
                new() { Corretta = true, Tentativi = 1, SecondoTentativo = true },
                // Corretta dopo rotazione (Tentativi > 1): 4 punti
                new() { Corretta = true, Tentativi = 2, SecondoTentativo = false },
                // Sbagliata: 0 punti
                new() { Corretta = false, Tentativi = 1, SecondoTentativo = false },
            }
        };

        int punteggio = QuizService.CalcolaPunteggio(r);
        Assert.Equal(10 + 7 + 4, punteggio); // 21 punti

        // Verifica puntuale singola
        var r10 = new RisultatoQuiz { Dettagli = { new() { Corretta = true, Tentativi = 1, SecondoTentativo = false } } };
        Assert.Equal(10, QuizService.CalcolaPunteggio(r10));

        var r7 = new RisultatoQuiz { Dettagli = { new() { Corretta = true, Tentativi = 1, SecondoTentativo = true } } };
        Assert.Equal(7, QuizService.CalcolaPunteggio(r7));

        var r4 = new RisultatoQuiz { Dettagli = { new() { Corretta = true, Tentativi = 2, SecondoTentativo = false } } };
        Assert.Equal(4, QuizService.CalcolaPunteggio(r4));
    }

    [Fact]
    public void Deserializzazione_SenzaCampoNuovo_DefaultFalse()
    {
        string json = """
            {
                "IdDomanda": "domanda_1",
                "Categoria": "Basi",
                "MateriaNome": "CSharp",
                "Corretta": true,
                "Tentativi": 1
            }
            """;

        var dettaglio = JsonSerializer.Deserialize<DettaglioRisposta>(json);
        Assert.NotNull(dettaglio);
        Assert.False(dettaglio.SecondoTentativo);
        Assert.Equal(0.0, dettaglio.PesoErrore());
    }
}
