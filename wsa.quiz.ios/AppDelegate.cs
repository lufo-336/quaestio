using Avalonia;
using Avalonia.iOS;
using Avalonia.Media;
using Foundation;

namespace Wsa.Quiz.Ios;

// A differenza di Android (Trappola 14: l'Activity non e' piu' generica e l'App
// si sceglie in MainApplication), su iOS in Avalonia 12 l'App si sceglie qui,
// col parametro generico. Schema preso dal template ufficiale Avalonia.Templates
// xplat/AvaloniaTest.iOS.
#pragma warning disable CA1711 // il nome finisce in "Delegate": e' quello voluto da UIKit
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<Wsa.Quiz.App.App>
#pragma warning restore CA1711
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder)
            .With(new FontManagerOptions { DefaultFamilyName = "avares://Wsa.Quiz.App/Assets/Fonts#Archivo" });
}
