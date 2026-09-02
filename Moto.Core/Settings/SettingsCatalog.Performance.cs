// Moto.Core/Settings/SettingsCatalog.Performance.cs
namespace Moto.Core.Settings
{
    public static partial class SettingsCatalog
    {
        static partial void RegisterPerformance();

        static partial void RegisterPerformance()
        {
            // ★ CORRECTION (02/09, revue croisée) : "power_mode" retiré d'ici —
            // doublon EXACT (même Id) avec SettingsCatalog.cs:250, déjà relié à
            // RealEffectKeys/RealSettingChanged (SettingsWindowView.xaml.cs). Deux
            // objets pour la même clé de persistance auraient affiché "Mode de
            // puissance" deux fois dans l'écran Réglages (catégorie Agent), avec
            // deux descriptions différentes pour la même valeur réelle — trouvé en
            // reconnectant cette catégorie (jamais visible avant, ce fichier
            // n'était jamais appelé). La version de SettingsCatalog.cs fait foi.
            T("performance_full_auto", "Agent", "Performance", "MOTO fait tout pour moi",
              "Preset débutant : active Ultra (tout automatique).", false);
            T("performance_show_indicator", "Agent", "Performance", "Indicateur de mode",
              "Affiche le mode actif dans la barre de statut.", true);
        }
    }
}
// → Dans SettingsCatalog.Extensions.cs, à la fin de RegisterExtensions(), ajouter :
//   RegisterPerformance();
