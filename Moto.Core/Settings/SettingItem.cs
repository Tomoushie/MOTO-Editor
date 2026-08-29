// Moto.Editor/Settings/SettingItem.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// Wrapper view-model d'un paramètre pour l'UI.
    /// Expose des propriétés typées bindables et des commandes stepper.
    /// </summary>
    public class SettingItem : INotifyPropertyChanged
    {
        private readonly SettingsEngine _engine;

        public SettingDefinition Def { get; }

        public SettingItem(SettingDefinition def, SettingsEngine engine)
        {
            Def = def;
            _engine = engine;

            // Rafraîchit l'UI si le paramètre change ailleurs (reset, json...).
            _engine.SettingChanged += (id, _) =>
            {
                if (string.Equals(id, Def.Id, StringComparison.OrdinalIgnoreCase))
                {
                    RaiseAll();
                }
            };
        }

        public bool BoolValue
        {
            get => _engine.GetBool(Def.Id);
            set { _engine.Set(Def.Id, value); RaiseAll(); }
        }

        public int IntValue => _engine.GetInt(Def.Id);

        public string StringValue
        {
            get => _engine.GetString(Def.Id);
            set { _engine.Set(Def.Id, value); }
        }

        public string OptionValue
        {
            get => _engine.GetString(Def.Id);
            set { _engine.Set(Def.Id, value); RaiseAll(); }
        }

        public ICommand IncrementCommand => new Command(() => Adjust(+Def.Step));
        public ICommand DecrementCommand => new Command(() => Adjust(-Def.Step));

        private void Adjust(int delta)
        {
            var next = Math.Clamp(IntValue + delta, Def.Min, Def.Max);
            _engine.Set(Def.Id, next);
            RaiseAll();
        }

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(BoolValue));
            OnPropertyChanged(nameof(IntValue));
            OnPropertyChanged(nameof(StringValue));
            OnPropertyChanged(nameof(OptionValue));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
