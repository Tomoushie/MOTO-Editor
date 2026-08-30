// Moto.Core/Settings/SettingsCatalog.Platform.cs (v2 — +3 paramètres)
namespace Moto.Core.Settings
{
    public static partial class SettingsCatalog
    {
        static partial void RegisterPlatform();

        static partial void RegisterPlatform()
        {
            T("platform_auto_detect", "Agent", "Platform Engine", "Détection auto des portages",
              "Analyse automatiquement les portages à l'ouverture du projet.", true);
            T("platform_include_linux", "Agent", "Platform Engine", "Inclure Linux",
              "Propose le portage Linux (scripts + doc).", true);
            T("platform_generate_ci", "Agent", "Platform Engine", "Générer les pipelines CI",
              "Génère les workflows CI par plateforme.", false);
            E("platform_ci_provider", "Agent", "Platform Engine", "Provider CI",
              "GitHub Actions, GitLab CI, Azure DevOps ou tous.", "GitHub",
              "GitHub", "GitLab", "Azure", "All");
            T("platform_auto_validate", "Agent", "Platform Engine", "Validation post-génération",
              "Lance un build après chaque portage pour vérifier les TFM.", true);
            T("platform_incremental_validate", "Agent", "Platform Engine", "Validation incrémentale",
              "Compile uniquement les TFM ajoutés (plus rapide).", true);
            T("platform_avalonia_linux", "Agent", "Platform Engine", "Head Avalonia Linux",
              "Génère le vrai éditeur Moto.Linux (Avalonia + Moto.Core).", true);
            T("platform_smart_detect", "Agent", "Platform Engine", "Détection continue intelligente",
              "Ne re-analyse que si un fichier modifié contient un pattern plateforme.", true);
        }
    }
}
// → Dans SettingsCatalog.Extensions.cs, à la fin de RegisterExtensions() :
//   RegisterPlatform();
