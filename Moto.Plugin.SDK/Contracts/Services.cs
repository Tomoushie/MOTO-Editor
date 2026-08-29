// Moto.Plugin.SDK/Contracts/Services.cs
// Services fournis par l'hôte au plugin (paramètres + logging).
using System;

namespace Moto.Plugin.SDK
{
    /// <summary>Accès typé aux paramètres du plugin (préfixés automatiquement).</summary>
    public interface IPluginSettingsAccessor
    {
        T Get<T>(string key, T defaultValue);
        void Set<T>(string key, T value);
        /// <summary>Déclenché quand un paramètre du plugin change.</summary>
        event Action<string, object?>? Changed;
    }

    /// <summary>Logger minimal fourni par l'éditeur.</summary>
    public interface IPluginLogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception? ex = null);
    }
}
