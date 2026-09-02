// Moto.Editor/Views/SettingsWindowView.xaml.cs
// ★ AJOUT (31/08) : fenêtre de Réglages façon Zed (capture fournie par Tom) —
// flottante, déplaçable, redimensionnable.
//
// ★ RÉÉCRITURE (02/09, chantier Réglages 100+) : la fenêtre (chrome flottant)
// est inchangée ; son CONTENU vient maintenant du vrai catalogue du logiciel
// (Moto.Core.Settings.SettingsCatalog.All), pas d'une liste d'~95 réglages
// écrite à la main dans ce fichier (SettingKind/SettingDef locaux, supprimés).
//
// Contexte trouvé en creusant : le vrai catalogue existait déjà, riche et
// bien structuré (Id/Category/Section/Title/Description/Type/Min/Max/
// Options), avec un écran GÉNÉRIQUE déjà construit pour l'exposer
// (Moto.Editor.Pages.SettingsPage — recherche, sections, rendu par type via
// Moto.Editor.Settings.SettingItem + SettingTemplateSelector) — mais cet
// écran est explicitement exclu de la compilation (Moto.Editor.csproj :
// "Pages jamais navigables"), donc invisible dans l'appli qui tourne
// réellement. Plutôt que d'activer SettingsPage tel quel (ce qui aurait
// remplacé la fenêtre flottante façon Zed, explicitement demandée par Tom,
// par une page plein écran classique), le contenu de SettingsPage est ici
// adapté au chrome flottant déjà en place : même source de données
// (SettingsCatalog.All) et même mécanisme de persistance (SettingItem), rendu
// en C# (comme avant) plutôt qu'en CollectionView à gabarits — la structure
// catégorie + sous-en-têtes de section s'imbrique plus naturellement ainsi,
// et ça reste dans la convention déjà établie de ce fichier ("généré en
// code : des dizaines de lignes similaires écrites une par une en XAML
// auraient été bien plus risquées à relire").
//
// Honnêteté sur la portée : la plupart des 297 réglages du catalogue
// n'ont PAS de fonctionnalité réelle branchée derrière (voir SettingsApplier
// — 4 seulement sont réellement appliqués en direct aujourd'hui). Ce constat
// préexiste à cette passe (déjà vrai pour les ~95 réglages remplacés) —
// construire les FONCTIONNALITÉS elles-mêmes est un chantier séparé, plus
// grand. Ce qui change ici : la liste affichée est enfin LA VRAIE LISTE DU
// LOGICIEL (SettingsCatalog.All) et cherchable — plus une vitrine séparée.
// ★ CORRECTION (02/09, revue croisée) : "complète" retiré de la phrase
// ci-dessus — il existe un TROISIÈME système, séparé, de ~126 réglages IA
// avancés réellement utilisés par le moteur (SettingItem<T>, dans les
// fichiers SettingsCatalog.Ai.*/Collab/DevOps/Git/Mcp/Marketplace/Editor.Ux),
// jamais ajoutés à SettingsCatalog.All et donc toujours invisibles ici — Tom
// a explicitement choisi de les laisser de côté pour cette passe (chantier
// séparé, plus gros, si un jour souhaité).
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Moto.Core.Settings;
using Moto.Editor.Settings;

namespace Moto.Editor.Views
{
    public partial class SettingsWindowView : ContentView
    {
        /// <summary>
        /// Ids dont l'effet réel est vérifié ailleurs dans le logiciel (voir
        /// MainPage.xaml.cs : SettingsWindow.RealSettingChanged). Inchangé
        /// depuis la version précédente — ces 5 ids existent à l'identique
        /// dans le vrai catalogue (aucun n'a été renommé/retiré).
        /// </summary>
        private static readonly HashSet<string> RealEffectKeys = new()
        {
            "theme_mode", "buffer_font_size", "minimap_show", "terminal_show", "power_mode"
        };

        private readonly List<string> _categories;
        private string _currentCategory;
        private string _search = string.Empty;
        private double _startX, _startY, _startW, _startH;

        // ★ AJOUT (02/09, revue croisée) : un SettingItem par Id, réutilisé entre
        // les rendus (changement de catégorie, chaque frappe dans la recherche,
        // chaque réouverture) plutôt que reconstruit à chaque fois. Le
        // constructeur de SettingItem s'abonne à SettingsEngine.Shared.SettingChanged
        // (statique, durée de vie de l'appli) sans jamais se désabonner — en
        // créer un neuf à chaque rendu aurait accumulé des abonnements morts sans
        // limite (même famille de fuite que OnPlayClicked, déjà corrigée ailleurs
        // cette session). Le cache borne le nombre total d'abonnements au nombre
        // de réglages RÉELLEMENT affichés au moins une fois dans cette fenêtre
        // (≤297), pas au nombre de rendus.
        private readonly Dictionary<string, SettingItem> _items = new();

        /// <summary>Déclenché pour les réglages à effet réel (mêmes id que SettingsMenuView).</summary>
        public event Action<string, object>? RealSettingChanged;

        public SettingsWindowView()
        {
            InitializeComponent();

            // Ordre de déclaration du catalogue (pas alphabétique) — reflète
            // l'ordre choisi par SettingsCatalog.cs (Général, Apparence,
            // Raccourcis, Éditeur...), pas un tri arbitraire.
            _categories = SettingsCatalog.All.Select(d => d.Category).Distinct().ToList();
            CategoryList.ItemsSource = _categories;

            // ★ Notifie RealSettingChanged pour TOUT changement d'un id de
            // RealEffectKeys, quelle que soit la ligne/le contrôle d'où il
            // vient — remplace l'ancien Persist() dédié (retiré : la
            // persistance passe maintenant par SettingItem, réutilisé tel
            // quel plutôt que dupliqué).
            SettingsEngine.Shared.SettingChanged += (id, value) =>
            {
                if (RealEffectKeys.Contains(id))
                    RealSettingChanged?.Invoke(id, value);
            };

            _currentCategory = _categories.FirstOrDefault() ?? "";
            CategoryList.SelectedItem = _currentCategory;
            // ★ Filet de sécurité : SelectionChanged n'est pas garanti de se
            // déclencher pour une sélection posée par code avant que le contrôle
            // soit dans l'arbre visuel (fenêtre encore IsVisible=False à cet
            // instant) — appelé directement pour ne jamais laisser le panneau de
            // détail vide à la première ouverture.
            RenderCategory(_currentCategory);
        }

        /// <summary>
        /// ★ CORRECTION (31/08) : Tom signale que l'engrenage n'ouvrait "aucune
        /// fenêtre" — aucun bug trouvé dans le chemin d'ouverture lui-même (vérifié
        /// ligne par ligne), mais WindowFrame.TranslationX/Y (glisser) et
        /// WidthRequest/HeightRequest (redimensionner) ne sont RÉINITIALISÉS nulle
        /// part : un seul glissement accidentel avant que Tom ne comprenne que rien
        /// n'était encore ouvert aurait suffi à repositionner la fenêtre hors-écran
        /// pour TOUTES les ouvertures suivantes (IsVisible=true, mais invisible à
        /// l'œil). Réinitialisé par précaution à chaque Show(), que ce soit la
        /// cause réelle ou non — de toute façon la bonne pratique pour un "rouvrir".
        /// Accepte aussi une catégorie cible (utilisé par le menu ⚙ : "Thèmes" ouvre
        /// direct sur Appearance, "Raccourcis" sur Keymap, etc.).
        /// ★ CORRECTION (02/09) : les catégories cibles sont maintenant les vraies
        /// chaînes françaises du catalogue ("Général"/"Apparence"/"Raccourcis"),
        /// pas les anciennes clés anglaises maison — voir MainPage.Routing.cs.
        /// </summary>
        public void Show(string category = "Général")
        {
            WindowFrame.TranslationX = 0;
            WindowFrame.TranslationY = 0;
            WindowFrame.WidthRequest = 900;
            WindowFrame.HeightRequest = 620;
            IsVisible = true;

            SearchEntry.Text = string.Empty;
            _search = string.Empty;

            if (!_categories.Contains(category))
                category = _categories.FirstOrDefault() ?? category;

            CategoryList.SelectedItem = category;
            RenderCategory(category);
        }

        private void OnCloseClicked(object sender, EventArgs e) => IsVisible = false;

        private void OnCategorySelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count == 0 || e.CurrentSelection[0] is not string category) return;
            RenderCategory(category);
        }

        /// <summary>
        /// ★ AJOUT (02/09) : recherche globale, tous catégories confondues —
        /// indispensable à 297 réglages (contre ~95 avant, où l'absence de
        /// recherche passait encore). Vide → revient à la catégorie
        /// sélectionnée dans la liste de gauche.
        /// </summary>
        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            _search = e.NewTextValue ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_search))
                RenderCategory(_currentCategory);
            else
                RenderSearchResults(_search);
        }

        // ------------------------------------------------------------------
        // Déplacement (barre de titre) / redimensionnement (coin bas-droit)
        // ------------------------------------------------------------------

        private void OnDragPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startX = WindowFrame.TranslationX;
                    _startY = WindowFrame.TranslationY;
                    break;
                case GestureStatus.Running:
                    WindowFrame.TranslationX = _startX + e.TotalX;
                    WindowFrame.TranslationY = _startY + e.TotalY;
                    break;
            }
        }

        private void OnResizePanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startW = WindowFrame.Width > 0 ? WindowFrame.Width : WindowFrame.WidthRequest;
                    _startH = WindowFrame.Height > 0 ? WindowFrame.Height : WindowFrame.HeightRequest;
                    break;
                case GestureStatus.Running:
                    WindowFrame.WidthRequest = Math.Max(WindowFrame.MinimumWidthRequest, _startW + e.TotalX);
                    WindowFrame.HeightRequest = Math.Max(WindowFrame.MinimumHeightRequest, _startH + e.TotalY);
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Rendu (en code — voir le commentaire en tête de fichier)
        // ------------------------------------------------------------------

        /// <summary>Affiche tous les réglages d'une catégorie, groupés par Section
        /// (sous-en-têtes), dans l'ordre de déclaration du catalogue.
        /// ★ CORRECTION (02/09, revue croisée) : GroupBy plutôt qu'un simple "la
        /// section a changé depuis la dernière ligne" — cette dernière hypothèse
        /// suppose que les entrées d'une même section sont déjà contiguës dans
        /// SettingsCatalog.All, ce qui s'est révélé faux à 2 endroits pour la
        /// catégorie "Agent" (une interversion déjà présente dans le catalogue
        /// avant cette passe : Conversation/Génération/Conversation ; une
        /// introduite en reconnectant les 7 catégories orphelines : Performance et
        /// Documentation désormais coupées en deux endroits éloignés de la liste,
        /// chacune affichant son sous-en-tête deux fois). GroupBy(d => d.Section)
        /// préserve l'ordre de PREMIÈRE apparition de chaque section (comportement
        /// documenté de LINQ) et regroupe toutes ses entrées ensemble, quel que
        /// soit leur ordre réel dans la liste source — aucun réglage ne change de
        /// section affichée, seul le doublon de titre disparaît.
        /// </summary>
        private void RenderCategory(string category)
        {
            _currentCategory = category;
            DetailHost.Children.Clear();
            DetailHost.Children.Add(BuildTitle(category));

            foreach (var group in SettingsCatalog.All.Where(d => d.Category == category).GroupBy(d => d.Section))
            {
                DetailHost.Children.Add(BuildSectionHeader(group.Key));
                foreach (var def in group)
                    DetailHost.Children.Add(BuildRow(def));
            }
        }

        /// <summary>Résultats de recherche, toutes catégories confondues, groupés
        /// par "Catégorie › Section" pour rester lisible même dispersés.</summary>
        private void RenderSearchResults(string search)
        {
            DetailHost.Children.Clear();
            DetailHost.Children.Add(BuildTitle($"Résultats pour « {search} »"));

            var matches = SettingsCatalog.All
                .Where(d =>
                    d.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    d.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    d.Id.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Category).ThenBy(d => d.Section)
                .ToList();

            if (matches.Count == 0)
            {
                DetailHost.Children.Add(new Label
                {
                    Text = "Aucun résultat.",
                    FontSize = 12,
                    TextColor = (Color)Application.Current!.Resources["Txt2"]
                });
                return;
            }

            string? lastGroup = null;
            foreach (var def in matches)
            {
                var groupKey = $"{def.Category} › {def.Section}";
                if (groupKey != lastGroup)
                {
                    DetailHost.Children.Add(BuildSectionHeader(groupKey));
                    lastGroup = groupKey;
                }
                DetailHost.Children.Add(BuildRow(def));
            }
        }

        private Label BuildTitle(string text) => new()
        {
            Text = text,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = (Color)Application.Current!.Resources["Txt1"]
        };

        private Label BuildSectionHeader(string section) => new()
        {
            Text = section,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = (Color)Application.Current!.Resources["Accent"],
            Margin = new Thickness(0, 12, 0, 0)
        };

        /// <summary>Voir le commentaire du champ _items — un seul SettingItem par
        /// Id pour toute la durée de vie de cette fenêtre.</summary>
        private SettingItem GetOrCreateItem(SettingDefinition def)
        {
            if (!_items.TryGetValue(def.Id, out var item))
            {
                item = new SettingItem(def, SettingsEngine.Shared);
                _items[def.Id] = item;
            }
            return item;
        }

        /// <summary>
        /// Une ligne = un SettingDefinition du vrai catalogue, réellement
        /// lu/écrit via SettingItem (même classe que SettingsPage, éprouvée) —
        /// pas de Get/Set maison dupliqué ici.
        /// </summary>
        private View BuildRow(SettingDefinition def)
        {
            var txt1 = (Color)Application.Current!.Resources["Txt1"];
            var txt2 = (Color)Application.Current!.Resources["Txt2"];
            var border = (Color)Application.Current!.Resources["BorderCol"];

            var item = GetOrCreateItem(def);

            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };

            var textCol = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            textCol.Children.Add(new Label { Text = def.Title, FontSize = 13, TextColor = txt1 });
            textCol.Children.Add(new Label { Text = def.Description, FontSize = 11, TextColor = txt2 });
            grid.Add(textCol, 0, 0);

            View control = def.Type switch
            {
                SettingType.Toggle => BuildToggle(item),
                SettingType.Int => BuildInt(item),
                SettingType.Enum => BuildEnum(item, def),
                SettingType.Action => BuildAction(item, def),
                SettingType.Double => BuildDouble(item),
                _ => BuildString(item),
            };
            control.VerticalOptions = LayoutOptions.Center;
            grid.Add(control, 1, 0);

            return new Border
            {
                Padding = new Thickness(0, 0, 0, 14),
                Stroke = Colors.Transparent,
                Content = new VerticalStackLayout
                {
                    Children =
                    {
                        grid,
                        new BoxView { HeightRequest = 1, Color = border, Margin = new Thickness(0, 12, 0, 0) }
                    }
                }
            };
        }

        private static Switch BuildToggle(SettingItem item)
        {
            var sw = new Switch { BindingContext = item };
            sw.SetBinding(Switch.IsToggledProperty, nameof(SettingItem.BoolValue));
            return sw;
        }

        /// <summary>
        /// ★ CORRECTION (02/09, revue croisée) : Entry ajouté à côté des boutons
        /// −/+ — IntValue avait un setter côté SettingItem mais rien ici ne
        /// l'utilisait, forçant à cliquer +/- pas à pas (parfois des dizaines de
        /// fois) pour atteindre une valeur précise sur les réglages à large plage.
        /// Les deux coexistent : l'Entry pour taper directement, les boutons pour
        /// les petits ajustements rapides — les deux passent par la même
        /// propriété IntValue (bornée à Min/Max côté SettingItem), donc restent
        /// synchronisés entre eux.
        /// </summary>
        private static HorizontalStackLayout BuildInt(SettingItem item)
        {
            var minus = new Button { Text = "−", Padding = new Thickness(8, 2), FontSize = 12 };
            minus.SetBinding(Button.CommandProperty, nameof(SettingItem.DecrementCommand));

            var entry = new Entry
            {
                WidthRequest = 70, Keyboard = Keyboard.Numeric,
                HorizontalTextAlignment = TextAlignment.Center
            };
            entry.SetBinding(Entry.TextProperty, nameof(SettingItem.IntValue));

            var plus = new Button { Text = "+", Padding = new Thickness(8, 2), FontSize = 12 };
            plus.SetBinding(Button.CommandProperty, nameof(SettingItem.IncrementCommand));

            return new HorizontalStackLayout { Spacing = 6, BindingContext = item, Children = { minus, entry, plus } };
        }

        private static Picker BuildEnum(SettingItem item, SettingDefinition def)
        {
            var picker = new Picker { WidthRequest = 180, BindingContext = item };
            foreach (var opt in def.Options) picker.Items.Add(opt);
            picker.SetBinding(Picker.SelectedItemProperty, nameof(SettingItem.OptionValue));
            return picker;
        }

        private static Entry BuildString(SettingItem item)
        {
            var entry = new Entry { WidthRequest = 200, BindingContext = item };
            entry.SetBinding(Entry.TextProperty, nameof(SettingItem.StringValue));
            return entry;
        }

        private static Button BuildAction(SettingItem item, SettingDefinition def)
        {
            var btn = new Button { Text = def.ActionLabel, Padding = new Thickness(12, 4), BindingContext = item };
            btn.SetBinding(Button.CommandProperty, nameof(SettingItem.ActionCommand));
            return btn;
        }

        /// <summary>
        /// ★ AJOUT (02/09, réglages IA cachés) : SettingType.Double. Analyse
        /// manuelle plutôt qu'une simple liaison Entry.Text↔DoubleValue — un
        /// Français tapant "0,7" (virgule) échouerait avec une conversion
        /// implicite qui suppose souvent le point comme séparateur ; on essaie
        /// d'abord le point (culture invariante, cas le plus courant pour un
        /// réglage technique), puis la virgule (culture actuelle de Windows) en
        /// repli — même prudence que l'ancien BuildNumber de ce fichier (avant
        /// la réécriture), qui gérait déjà ses nombres à la main.
        /// </summary>
        private static Entry BuildDouble(SettingItem item)
        {
            var entry = new Entry
            {
                WidthRequest = 90, Keyboard = Keyboard.Numeric,
                HorizontalTextAlignment = TextAlignment.Center,
                Text = item.DoubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            void Commit()
            {
                // ★ CORRECTION (02/09, revue croisée) : deux trous trouvés ici —
                // (1) "NaN" est un texte valide pour TryParse mais Math.Clamp le
                // laisse passer tel quel (NaN < min et NaN > max valent tous les
                // deux false), donc la valeur écrite pouvait devenir NaN et casser
                // silencieusement tout consommateur réel (ex. PerfGateService) ;
                // (2) un texte invalide (vide, "abc"...) ne remettait jamais
                // l'affichage à la vraie valeur stockée, laissant la case
                // visuellement fausse jusqu'à la reconstruction de la ligne.
                if ((double.TryParse(entry.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)
                        || double.TryParse(entry.Text, out v))
                    && !double.IsNaN(v) && !double.IsInfinity(v))
                {
                    item.DoubleValue = v;
                }

                entry.Text = item.DoubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            entry.Completed += (s, e) => Commit();
            entry.Unfocused += (s, e) => Commit();
            return entry;
        }
    }
}
