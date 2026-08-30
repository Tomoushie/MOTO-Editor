// Moto.Core/Settings/SettingItemGeneric.cs
// SettingItem<T> : déclaration compacte d'un paramètre (1 ligne = 1 paramètre, voir
// Docs/CONTRIBUTING.md et Docs/Résumé compressé.md). Ce type générique n'existait nulle
// part alors qu'il est utilisé par toutes les classes SettingsCatalog.*.cs : reconstruit ici.
// À ne pas confondre avec SettingItem.cs (wrapper UI non générique de Moto.Editor).
using System;

namespace Moto.Core.Settings
{
    /// <summary>
    /// Déclare un paramètre unique (identifiant, valeur par défaut, description) et
    /// expose sa valeur courante via <see cref="Value"/>, lue/écrite dans
    /// <see cref="SettingsEngine.Shared"/> (persistance JSON automatique).
    /// </summary>
    public sealed class SettingItem<T>
    {
        public string Id { get; }
        public T DefaultValue { get; }
        public string Description { get; }

        public SettingItem(string id, T defaultValue, string description = "")
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("L'identifiant d'un SettingItem ne peut pas être vide.", nameof(id));

            Id = id;
            DefaultValue = defaultValue;
            Description = description;
        }

        /// <summary>Valeur courante, lue/écrite dans SettingsEngine.Shared.</summary>
        public T Value
        {
            get => SettingsEngine.Shared.Get(Id, DefaultValue);
            set => SettingsEngine.Shared.Set(Id, value);
        }

        /// <summary>Permet d'utiliser directement l'item comme sa valeur (ex. `if (settings.Flag)`).</summary>
        public static implicit operator T(SettingItem<T> item) => item.Value;

        public override string ToString() => $"{Id} = {Value}";
    }
}
