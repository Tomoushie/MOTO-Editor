using System.Collections.Generic;
using Microsoft.Maui.ApplicationModel;
using Moto.Editor.Services;
using Moto.Editor.Views;

namespace Moto.Editor;

/// <summary>
/// Greffe additive : entrée « À propos » dans la palette de commandes.
/// Fichier partial : ne modifie pas MainPage.xaml.cs existant.
/// </summary>
public partial class MainPage
{
    private AboutPaletteCommandService? _aboutCommand;

    /// <summary>
    /// À appeler UNE fois dans le constructeur de MainPage (après InitializeComponent).
    /// Abonne l'affichage de la vue À propos.
    /// </summary>
    private void HookAboutCommand()
    {
        _aboutCommand = Handler?.MauiContext?.Services.GetService<AboutPaletteCommandService>();
        if (_aboutCommand is null) return;

        _aboutCommand.AboutRequested += view =>
            MainThread.BeginInvokeOnMainThread(() => ShowAboutView(view));
    }

    /// <summary>
    /// À fusionner dans la liste de commandes de la palette
    /// (une ligne : paletteCommands.AddRange(GetAboutPaletteCommands());).
    /// </summary>
    public IEnumerable<(string Id, string Title, string Description)> GetAboutPaletteCommands()
    {
        yield return (
            AboutPaletteCommandService.CommandId,
            AboutPaletteCommandService.CommandTitle,
            AboutPaletteCommandService.CommandDescription);
    }

    /// <summary>
    /// À insérer dans le dispatch de la palette (switch sur l'id de commande) :
    ///   case AboutPaletteCommandService.CommandId: ExecuteAboutCommand(); break;
    /// </summary>
    public void ExecuteAboutCommand()
    {
        _aboutCommand?.Execute();
    }

    /// <summary>
    /// Affiche la vue À propos dans le conteneur d'overlay existant.
    /// ← Adaptez au nom de votre méthode/Grid d'overlay réelle.
    /// </summary>
    private void ShowAboutView(AboutView view)
    {
        ShowInOverlay(view); // méthode d'overlay existante de MainPage
    }
}
