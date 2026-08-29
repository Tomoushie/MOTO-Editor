// Moto.Editor/Settings/SettingTemplateSelector.cs
using Microsoft.Maui.Controls;
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// Choisit le template selon le type du paramètre
    /// (toggle / stepper / picker / champ texte).
    /// </summary>
    public class SettingTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ToggleTemplate { get; set; }
        public DataTemplate IntTemplate { get; set; }
        public DataTemplate EnumTemplate { get; set; }
        public DataTemplate StringTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            return ((SettingItem)item).Def.Type switch
            {
                SettingType.Toggle => ToggleTemplate,
                SettingType.Int => IntTemplate,
                SettingType.Enum => EnumTemplate,
                _ => StringTemplate
            };
        }
    }
}
