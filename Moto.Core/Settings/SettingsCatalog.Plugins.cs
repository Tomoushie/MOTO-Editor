// Moto.Core/Settings/SettingsCatalog.Plugins.cs
namespace Moto.Core.Settings
{
    public static partial class SettingsCatalog
    {
        private static readonly List<SettingDefinition> _dynamicSettings = new();

        /// <summary>
        /// Enregistre dynamiquement un paramètre (utilisé par les plugins).
        /// </summary>
        public static void RegisterDynamic(SettingDefinition definition)
        {
            _dynamicSettings.Add(definition);
        }

        /// <summary>
        /// Retourne TOUS les paramètres (statiques + dynamiques).
        /// </summary>
        public static IReadOnlyList<SettingDefinition> GetAll()
        {
            var all = new List<SettingDefinition>();
            all.AddRange(GetStaticSettings()); // méthode existante
            all.AddRange(_dynamicSettings);
            return all;
        }
    }
}
