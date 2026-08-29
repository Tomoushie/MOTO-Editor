// Moto.Core/Settings/ISettingsStore.cs
// Interface minimale pour découpler les services de SettingsEngine.Shared.
// Ajout non cassant : SettingsEngine implémente déjà ces méthodes.
namespace Moto.Core.Settings
{
    /// <summary>
    /// Contrat de stockage de paramètres.
    /// Permet d'injecter un fake dans les tests unitaires.
    /// </summary>
    public interface ISettingsStore
    {
        T Get<T>(string key, T defaultValue);
        void Set<T>(string key, T value);
        object? GetRaw(string key);
        bool GetBool(string key, bool defaultValue = false);
        string GetString(string key, string defaultValue = "");
    }
}
