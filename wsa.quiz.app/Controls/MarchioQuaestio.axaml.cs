using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.Controls;

/// <summary>
/// Marchio vettoriale "quaestio" (a6-2, Corsie invertite) per l'intestazione.
/// Un tocco ne cambia a caso il quartetto di colori, pescando dalla tavolozza degli inchiostri.
/// </summary>
public partial class MarchioQuaestio : UserControl
{
    private static readonly string[] Originali = ["#40a79b", "#539fd4", "#4d4dbc", "#e0703a"];

    public MarchioQuaestio()
    {
        InitializeComponent();
        for (int i = 0; i < 4; i++)
            Resources[$"MarchioC{i + 1}"] = new SolidColorBrush(Color.Parse(Originali[i]));
        PointerPressed += OnPressed;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        // Grigio escluso: sul fondo chiaro quasi non si vede.
        var scelti = Inchiostri.Tutti.Where(i => i.Id != "grigio").OrderBy(_ => Random.Shared.Next()).Take(4).ToArray();
        for (int i = 0; i < 4; i++)
            Resources[$"MarchioC{i + 1}"] = new SolidColorBrush(Color.Parse(scelti[i].Hex));
    }
}
