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
        Action   // Nouveau : bouton d'action (Configurer, Test Audio...)
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

        /// <summary>Identifiant de l'action pour les paramètres de type Action.</summary>
        public string ActionId { get; set; } = string.Empty;

        /// <summary>Libellé du bouton d'action.</summary>
        public string ActionLabel { get; set; } = "Configurer";
    }
}
