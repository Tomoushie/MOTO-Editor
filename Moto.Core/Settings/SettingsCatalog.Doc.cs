// Moto.Core/Settings/SettingsCatalog.Doc.cs
namespace Moto.Core.Settings
{
    public static partial class SettingsCatalog
    {
        static partial void RegisterDoc()
        {
            T("doc_auto_update", "Agent", "Documentation", "Mise à jour auto de la doc",
              "Régénère la documentation à chaque modification du projet.", true);
            T("doc_on_project_open", "Agent", "Documentation", "Doc à l'ouverture du projet",
              "Génère la documentation à l'ouverture d'un projet.", true);
            S("doc_folder", "Agent", "Documentation", "Dossier de documentation",
              "Chemin relatif pour la documentation.", ".moto/docs");
            T("doc_include_private", "Agent", "Documentation", "Inclure les fichiers privés",
              "Inclut les fichiers marqués privés dans la doc.", false);
        }
    }
}
// → Dans SettingsCatalog.Extensions.cs, à la fin de RegisterExtensions(), ajouter :
//   RegisterDoc();
