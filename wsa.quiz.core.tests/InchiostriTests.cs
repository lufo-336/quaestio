using System.Globalization;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.Core.Tests;

public class InchiostriTests
{
    [Fact]
    public void Tutti_Sedici_InOrdine()
    {
        var attesi = new[]
        {
            "rosso", "terracotta", "cacao", "ocra",
            "oliva", "prato", "verdeacqua", "petrolio",
            "azzurro", "blu", "indaco", "viola",
            "prugna", "rosaantico", "ardesia", "grigio"
        };

        Assert.Equal(attesi, Inchiostri.Tutti.Select(i => i.Id));
    }

    [Fact]
    public void Sceglibili_SonoDodici()
    {
        var attesiSceglibili = new[]
        {
            "terracotta", "cacao", "oliva", "prato",
            "petrolio", "azzurro", "blu", "indaco",
            "viola", "prugna", "rosaantico", "ardesia"
        };

        var sceglibili = Inchiostri.Tutti.Where(i => i.Sceglibile).Select(i => i.Id).ToList();
        Assert.Equal(12, sceglibili.Count);
        Assert.Equal(attesiSceglibili, sceglibili);

        foreach (var riservato in new[] { "rosso", "ocra", "verdeacqua", "grigio" })
            Assert.False(Inchiostri.Trova(riservato).Sceglibile);
    }

    [Theory]
    [InlineData("blu", "blu")]
    [InlineData("blunotte", "indaco")]
    [InlineData("indaco", "indaco")]
    [InlineData("viola", "viola")]
    [InlineData("magenta", "prugna")]
    [InlineData("verdeacqua", "petrolio")]
    [InlineData("cacao", "cacao")]
    [InlineData("grafite", "ardesia")]
    [InlineData(null, "blu")]
    [InlineData("", "blu")]
    [InlineData("xyz", "blu")]
    [InlineData("BLUNOTTE", "indaco")]
    [InlineData("rosso", "blu")]
    [InlineData("ocra", "blu")]
    [InlineData("grigio", "blu")]
    public void Migra_VecchiId(string? input, string atteso)
    {
        Assert.Equal(atteso, Inchiostri.MigraIdColore(input));
    }

    [Fact]
    public void Migra_OgniIdSceglibileRestaSeStesso()
    {
        foreach (var inchiostro in Inchiostri.Tutti.Where(i => i.Sceglibile))
        {
            Assert.Equal(inchiostro.Id, Inchiostri.MigraIdColore(inchiostro.Id));
            Assert.Equal(inchiostro.Id, Inchiostri.MigraIdColore(inchiostro.Id.ToUpperInvariant()));
        }
    }

    [Fact]
    public void TestoSopra_Contrasto()
    {
        foreach (var i in Inchiostri.Tutti)
        {
            string testo = i.TestoNero ? "#000000" : "#ffffff";
            double contrasto = Inchiostri.Contrasto(i.Hex, testo);
            Assert.True(contrasto >= 4.5, $"{i.Id} ({i.Hex}) su {testo} ha contrasto {contrasto:F2} < 4.5");
        }
    }

    [Theory]
    [InlineData("rosso")]
    [InlineData("ocra")]
    [InlineData("verdeacqua")]
    public void TestoEsito_SuChiaro_Contrasto(string id)
    {
        var inchiostro = Inchiostri.Trova(id);
        Assert.False(string.IsNullOrWhiteSpace(inchiostro.HexTestoSuChiaro));
        double contrasto = Inchiostri.Contrasto(inchiostro.HexTestoSuChiaro!, "#e8e7e3");
        Assert.True(contrasto >= 4.5, $"{id} ({inchiostro.HexTestoSuChiaro}) su #e8e7e3 ha contrasto {contrasto:F2} < 4.5");
    }

    [Fact]
    public void Contrasto_Riferimento()
    {
        double contrasto = Inchiostri.Contrasto("#000000", "#ffffff");
        Assert.InRange(contrasto, 20.99, 21.01);
        Assert.InRange(Inchiostri.Contrasto("#ffffff", "#000000"), 20.99, 21.01);
    }

    [Fact]
    public void VariantiOpzionali_SoloDovePrevisto()
    {
        foreach (var i in Inchiostri.Tutti)
        {
            if (i.Id == "grigio")
                Assert.Equal("#c4c3bd", i.HexChiaro);
            else
                Assert.Null(i.HexChiaro);

            if (i.Id is "rosso" or "ocra" or "verdeacqua")
                Assert.NotNull(i.HexTestoSuChiaro);
            else
                Assert.Null(i.HexTestoSuChiaro);
        }
    }

    [Fact]
    public void Trova_IdEsistenteEFallback()
    {
        Assert.Equal("#40a79b", Inchiostri.Trova("verdeacqua").Hex);
        Assert.Equal("blu", Inchiostri.Trova("BLU").Id);
        Assert.Equal(Inchiostri.Predefinito, Inchiostri.Trova(null).Id);
        Assert.Equal(Inchiostri.Predefinito, Inchiostri.Trova("inesistente").Id);
    }

    [Fact]
    public void Risorse_NomiPresenti_ItEdEn()
    {
        var it = CultureInfo.GetCultureInfo("it");
        var en = CultureInfo.GetCultureInfo("en");

        foreach (var i in Inchiostri.Tutti)
        {
            Assert.False(string.IsNullOrWhiteSpace(Strings.ResourceManager.GetString(i.NomeRisorsa, it)),
                $"Manca stringa it per {i.NomeRisorsa}");
            Assert.False(string.IsNullOrWhiteSpace(Strings.ResourceManager.GetString(i.NomeRisorsa, en)),
                $"Manca stringa en per {i.NomeRisorsa}");
        }
    }

    [Theory]
    [InlineData("#141413", "#e8e7e3")]
    [InlineData("#f4f4f2", "#0c0c0c")]
    public void VarianteLeggibile_TuttiSceglibili_ContrastoSufficiente(string hexTesto, string hexSfondo)
    {
        foreach (var inchiostro in Inchiostri.Tutti.Where(i => i.Sceglibile))
        {
            string risultato = Inchiostri.VarianteLeggibile(inchiostro.Hex, hexTesto, hexSfondo);
            double contrasto = Inchiostri.Contrasto(risultato, hexSfondo);
            Assert.True(contrasto >= 4.5,
                $"{inchiostro.Id} ({inchiostro.Hex} -> {risultato}) su {hexSfondo} ha contrasto {contrasto:F2} < 4.5");
        }
    }

    [Theory]
    [InlineData("rosso")]
    [InlineData("ocra")]
    [InlineData("verdeacqua")]
    public void VarianteLeggibile_EsitiSuSfondoRialzatoScuro_ContrastoSufficiente(string id)
    {
        var inchiostro = Inchiostri.Trova(id);
        string risultato = Inchiostri.VarianteLeggibile(inchiostro.Hex, "#f4f4f2", "#161616");
        double contrasto = Inchiostri.Contrasto(risultato, "#161616");
        Assert.True(contrasto >= 4.5,
            $"{id} ({inchiostro.Hex} -> {risultato}) su #161616 ha contrasto {contrasto:F2} < 4.5");
    }

    [Fact]
    public void VarianteLeggibile_SeGiaLeggibile_RestaInvariato()
    {
        string risultato = Inchiostri.VarianteLeggibile("#40a79b", "#f4f4f2", "#161616");
        Assert.Equal("#40a79b", risultato);
    }
}
