// Moto.Editor/AI/Beginner/BeginnerCommands.cs (mise à jour)
namespace Moto.Editor.AI.Beginner
{
    /// <summary>
    /// Commandes visibles dans MOTO Editor.
    /// </summary>
    public static class BeginnerCommands
    {
        // === Actions existantes ===
        public const string ExplainThisCode = "MOTO: Expliquer ce code";
        public const string FixThisFile = "MOTO: Corriger ce fichier";
        public const string MakeThisBetter = "MOTO: Améliorer ce fichier";
        public const string GenerateMissingFiles = "MOTO: Générer les fichiers manquants";
        public const string ExplainErrors = "MOTO: Expliquer les erreurs";
        public const string TeachMe = "MOTO: Apprendre";

        // === Actions UI ===
        public const string ToggleViewMode = "MOTO: Basculer vue Débutant / Expert";
        public const string NewGuidedProject = "MOTO: Créer un projet (assisté)";
        public const string ShowQuickActions = "MOTO: Afficher les actions rapides";

        // === Nouvelles actions Builders ===
        public const string GenerateBlueprint = "MOTO: Créer un projet depuis une description";
        public const string BuildModule = "MOTO: Ajouter un module";
        public const string BuildBehavior = "MOTO: Ajouter un comportement";
        public const string FixMyProject = "MOTO: Réparer tout le projet";
    }

    /// <summary>
    /// IDs des Quick Actions, utilisés pour brancher les handlers dans MainWindow.
    /// </summary>
    public static class QuickActionIds
    {
        public const string AddMethod = "add-method";
        public const string AddClass = "add-class";
        public const string AddInterface = "add-interface";
        public const string AddSystem = "add-system";
        public const string AddNamespace = "add-namespace";
        public const string AddComment = "add-comment";
        public const string AddTest = "add-test";
        public const string ExplainSelection = "explain-selection";
    }
}
