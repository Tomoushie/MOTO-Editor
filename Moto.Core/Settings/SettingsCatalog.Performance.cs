// Moto.Core/Settings/SettingsCatalog.Performance.cs
namespace Moto.Core.Settings
{
    public static partial class SettingsCatalog
    {
        static partial void RegisterPerformance();

        static partial void RegisterPerformance()
        {
            E("power_mode", "Agent", "Performance", "Mode de puissance",
              "Niveau de performance de MOTO AI.", "Balanced",
              "Eco", "Balanced", "Turbo", "Ultra");
            T("performance_full_auto", "Agent", "Performance", "MOTO fait tout pour moi",
              "Preset débutant : active Ultra (tout automatique).", false);
            T("performance_show_indicator", "Agent", "Performance", "Indicateur de mode",
              "Affiche le mode actif dans la barre de statut.", true);
        }
    }
}
// → Dans SettingsCatalog.Extensions.cs, à la fin de RegisterExtensions(), ajouter :
//   RegisterPerformance();
