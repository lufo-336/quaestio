using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Wsa.Quiz.Core.Risorse;

namespace Wsa.Quiz.App;

/// <summary>
/// Rappresenta una variante cromatica d'accento dell'applicazione.
/// </summary>
/// <param name="BasePerScuro">Se valorizzato, sostituisce <c>Base</c> per i controlli Fluent
/// (caselle, interruttori, linea della tab) quando il tema e' scuro: serve ai colori troppo
/// scuri per staccarsi dallo sfondo nero (Grafite). Barra e bottoni restano su <c>Base</c>.</param>
public sealed record ColoreAccento(string Id, string NomeRisorsa, Color Base, Color Hover, Color Pressed, Color? BasePerScuro = null)
{
    /// <summary>Nome localizzato del colore (letto dalle risorse Strings).</summary>
    public string Nome => Strings.ResourceManager.GetString(NomeRisorsa, Lingue.Cultura) ?? Id;
}

/// <summary>
/// Tavolozza dei colori d'accento selezionabili dall'utente.
/// Gestisce la definizione dei colori e la loro applicazione a runtime
/// nelle Application.Current.Resources.
///
/// NOTA SUI COLORI (Trappola 20 e Trappola 7):
/// Usiamo brush espliciti propri (AccentoBrush, AccentoHoverBrush, AccentoPressedBrush)
/// invece di SystemAccentColorBrush perché su piattaforme mobile (Android)
/// SystemAccentColorBrush può risolversi in un colore trasparente o bianco,
/// rendendo i bottoni abilitati invisibili. Inoltre, nei Setter degli Style in XAML
/// si usano DynamicResource di tipo Brush e non Color (Trappola 7).
/// Per i controlli del tema Fluent impostiamo anche SystemAccentColor* (Color).
/// </summary>
public static class Tavolozza
{
    public static IReadOnlyList<ColoreAccento> Colori { get; } = new List<ColoreAccento>
    {
        new("blu", "Colore_Blu", Color.Parse("#0078D4"), Color.Parse("#2B90DE"), Color.Parse("#005FB0")),
        new("blunotte", "Colore_BluNotte", Color.Parse("#23408E"), Color.Parse("#3555A8"), Color.Parse("#1A3170")),
        new("indaco", "Colore_Indaco", Color.Parse("#4F46E5"), Color.Parse("#6A62EE"), Color.Parse("#3F37C4")),
        new("viola", "Colore_Viola", Color.Parse("#7E3FB8"), Color.Parse("#9459CC"), Color.Parse("#663096")),
        new("magenta", "Colore_Magenta", Color.Parse("#B4236E"), Color.Parse("#C73E86"), Color.Parse("#931B59")),
        new("verdeacqua", "Colore_VerdeAcqua", Color.Parse("#00838F"), Color.Parse("#1A9AA6"), Color.Parse("#006A74")),
        new("cacao", "Colore_Cacao", Color.Parse("#7A4E2D"), Color.Parse("#8F6342"), Color.Parse("#613D22")),
        new("grafite", "Colore_Grafite", Color.Parse("#3D4B5C"), Color.Parse("#526174"), Color.Parse("#2E3946"), Color.Parse("#8A9BB0")),
    };

    public static ColoreAccento Trova(string? id)
    {
        if (string.IsNullOrEmpty(id)) return Colori[0];
        return Colori.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Colori[0];
    }

    private static string? _ultimo;

    /// <summary>Riapplica l'ultimo colore scelto: va chiamato quando cambia il tema chiaro/scuro.</summary>
    public static void Riapplica() => Applica(_ultimo);

    public static void Applica(string? id)
    {
        _ultimo = id;
        var c = Trova(id);
        var app = Application.Current;
        if (app == null) return;
        bool scuro = app.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        Color fluent = scuro && c.BasePerScuro is Color chiaro ? chiaro : c.Base;

        // Nuovi oggetti SolidColorBrush a ogni chiamata
        app.Resources["AccentoBrush"] = new SolidColorBrush(c.Base);
        app.Resources["AccentoHoverBrush"] = new SolidColorBrush(c.Hover);
        app.Resources["AccentoPressedBrush"] = new SolidColorBrush(c.Pressed);

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
