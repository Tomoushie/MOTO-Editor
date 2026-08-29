// Moto.Core/Settings/SettingsCatalog.AutoLink.cs
namespace Moto.Core.Settings
{
    public static partial class SettingsCatalog
    {
        static partial void RegisterAutoLink()
        {
            T("autolink_enabled", "Agent", "AutoLink", "AutoLink activé",
              "Détecte automatiquement les liens manquants.", true);
            T("autolink_auto_apply", "Agent", "AutoLink", "Application automatique",
              "Applique les suggestions sans confirmation (mode expert).", false);
            I("autolink_scan_interval_sec", "Agent", "AutoLink", "Intervalle de scan (sec)",
              "Délai entre deux analyses AutoLink.", 5, 1, 60);
        }
    }
}
// → Dans SettingsCatalog.Extensions.cs, à la fin de RegisterExtensions(), ajouter :
//   RegisterAutoLink();
