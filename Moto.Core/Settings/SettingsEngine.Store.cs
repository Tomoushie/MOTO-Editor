// Moto.Core/Settings/SettingsEngine.Store.cs
// Fait implémenter ISettingsStore par SettingsEngine (partial class).
// Aucune méthode existante n'est modifiée : on déclare juste l'interface.
namespace Moto.Core.Settings
{
    public partial class SettingsEngine : ISettingsStore
    {
        // Les méthodes Get<T>, Set<T>, GetRaw, GetBool, GetString
        // existent déjà dans la classe principale.
        // Ce partial déclare uniquement le contrat.
    }
}
