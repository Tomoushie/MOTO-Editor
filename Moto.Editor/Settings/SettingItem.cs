// Moto.Editor/Settings/SettingItem.cs (v2 — ajout ActionCommand)
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    public class SettingItem : INotifyPropertyChanged
    {
        private readonly SettingsEngine _engine;

        public SettingDefinition Def { get; }

        /// <summary>Événement global déclenché par les boutons d'action.</summary>
        public static event Action<string> ActionRequested;

        public SettingItem(SettingDefinition def, SettingsEngine engine)
        {
            Def = def;
            _engine = engine;

            _engine.SettingChanged += (id, _) =>
            {
                if (string.Equals(id, Def.Id, StringComparison.OrdinalIgnoreCase))
                {
                    RaiseAll();
                }
            };
        }

        // ★ CORRECTION (02/09, revue croisée) : les 4 getters ci-dessous appelaient
        // GetBool(Def.Id)/GetInt(Def.Id)/GetString(Def.Id) SANS argument par
        // défaut — donc le repli codé en dur de SettingsEngine (false/0/""),
        // jamais Def.Default. Tant que le seul consommateur (SettingsPage) n'était
        // pas compilé, ce bug n'avait jamais été réellement exécuté. Avec
        // SettingsWindowView désormais rebranché sur le vrai catalogue (297
        // réglages), ouvrir n'importe quelle catégorie jamais touchée aurait
        // affiché la quasi-totalité des interrupteurs sur OFF (la plupart des
        // Toggle ont Default=true), les nombres à 0 et les menus déroulants
        // vides — alors que les valeurs par défaut réelles sont documentées dans
        // Def.Default depuis le début.
        public bool BoolValue
        {
            get => _engine.GetBool(Def.Id, Def.Default is bool b && b);
            set { _engine.Set(Def.Id, value); RaiseAll(); }
        }

        // ★ AJOUT (02/09, revue croisée) : setter ajouté — IntValue n'était QUE
        // lisible, seuls Increment/DecrementCommand (±Def.Step) pouvaient la
        // changer. Pour un réglage à large plage (ex. max_tokens 256-32000, pas
        // 256), atteindre une valeur précise aurait demandé des dizaines de clics
        // sans aucun moyen de taper directement un nombre — régression réelle par
        // rapport à l'ancien écran (simple champ texte numérique). Voir
        // SettingsWindowView.BuildInt : un Entry est maintenant lié ici en plus
        // des boutons −/+. Bornée à Min/Max comme Adjust() le fait déjà.
        public int IntValue
        {
            get => _engine.GetInt(Def.Id, Def.Default is int i ? i : 0);
            set { _engine.Set(Def.Id, Math.Clamp(value, Def.Min, Def.Max)); RaiseAll(); }
        }

        public string StringValue
        {
            get => _engine.GetString(Def.Id, Def.Default?.ToString() ?? "");
            set { _engine.Set(Def.Id, value); }
        }

        // ★ CORRECTION (02/09, revue croisée) : si la valeur déjà enregistrée ne
        // fait plus partie de Def.Options (ex. certaines options ont changé de
        // libellé entre l'ancien écran maison et le vrai catalogue — "Éco"→"Eco",
        // "System" retiré de theme_mode), un Picker.SelectedItem sur une valeur
        // absente de sa liste s'affiche VIDE plutôt que de planter — repli sur la
        // première option réelle pour l'AFFICHAGE uniquement (n'écrase PAS la
        // valeur enregistrée tant que l'utilisateur ne choisit rien lui-même).
        public string OptionValue
        {
            get
            {
                var stored = _engine.GetString(Def.Id, Def.Default?.ToString() ?? "");
                if (Def.Options.Contains(stored)) return stored;
                return Def.Options.Count > 0 ? Def.Options[0] : stored;
            }
            set { _engine.Set(Def.Id, value); RaiseAll(); }
        }

        /// <summary>★ AJOUT (02/09, réglages IA cachés) : SettingType.Double —
        /// valeurs fractionnaires (ex. seuils 0.0-1.0). Bornée à MinDouble/MaxDouble,
        /// même principe que IntValue/Min/Max.</summary>
        public double DoubleValue
        {
            get => _engine.Get(Def.Id, Def.Default is double d ? d : 0.0);
            set { _engine.Set(Def.Id, Math.Clamp(value, Def.MinDouble, Def.MaxDouble)); RaiseAll(); }
        }

        public string ActionLabel => Def.ActionLabel;

        public ICommand IncrementCommand => new Command(() => Adjust(+Def.Step));
        public ICommand DecrementCommand => new Command(() => Adjust(-Def.Step));

        /// <summary>Commande du bouton pour les paramètres de type Action.</summary>
        public ICommand ActionCommand => new Command(() => ActionRequested?.Invoke(Def.ActionId));

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
            OnPropertyChanged(nameof(DoubleValue));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
