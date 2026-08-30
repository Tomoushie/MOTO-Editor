// Moto.Core/Settings/SettingsCatalog.Context.cs
namespace Moto.Core.Settings
{
    public static partial class SettingsCatalog
    {
        static partial void RegisterContext();

        static partial void RegisterContext()
        {
            T("context_engine_enabled", "Agent", "Context Engine", "Context Engine activé",
              "Analyse le contexte et propose des suggestions proactives.", true);
            I("context_scan_interval_sec", "Agent", "Context Engine", "Intervalle de scan (sec)",
              "Délai entre deux analyses contextuelles.", 10, 5, 120);
            T("context_auto_apply", "Agent", "Context Engine", "Application automatique",
              "Applique les suggestions critiques sans confirmation.", false);
            T("context_show_low_priority", "Agent", "Context Engine", "Afficher priorité basse",
              "Affiche les suggestions de priorité basse (commentaires, renommage).", true);
        }
    }
}
// → Dans SettingsCatalog.Extensions.cs, à la fin de RegisterExtensions(), ajouter :
//   RegisterContext();
