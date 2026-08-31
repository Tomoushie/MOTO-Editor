// Moto.Editor/Controls/AiComposerBarView.xaml.cs
// ★ AJOUT/RÉVISION (31/08) : voir le commentaire du .xaml pour le contexte complet.
using System;
using Microsoft.Maui.Controls;
using Moto.Core.Settings;

namespace Moto.Editor.Controls
{
    public partial class AiComposerBarView : ContentView
    {
        // ★ "MOTO AI — Par défaut" ajouté en tête, demandé explicitement par Tom pour
        // le cas où aucun modèle n'est choisi. Correspond à la valeur par défaut déjà
        // réelle du réglage (ai.default_model = "MOTO interne").
        private static readonly string[] Models = { "MOTO AI — Par défaut", "Ollama", "OpenAI", "Anthropic", "Mistral" };
        private static readonly string[] EffortLevels = { "Éco", "Balanced", "Ultra" };

        /// <summary>Levée pour "ai"/"cortex" — MainPage route vers OnActivitySelected (code inchangé).</summary>
        public event Action<string>? PanelRequested;

        /// <summary>
        /// ★ AJOUT (31/08) : "+" — pas de vrai système de pièces jointes/connecteurs
        /// dans MOTO Editor ; relayé vers la même action que "Rechercher projet"
        /// (sélecteur de dossier) plutôt qu'un bouton qui ne ferait rien.
        /// </summary>
        public event Action? AttachRequested;

        private readonly BudgetRingDrawable _budgetDrawable = new();

        public AiComposerBarView()
        {
            InitializeComponent();
            BuildModelList();
            BuildEffortList();
            _ = BuildMicListAsync();

            var currentModel = SettingsEngine.Shared.GetString("ai.default_model", "MOTO interne");
            ModelLabel.Text = currentModel == "MOTO interne" ? "MOTO AI" : currentModel;

            var currentEffort = SettingsEngine.Shared.GetString("power_mode", "Balanced");
            EffortLabel.Text = $"Puissance · {currentEffort}";

            BudgetRing.Drawable = _budgetDrawable;

            // ★ AJOUT (31/08, points 1/3) : zone grise arrondie au survol/clic. Les
            // boutons qui ont déjà un fond BgPanel au repos (IA/Cortex/Modèle/
            // Puissance) reviennent à BgPanel en sortie de survol, pas à Transparent.
            var bgPanel = (Color)Application.Current!.Resources["BgPanel"];
            HoverEffects.Attach(BtnAttach);
            HoverEffects.Attach(BtnMic);
            HoverEffects.Attach(BtnMicChevron);
            HoverEffects.Attach(BtnAi, idleColor: bgPanel);
            HoverEffects.Attach(BtnCortex, idleColor: bgPanel);
            HoverEffects.Attach(BtnModel, idleColor: bgPanel);
            HoverEffects.Attach(BtnEffort, idleColor: bgPanel);
        }

        private void BuildModelList()
        {
            var current = SettingsEngine.Shared.GetString("ai.default_model", "MOTO interne");
            ModelList.Children.Clear();
            foreach (var model in Models)
            {
                // "MOTO AI — Par défaut" représente la valeur réelle "MOTO interne".
                var realValue = model == "MOTO AI — Par défaut" ? "MOTO interne" : model;
                ModelList.Children.Add(MakeRow(model, realValue == current, () =>
                {
                    SettingsEngine.Shared.Set("ai.default_model", realValue);
                    ModelLabel.Text = realValue == "MOTO interne" ? "MOTO AI" : realValue;
                    ClosePopups();
                    BuildModelList(); // rafraîchit la coche
                }));
            }
        }

        private void BuildEffortList()
        {
            var current = SettingsEngine.Shared.GetString("power_mode", "Balanced");
            EffortList.Children.Clear();
            foreach (var level in EffortLevels)
            {
                EffortList.Children.Add(MakeRow(level, level == current, () =>
                {
                    SettingsEngine.Shared.Set("power_mode", level);
                    EffortLabel.Text = $"Puissance · {level}";
                    ClosePopups();
                    BuildEffortList();
                }));
            }
        }

        /// <summary>
        /// ★ Honnêteté sur la portée : cette liste énumère les VRAIS micros détectés par
        /// Windows (Windows.Devices.Enumeration, API vérifiée). Choisir un micro ici ne
        /// fait encore RIEN de réel : la dictée elle-même (bouton micro) n'est pas
        /// câblée à une vraie reconnaissance vocale dans cette passe — voir
        /// OnMicButtonTapped.
        /// </summary>
        private async System.Threading.Tasks.Task BuildMicListAsync()
        {
            MicList.Children.Clear();
            MicList.Children.Add(new Label { Text = "Microphone", FontSize = 11, Opacity = 0.6, Margin = new Thickness(8, 4) });

#if WINDOWS
            try
            {
                var devices = await global::Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
                    global::Windows.Devices.Enumeration.DeviceClass.AudioCapture);
                if (devices.Count == 0)
                {
                    MicList.Children.Add(MakeRow("Aucun micro détecté", false, null));
                    return;
                }
                foreach (var device in devices)
                {
                    var name = device.Name;
                    MicList.Children.Add(MakeRow(name, false, () =>
                    {
                        SettingsEngine.Shared.Set("ai.bar.mic_device", name);
                        ClosePopups();
                    }));
                }
            }
            catch
            {
                MicList.Children.Add(MakeRow("Micro par défaut du système", false, null));
            }
#else
            MicList.Children.Add(MakeRow("Micro par défaut du système", false, null));
#endif
        }

        private static Border MakeRow(string label, bool selected, Action? onTap)
        {
            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            grid.Add(new Label { Text = label, FontSize = 12, VerticalOptions = LayoutOptions.Center }, 0, 0);
            if (selected)
                grid.Add(new Label { Text = "✓", FontSize = 12, VerticalOptions = LayoutOptions.Center }, 1, 0);

            var border = new Border { BackgroundColor = Colors.Transparent, Padding = new Thickness(8, 6), StrokeThickness = 0, Content = grid };
            if (onTap != null)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => onTap();
                border.GestureRecognizers.Add(tap);
            }
            return border;
        }

        private void ClosePopups()
        {
            ModelPopup.IsVisible = false;
            EffortPopup.IsVisible = false;
            MicPopup.IsVisible = false;
            BudgetPopup.IsVisible = false;
        }

        private void Toggle(Border popup)
        {
            bool target = !popup.IsVisible;
            ClosePopups();
            popup.IsVisible = target;
        }

        private void OnModelButtonTapped(object sender, TappedEventArgs e) => Toggle(ModelPopup);
        private void OnEffortButtonTapped(object sender, TappedEventArgs e) => Toggle(EffortPopup);
        private void OnMicChevronTapped(object sender, TappedEventArgs e) => Toggle(MicPopup);
        private void OnBudgetTapped(object sender, TappedEventArgs e) => Toggle(BudgetPopup);

        private void OnAttachTapped(object sender, TappedEventArgs e)
        {
            ClosePopups();
            AttachRequested?.Invoke();
        }

        /// <summary>
        /// ★ Honnêteté sur la portée (point 4 de Tom) : pas de vraie reconnaissance
        /// vocale câblée dans cette passe (ça demande une vraie vérification de
        /// permissions/manifeste que je ne peux pas tester à l'aveugle) — message clair
        /// plutôt qu'un bouton qui a l'air de marcher sans rien faire.
        /// </summary>
        private async void OnMicButtonTapped(object sender, TappedEventArgs e)
        {
            ClosePopups();
            var page = Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0].Page : null;
            if (page != null)
                await page.DisplayAlert("Dictée", "Pas encore disponible : la liste des micros est réelle, mais la reconnaissance vocale elle-même n'est pas encore câblée.", "OK");
        }

        private void OnAiTapped(object sender, TappedEventArgs e)
        {
            ClosePopups();
            PanelRequested?.Invoke("ai");
        }

        private void OnCortexTapped(object sender, TappedEventArgs e)
        {
            ClosePopups();
            PanelRequested?.Invoke("cortex");
        }
    }
}
