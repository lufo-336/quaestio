using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App;

/// <summary>
/// Rappresenta una variante cromatica d'accento dell'applicazione.
/// </summary>
public sealed record ColoreAccento(string Id, string NomeRisorsa, Color Base, Color Hover, Color Pressed, Color Testo)
{
    /// <summary>Nome localizzato del colore (letto dalle risorse Strings).</summary>
    public string Nome => Strings.ResourceManager.GetString(NomeRisorsa, Lingue.Cultura) ?? Id;
}

/// <summary>
/// Tavolozza dei colori d'accento selezionabili dall'utente, costruita dagli
/// inchiostri sceglibili del core (<see cref="Inchiostri"/>).
/// Gestisce la definizione dei colori e la loro applicazione a runtime
/// nelle Application.Current.Resources.
///
/// NOTA SUI COLORI (Trappola 20 e Trappola 7):
/// Usiamo brush espliciti propri (AccentoBrush, AccentoHoverBrush, AccentoPressedBrush,
/// AccentoTestoBrush, AccentoLinkBrush) invece di SystemAccentColorBrush perché su piattaforme mobile (Android)
/// SystemAccentColorBrush può risolversi in un colore trasparente o bianco,
/// rendendo i bottoni abilitati invisibili. Inoltre, nei Setter degli Style in XAML
/// si usano DynamicResource di tipo Brush e non Color (Trappola 7).
/// Per i controlli del tema Fluent impostiamo anche SystemAccentColor* (Color).
/// </summary>
public static class Tavolozza
{
    public static IReadOnlyList<ColoreAccento> Colori { get; } = Inchiostri.Tutti
        .Where(i => i.Sceglibile)
        .Select(CreaColore)
        .ToList();

    private static ColoreAccento CreaColore(Inchiostro i)
    {
        var baseColor = Color.Parse(i.Hex);
        return new ColoreAccento(
            i.Id,
            i.NomeRisorsa,
            baseColor,
            Mescola(baseColor, Colors.White, 0.10),
            Mescola(baseColor, Colors.Black, 0.20),
            i.TestoNero ? Colors.Black : Colors.White);
    }

    public static ColoreAccento Trova(string? id)
    {
        string migrato = Inchiostri.MigraIdColore(id);
        return Colori.FirstOrDefault(c => string.Equals(c.Id, migrato, StringComparison.OrdinalIgnoreCase))
               ?? Colori.First(c => string.Equals(c.Id, Inchiostri.Predefinito, StringComparison.OrdinalIgnoreCase));
    }

    private static string? _ultimo;

    /// <summary>Riapplica l'ultimo colore scelto: va chiamato quando cambia il tema chiaro/scuro.</summary>
    public static void Riapplica() => Applica(_ultimo);

    public static void Applica(string? id)
    {
        string migrato = Inchiostri.MigraIdColore(id);
        _ultimo = migrato;
        var c = Trova(migrato);
        var app = Application.Current;
        if (app == null) return;
        Color fluent = c.Base;

        bool scuro = app.ActualThemeVariant == ThemeVariant.Dark;
        string hexTesto = scuro ? "#f4f4f2" : "#141413";
        string hexSfondo = scuro ? "#0c0c0c" : "#e8e7e3";
        var inchiostro = Inchiostri.Trova(migrato);
        var linkColor = Color.Parse(Inchiostri.VarianteLeggibile(inchiostro.Hex, hexTesto, hexSfondo));

        // Nuovi oggetti SolidColorBrush a ogni chiamata
        app.Resources["AccentoBrush"] = new SolidColorBrush(c.Base);
        app.Resources["AccentoHoverBrush"] = new SolidColorBrush(c.Hover);
        app.Resources["AccentoPressedBrush"] = new SolidColorBrush(c.Pressed);
        app.Resources["AccentoTestoBrush"] = new SolidColorBrush(c.Testo);
        app.Resources["AccentoLinkBrush"] = new SolidColorBrush(linkColor);

        // Color per il tema Fluent (caselle, interruttori, tab, barre di avanzamento)
        app.Resources["SystemAccentColor"] = fluent;
        app.Resources["SystemAccentColorDark1"] = Mescola(fluent, Colors.Black, 0.10);
        app.Resources["SystemAccentColorDark2"] = Mescola(fluent, Colors.Black, 0.20);
        app.Resources["SystemAccentColorDark3"] = Mescola(fluent, Colors.Black, 0.30);
        app.Resources["SystemAccentColorLight1"] = Mescola(fluent, Colors.White, 0.15);
        app.Resources["SystemAccentColorLight2"] = Mescola(fluent, Colors.White, 0.30);
        app.Resources["SystemAccentColorLight3"] = Mescola(fluent, Colors.White, 0.45);
    }

    private static Color Mescola(Color a, Color b, double t)
    {
        byte r = (byte)Math.Clamp(Math.Round(a.R + (b.R - a.R) * t), 0, 255);
        byte g = (byte)Math.Clamp(Math.Round(a.G + (b.G - a.G) * t), 0, 255);
        byte bVal = (byte)Math.Clamp(Math.Round(a.B + (b.B - a.B) * t), 0, 255);
        byte alpha = (byte)Math.Clamp(Math.Round(a.A + (b.A - a.A) * t), 0, 255);
        return Color.FromArgb(alpha, r, g, bVal);
    }
}
