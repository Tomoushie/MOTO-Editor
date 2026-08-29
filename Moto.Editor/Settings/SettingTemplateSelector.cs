// Moto.Editor/Settings/SettingTemplateSelector.cs (v2)
using Microsoft.Maui.Controls;
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    public class SettingTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ToggleTemplate { get; set; }
        public DataTemplate IntTemplate { get; set; }
        public DataTemplate EnumTemplate { get; set; }
        public DataTemplate StringTemplate { get; set; }
        public DataTemplate ActionTemplate { get; set; }   // Nouveau

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            return ((SettingItem)item).Def.Type switch
            {
                SettingType.Toggle => ToggleTemplate,
                SettingType.Int => IntTemplate,
                SettingType.Enum => EnumTemplate,
                SettingType.Action => ActionTemplate,
                _ => StringTemplate
            };
        }
    }
}
