using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Wsa.Quiz.App;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.Browser;

internal sealed partial class Program
{
    [JSImport("isTouchDevice", "quaestio-host")]
    internal static partial bool IsTouchDevice();

    [JSImport("loadStorageJson", "quaestio-host")]
    internal static partial string LoadStorageJson();

    [JSImport("saveStorageFile", "quaestio-host")]
    internal static partial void SaveStorageFile(string path, string content);

    [JSImport("removeStorageFile", "quaestio-host")]
    internal static partial void RemoveStorageFile(string path);

    private static void IdrataFileSystemDaLocalStorage()
    {
        try
        {
            string json = LoadStorageJson();
            var file = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (file == null) return;

            foreach (var (percorso, contenuto) in file)
            {
                if (string.IsNullOrEmpty(percorso) || !percorso.StartsWith('/'))
                    continue;

                string? dir = Path.GetDirectoryName(percorso);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(percorso, contenuto);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ATTENZIONE] Idratazione localStorage fallita: {ex.Message}");
        }
    }

    private static Task Main(string[] args)
    {
        IdrataFileSystemDaLocalStorage();

        StorageService.OnFilePersisted = (percorso, contenuto) =>
        {
            if (contenuto != null)
                SaveStorageFile(percorso, contenuto);
            else
                RemoveStorageFile(percorso);
        };

        AppEnv.UsaTouchInSingleView = IsTouchDevice();

        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Wsa.Quiz.App.App>();
}
