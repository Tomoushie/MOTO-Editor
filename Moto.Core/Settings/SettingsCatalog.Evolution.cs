// Moto.Core/Settings/SettingsCatalog.Evolution.cs
namespace Moto.Core.Settings
{
    /// <summary>Paramètres des modes Story / Evolution.</summary>
    public static partial class SettingsCatalog
    {
        // Déclaration de la méthode partielle (requise par le compilateur ;
        // absente ailleurs dans la classe partielle) — appelée depuis
        // RegisterExtensions() (une ligne à ajouter, voir note en fin de fichier).
        static partial void RegisterEvolution();

        static partial void RegisterEvolution()
        {
            T("evolution_enabled", "Agent", "Évolution", "Évolution proactive",
              "MOTO AI propose des améliorations sans que tu demandes rien.", true);
            I("evolution_interval_min", "Agent", "Évolution", "Intervalle évolution (min)",
              "Délai entre deux analyses proactives.", 5, 1, 120);
            T("story_comments", "Agent", "Story Mode", "Commentaires narratifs",
              "Injecte l'histoire en commentaires pédagogiques dans le code généré.", true);
        }
    }
}
// → Dans SettingsCatalog.Extensions.cs, à la fin de RegisterExtensions(), ajouter :
//   RegisterEvolution();
