// Moto.Core/Settings/SettingsCatalog.Beginner.cs
namespace Moto.Core.Settings
{
    /// <summary>Paramètres des 4 modes débutants.</summary>
    public static partial class SettingsCatalog
    {
        static partial void RegisterBeginner();

        static partial void RegisterBeginner()
        {
            T("explain_everything", "Débutant", "Modes", "Explain Everything", "Explique chaque fichier, ligne, erreur, système, dépendance.", false);
            T("tutor_mode", "Débutant", "Modes", "AI Tutor", "L'IA pose des questions, propose des exercices, félicite.", false);
            T("nocode_mode", "Débutant", "Modes", "No Code", "Décris : l'IA génère, connecte et valide tout.", false);
            T("pair_programming", "Débutant", "Modes", "Pair Programming", "L'IA écrit avec toi (suggestions ghost, Tab pour accepter).", false);
        }
    }
}
// → AJOUTER une ligne dans SettingsCatalog.Extensions.cs, à la fin de RegisterExtensions() :
//   RegisterBeginner();
