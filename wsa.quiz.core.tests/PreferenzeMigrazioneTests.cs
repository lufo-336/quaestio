using System.Text.Json;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.Core.Tests;

public sealed class PreferenzeMigrazioneTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "quaestio-pref-test-" + Guid.NewGuid().ToString("N"));

    public PreferenzeMigrazioneTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Theory]
    [InlineData("blunotte", "indaco")]
    [InlineData("magenta", "prugna")]
    [InlineData("verdeacqua", "petrolio")]
    [InlineData("grafite", "ardesia")]
    [InlineData("sconosciuto", "blu")]
    [InlineData("", "blu")]
    public void CaricaPreferenze_MigraESalvaUnaVolta(string vecchioId, string atteso)
    {
        string path = Path.Combine(_dir, "preferenze.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new PreferenzeUtente { ColoreAccento = vecchioId }));

        var storage = new StorageService(_dir, _dir);
        var caricate = storage.CaricaPreferenze();

        Assert.Equal(atteso, caricate.ColoreAccento);

        var suDisco = JsonSerializer.Deserialize<PreferenzeUtente>(File.ReadAllText(path));
        Assert.NotNull(suDisco);
        Assert.Equal(atteso, suDisco!.ColoreAccento);

        // La seconda lettura non deve riscrivere il file (idempotente)
        var sentinella = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, sentinella);

        var ricaricate = storage.CaricaPreferenze();
        Assert.Equal(atteso, ricaricate.ColoreAccento);
        Assert.Equal(sentinella, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void CaricaPreferenze_IdValido_NonRiscriveFile()
    {
        string path = Path.Combine(_dir, "preferenze.json");
        File.WriteAllText(path, """{"ColoreAccento":"oliva","TemaScuro":true}""");
        var sentinella = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, sentinella);

        var storage = new StorageService(_dir, _dir);
        var caricate = storage.CaricaPreferenze();

        Assert.Equal("oliva", caricate.ColoreAccento);
        Assert.True(caricate.TemaScuro);
        Assert.Equal(sentinella, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void CaricaPreferenze_FileAssente_NonCreaFile()
    {
        var storage = new StorageService(_dir, _dir);
        var caricate = storage.CaricaPreferenze();

        Assert.Equal(Inchiostri.Predefinito, caricate.ColoreAccento);
        Assert.False(File.Exists(Path.Combine(_dir, "preferenze.json")));
    }
}
