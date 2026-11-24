using System.Collections.Generic;
using System.Linq;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Models.Statistiche;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.Core.Tests;

public class PuntiDeboliTests
{
    [Fact]
    public void LivelloDi_AiBordi()
    {
        Assert.Equal(LivelliDebolezza.Verdi, QuizService.LivelloDi(0.0));
        Assert.Equal(LivelliDebolezza.Verdi, QuizService.LivelloDi(0.19));
        Assert.Equal(LivelliDebolezza.Ambra, QuizService.LivelloDi(0.20));
        Assert.Equal(LivelliDebolezza.Ambra, QuizService.LivelloDi(0.59));
        Assert.Equal(LivelliDebolezza.Rosse, QuizService.LivelloDi(0.60));
        Assert.Equal(LivelliDebolezza.Rosse, QuizService.LivelloDi(1.0));
    }

    [Fact]
    public void FiltraPerLivello_EsclusiVuoti_RestituisceTutto()
    {
        var d1 = new Domanda { Id = "1", MateriaNome = "CSharp", Categoria = "OOP" };
        var d2 = new Domanda { Id = "2", MateriaNome = "CSharp", Categoria = "Async" };
        var lista = new List<Domanda> { d1, d2 };

        var livelli = new Dictionary<(string Materia, string Categoria), string>
        {
            [("CSharp", "OOP")] = LivelliDebolezza.Rosse,
            [("CSharp", "Async")] = LivelliDebolezza.Verdi
        };

        var resNull = QuizService.FiltraPerLivello(lista, null, livelli);
        var resEmpty = QuizService.FiltraPerLivello(lista, new List<string>(), livelli);

        Assert.Equal(2, resNull.Count);
        Assert.Equal(2, resEmpty.Count);
    }

    [Fact]
    public void FiltraPerLivello_EscludendoVerdi_RestanoRosseAmbraNuove()
    {
        var dVerde = new Domanda { Id = "1", MateriaNome = "CSharp", Categoria = "VerdeCat" };
        var dAmbra = new Domanda { Id = "2", MateriaNome = "CSharp", Categoria = "AmbraCat" };
        var dRossa = new Domanda { Id = "3", MateriaNome = "CSharp", Categoria = "RossaCat" };
        var dNuova = new Domanda { Id = "4", MateriaNome = "CSharp", Categoria = "NuovaCat" };

        var lista = new List<Domanda> { dVerde, dAmbra, dRossa, dNuova };

        var livelli = new Dictionary<(string Materia, string Categoria), string>
        {
            [("CSharp", "VerdeCat")] = LivelliDebolezza.Verdi,
            [("CSharp", "AmbraCat")] = LivelliDebolezza.Ambra,
            [("CSharp", "RossaCat")] = LivelliDebolezza.Rosse
            // NuovaCat assente dal dizionario -> considerata Nuove
        };

        var esclusi = new List<string> { LivelliDebolezza.Verdi };
        var res = QuizService.FiltraPerLivello(lista, esclusi, livelli);

        Assert.Equal(3, res.Count);
        Assert.DoesNotContain(dVerde, res);
        Assert.Contains(dAmbra, res);
        Assert.Contains(dRossa, res);
        Assert.Contains(dNuova, res);
    }

    [Fact]
    public void FiltraPerLivello_CategoriaAssenteDalDizionario_ConsiderataNuova()
    {
        var dNuova = new Domanda { Id = "1", MateriaNome = "CSharp", Categoria = "MaiVista" };
        var dRossa = new Domanda { Id = "2", MateriaNome = "CSharp", Categoria = "RossaCat" };
        var lista = new List<Domanda> { dNuova, dRossa };

        var livelli = new Dictionary<(string Materia, string Categoria), string>
        {
            [("CSharp", "RossaCat")] = LivelliDebolezza.Rosse
        };

        // Se escludo le Nuove, la domanda con categoria assente viene esclusa
        var esclusi = new List<string> { LivelliDebolezza.Nuove };
        var res = QuizService.FiltraPerLivello(lista, esclusi, livelli);

        Assert.Single(res);
        Assert.Equal("2", res[0].Id);
    }

    [Fact]
    public void IdSbagliate_EstraeTuttiGliIdUnivoci()
    {
        static DomandaSbagliata D(string id) => new(id, "", "", "", 1, 1);
        static CategoriaStat C(string mat, string cat, params DomandaSbagliata[] d) => new(mat, cat, 1, 1, 1.0, d);
        var stat = new List<StatistichePerMateria>
        {
            new("CSharp", new List<CategoriaStat>
            {
                C("CSharp", "OOP", D("q1"), D("q2")),
                C("CSharp", "Async", D("q2"), D("q3")) // q2 duplicato
            }),
            new("SQL", new List<CategoriaStat> { C("SQL", "Join", D("q4")) })
        };

        var res = QuizService.IdSbagliate(stat);

        Assert.Equal(4, res.Count);
        Assert.Contains("q1", res);
        Assert.Contains("q2", res);
        Assert.Contains("q3", res);
        Assert.Contains("q4", res);
    }

    [Fact]
    public void ApplicaLimiteConPriorita_IdPrioritariVuota_ComportamentoAttuale()
    {
        var pool = Enumerable.Range(1, 10).Select(i => new Domanda { Id = $"d{i}" }).ToList();

        var res = QuizService.ApplicaLimiteConPriorita(pool, 4, null);
        Assert.Equal(4, res.Count);

        var res2 = QuizService.ApplicaLimiteConPriorita(pool, 4, new List<string>());
        Assert.Equal(4, res2.Count);

        // Limite maggiore del pool
        var res3 = QuizService.ApplicaLimiteConPriorita(pool, 20, null);
        Assert.Equal(10, res3.Count);

        // Limite <= 0
        var res4 = QuizService.ApplicaLimiteConPriorita(pool, 0, null);
        Assert.Equal(10, res4.Count);
    }

    [Fact]
    public void ApplicaLimiteConPriorita_PrendePrimaPrioritarie_PoiRiempie()
    {
        var pool = Enumerable.Range(1, 10).Select(i => new Domanda { Id = $"d{i}" }).ToList();
        var prioritari = new List<string> { "d3", "d7" };

        var res = QuizService.ApplicaLimiteConPriorita(pool, 5, prioritari);

        Assert.Equal(5, res.Count);
        Assert.Contains(res, d => d.Id == "d3");
        Assert.Contains(res, d => d.Id == "d7");
        Assert.Equal(5, res.Select(d => d.Id).Distinct().Count());
    }

    [Fact]
    public void ApplicaLimiteConPriorita_PiuPrioritarieDelLimite_TroncaALimite()
    {
        var pool = Enumerable.Range(1, 10).Select(i => new Domanda { Id = $"d{i}" }).ToList();
        var prioritari = new List<string> { "d1", "d2", "d3", "d4", "d5" };

        var res = QuizService.ApplicaLimiteConPriorita(pool, 3, prioritari);

        Assert.Equal(3, res.Count);
        Assert.All(res, d => Assert.Contains(d.Id, prioritari));
    }
}
