using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.Core.Tests;

public class TestoConCodiceTests
{
    private static SegmentoTesto T(string s) => new(TipoSegmento.Testo, s);
    private static SegmentoTesto I(string s) => new(TipoSegmento.Inline, s);
    private static SegmentoTesto B(string s, string? lang) => new(TipoSegmento.Blocco, s, lang);

    [Fact]
    public void Analizza_SoloTesto()
    {
        Assert.Equal(new[] { T("Ciao") }, TestoConCodice.Analizza("Ciao"));
    }

    [Fact]
    public void Analizza_Inline()
    {
        Assert.Equal(
            new[] { T("Cosa fa "), I("int.Parse(\"42\")"), T(" in C#?") },
            TestoConCodice.Analizza("Cosa fa `int.Parse(\"42\")` in C#?"));
    }

    [Fact]
    public void Analizza_BloccoConLinguaggio()
    {
        Assert.Equal(
            new[] { T("Che cosa stampa?"), B("def f():\n    return 1", "python") },
            TestoConCodice.Analizza("Che cosa stampa?\n\n```python\ndef f():\n    return 1\n```"));
    }

    [Fact]
    public void Analizza_BloccoSenzaLinguaggio()
    {
        Assert.Equal(
            new[] { T("Cosa produce?"), B("int x = 5;", null) },
            TestoConCodice.Analizza("Cosa produce?\n```\nint x = 5;\n```"));
    }

    [Fact]
    public void Analizza_TestoDopoBlocco()
    {
        Assert.Equal(
            new[] { T("A"), B("x;", "c"), T("B") },
            TestoConCodice.Analizza("A\n```c\nx;\n```\nB"));
    }

    [Fact]
    public void Analizza_BacktickSingoloMalformato()
    {
        Assert.Equal(new[] { T("costo `5") }, TestoConCodice.Analizza("costo `5"));
    }

    [Fact]
    public void Analizza_FenceNonChiusoMalformato()
    {
        Assert.Equal(
            new[] { T("A"), B("x = 1", "python") },
            TestoConCodice.Analizza("A\n```python\nx = 1"));
    }

    [Fact]
    public void Analizza_CrLf()
    {
        Assert.Equal(
            new[] { T("Che cosa stampa?"), B("def f():\n    return 1", "python") },
            TestoConCodice.Analizza("Che cosa stampa?\r\n\r\n```python\r\ndef f():\r\n    return 1\r\n```"));
    }

    [Fact]
    public void Analizza_NullOVuoto()
    {
        Assert.Empty(TestoConCodice.Analizza(null));
        Assert.Empty(TestoConCodice.Analizza(""));
    }

    [Fact]
    public void SenzaMarcatori_TogliBacktick()
    {
        Assert.Equal("usa x e\ny", TestoConCodice.SenzaMarcatori("usa `x` e\n```python\ny\n```"));
    }
}
