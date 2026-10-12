using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Platform;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Icona della finestra desktop (step 14): barra delle applicazioni e alt-tab.
    /// Se la risorsa mancasse si torna a nessuna icona invece di far cadere
    /// l'avvio dell'app.
    /// </summary>
    private static WindowIcon? IconaFinestra()
    {
        try
        {
            var uri = new Uri("avares://Wsa.Quiz.App/Assets/Branding/quaestio-icon.png");
            using var stream = AssetLoader.Open(uri);
            return new WindowIcon(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Applica la lingua da preferenze.json prima di creare qualunque vista.
    /// La lingua di sistema e' InstalledUICulture: resta quella del sistema anche
    /// dopo che Applica ha cambiato la cultura corrente.
    /// </summary>
    public static void LinguaIniziale()
    {
        string preferenza = "";
        try
        {
            // Solo la cartella utente conta qui; la fonte dati non viene letta.
            var storage = new StorageService(new FileSystemFonteDati(AppContext.BaseDirectory),
                                             StorageService.CartellaUtenteDefault());
            preferenza = storage.CaricaPreferenze().Lingua;
        }
        catch (Exception) { /* preferenze illeggibili: si segue il sistema */ }

        Lingue.Applica(Lingue.Risolvi(preferenza, CultureInfo.InstalledUICulture));
    }

    /// <summary>
    /// Mette una MainView nuova al posto di quella vecchia nel suo genitore: la
    /// finestra desktop (ContentControl), il LayoutTransformControl su Android
    /// (Decorator) o la finestra di un harness. La nuova istanza rilegge dati,
    /// traduzioni e preferenze, quindi si riapre sull'ultima sezione salvata.
    /// </summary>
    public static void SostituisciVistaPrincipale(MainView vecchia)
    {
        switch (vecchia.Parent)
        {
            case ContentControl cc: cc.Content = new MainView(); break;
            case Decorator dec: dec.Child = new MainView(); break;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        LinguaIniziale();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppEnv.TouchMode = false;
            desktop.MainWindow = new Window
            {
                Title = "Quaestio",
                Width = 1080,
                Height = 760,
                MinWidth = 780,
                MinHeight = 540,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Icon = IconaFinestra(),
                Content = new MainView()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            AppEnv.TouchMode = true;
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Wsa.Quiz.App/"))
            {
                Source = new Uri("avares://Wsa.Quiz.App/Themes/Sizes.Touch.axaml")
            });
            // Scala globale touch: avvolgo la MainView in un LayoutTransformControl
            // alla RADICE single-view. Qui l'host dà al wrapper un'altezza limitata
            // (= area schermo), quindi gli ScrollViewer interni restano limitati e
            // restano scrollabili. (Un tentativo precedente metteva il transform più
            // in basso, dove l'altezza era illimitata → l'ultima opzione finiva
            // irraggiungibile sotto la bottom nav; vedi commit c9a8a2d.)
            singleView.MainView = new LayoutTransformControl
            {
                LayoutTransform = new ScaleTransform(AppEnv.ScalaTouch, AppEnv.ScalaTouch),
                Child = new MainView()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
