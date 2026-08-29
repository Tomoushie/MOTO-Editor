// Moto.Plugin.SDK/IPlugin.cs
namespace Moto.Plugin.SDK
{
    /// <summary>
    /// Contrat de base pour tous les plugins MOTO.
    /// </summary>
    public interface IPlugin
    {
        /// <summary>Identifiant unique du plugin.</summary>
        string Id { get; }

        /// <summary>Nom d'affichage.</summary>
        string Name { get; }

        /// <summary>Version du plugin.</summary>
        string Version { get; }

        /// <summary>Auteur du plugin.</summary>
        string Author { get; }

        /// <summary>Description courte.</summary>
        string Description { get; }

        /// <summary>
        /// Appelé quand le plugin est chargé.
        /// </summary>
        void Initialize(IPluginContext context);

        /// <summary>
        /// Appelé quand le plugin est activé.
        /// </summary>
        void Activate();

        /// <summary>
        /// Appelé quand le plugin est désactivé.
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Appelé quand le plugin est déchargé.
        /// </summary>
        void Dispose();
    }

    /// <summary>
    /// Contexte fourni au plugin pour interagir avec l'éditeur.
    /// </summary>
    public interface IPluginContext
    {
        /// <summary>Chemin du workspace actuel.</summary>
        string WorkspaceRoot { get; }

        /// <summary>Chemin du dossier du plugin.</summary>
        string PluginDirectory { get; }

        /// <summary>Enregistre une commande slash.</summary>
        void RegisterCommand(string command, Action<string> handler);

        /// <summary>Affiche un message dans la barre de statut.</summary>
        void SetStatus(string message);

        /// <summary>Ouvre un fichier dans l'éditeur.</summary>
        void OpenFile(string filePath);

        /// <summary>Écrit dans le log de l'éditeur.</summary>
        void Log(string message);
    }
}
