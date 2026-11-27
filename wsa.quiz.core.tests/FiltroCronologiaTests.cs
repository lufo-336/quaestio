using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.Core.Tests;

public class FiltroCronologiaTests
{
    private static RisultatoQuiz CreaQuiz(string materia = "C#", string modalita = "Completo") => new()
    {
        Id = Guid.NewGuid().ToString(),
        DataOra = new DateTime(2026, 11, 26, 12, 0, 0),
        Modalita = modalita,
        MateriaNome = materia,
        TotaleDomande = 10,
        RisposteCorrette = 8,
        RisposteErrate = 2,
        PercentualeCorrette = 80,
        Dettagli = new List<DettaglioRisposta>
        {
            new() { IdDomanda = "q1", MateriaNome = materia, Categoria = "Base", Corretta = true }
        }
    };

    private static RisultatoQuiz CreaLab(string linguaggio = "C#") => new()
    {
        Id = Guid.NewGuid().ToString(),
        DataOra = new DateTime(2026, 11, 26, 13, 0, 0),
        Modalita = LaboratorioService.Modalita,
        MateriaNome = linguaggio + LaboratorioService.SuffissoMateria,
        TotaleDomande = 3,
        RisposteCorrette = 2,
        RisposteErrate = 1,
        PercentualeCorrette = 66.7,
        Dettagli = new List<DettaglioRisposta>
        {
            new()
            {
                IdDomanda = "lab:cs:media-voti:v1:output",
                MateriaNome = linguaggio + LaboratorioService.SuffissoMateria,
                Categoria = "Divisione intera",
                Corretta = true
            }
        }
    };

    [Fact]
    public void Applica_LaboratorioFalse_RestituisceSoloQuiz()
    {
        var q1 = CreaQuiz("C#", "Completo");
        var q2 = CreaQuiz("SQL", "");
        var l1 = CreaLab();
        var l2 = CreaLab();
        var cronologia = new[] { q1, l1, q2, l2 };

        var filtro = new FiltroCronologia { Laboratorio = false };
        var risultato = filtro.Applica(cronologia).ToList();

        Assert.Equal(new[] { q1, q2 }, risultato);
    }

    [Fact]
    public void Applica_LaboratorioTrue_RestituisceSoloLaboratorio()
    {
        var q1 = CreaQuiz("C#", "Completo");
        var q2 = CreaQuiz("SQL", "Per categoria");
        var l1 = CreaLab();
        var l2 = CreaLab();
        var cronologia = new[] { q1, l1, q2, l2 };

        var filtro = new FiltroCronologia { Laboratorio = true };
        var risultato = filtro.Applica(cronologia).ToList();

        Assert.Equal(new[] { l1, l2 }, risultato);
    }

    [Fact]
    public void Attivo_SoloLaboratorioTrue_RestaFalse()
    {
        var filtro = new FiltroCronologia { Laboratorio = true };

        Assert.False(filtro.Attivo);

        filtro.PctMin = 50;
        Assert.True(filtro.Attivo);

        filtro.PctMin = null;
        Assert.False(filtro.Attivo);
    }

    [Fact]
    public void MaterieDisponibili_SuCronologiaFiltrataPerTipo_SeparaQuizELaboratorio()
    {
        var q = CreaQuiz("SQL");
        var l = CreaLab("C#");
        var cronologia = new[] { q, l };

        var materieQuiz = FiltroCronologia.MaterieDisponibili(
            cronologia.Where(r => !LaboratorioService.ERisultatoLab(r)));
        var materieLab = FiltroCronologia.MaterieDisponibili(
            cronologia.Where(LaboratorioService.ERisultatoLab));

        Assert.Equal(new[] { "SQL" }, materieQuiz);
        Assert.Equal(new[] { "C# · Laboratorio" }, materieLab);
    }
}
