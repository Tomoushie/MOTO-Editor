using System;
using Microsoft.Extensions.DependencyInjection;
using Moto.Editor.Views;

namespace Moto.Editor.Services;

/// <summary>
/// Entrée de palette de commandes « À propos de MOTO Editor ».
/// Découplée : la palette déclenche Execute(), MainPage affiche via l'event.
/// </summary>
public sealed class AboutPaletteCommandService
{
    private readonly IServiceProvider _services;

    /// <summary>Identifiant stable de la commande (utilisé par la palette).</summary>
    public const string CommandId = "about.moto";

    /// <summary>Libellé affiché dans la palette.</summary>
    public const string CommandTitle = "À propos de MOTO Editor";

    /// <summary>Description courte.</summary>
    public const string CommandDescription = "Logo, version, badges et infos système";

    /// <summary>Déclenché quand la commande est exécutée → MainPage affiche la vue.</summary>
    public event Action<AboutView>? AboutRequested;

    public AboutPaletteCommandService(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// Exécute la commande : résout AboutView via DI et demande son affichage.
    /// </summary>
    public void Execute()
    {
        var about = _services.GetService<AboutView>();
        if (about is null) return;
        AboutRequested?.Invoke(about);
    }
}
