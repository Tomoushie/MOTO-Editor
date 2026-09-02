// Moto.Core/Settings/SettingDefinition.cs (v2)
using System.Collections.Generic;

namespace Moto.Core.Settings
{
    public enum SettingType
    {
        Toggle,
        Int,
        Enum,
        String,
        Action,  // Bouton d'action (Configurer, Test Audio...)
        Double   // ★ AJOUT (02/09, réglages IA cachés) : valeurs fractionnaires
                 // (ex. seuils 0.0-1.0) — les ~123 réglages IA avancés
                 // (SettingItem<double>, Moto.Core.Settings) en comptent 4.
    }

    public class SettingDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SettingType Type { get; set; } = SettingType.Toggle;
        public object Default { get; set; }
        public List<string> Options { get; } = new List<string>();
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 10000;
        public int Step { get; set; } = 1;

        /// <summary>
        /// ★ AJOUT (02/09) : bornes pour SettingType.Double UNIQUEMENT — Min/Max
        /// ci-dessus restent des int (utilisés par SettingType.Int, déjà en
        /// production) ; un réglage fractionnaire (ex. 0.0-1.0) ne rentrerait pas
        /// dedans sans risquer de perdre la partie décimale.
        /// </summary>
        public double MinDouble { get; set; } = 0.0;
        public double MaxDouble { get; set; } = 1.0;

        /// <summary>Identifiant de l'action pour les paramètres de type Action.</summary>
        public string ActionId { get; set; } = string.Empty;

        /// <summary>Libellé du bouton d'action.</summary>
        public string ActionLabel { get; set; } = "Configurer";
    }
}
