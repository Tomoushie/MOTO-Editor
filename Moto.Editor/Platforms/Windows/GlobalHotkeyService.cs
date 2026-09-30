// Moto.Editor/Platforms/Windows/GlobalHotkeyService.cs
#if WINDOWS
using System;
using Microsoft.Maui.ApplicationModel;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Moto.Editor.Platforms.Windows
{
    /// <summary>
    /// Enregistre CTRL+SHIFT+I, CTRL+B, F5, F11 et CTRL+S. Utilise KeyboardAccelerator
    /// WinUI : fonctionne quand la fenêtre a le focus.
    /// </summary>
    public partial class GlobalHotkeyService
    {
        /// <summary>
        /// À appeler une fois la fenêtre native disponible.
        /// </summary>
        public static void Register(
            Microsoft.UI.Xaml.Window window,
            Action onHotkey,
            Action onToggleExplorer = null,
            Action onBuild = null,
            Action onToggleFullScreen = null,
            Action onSave = null)
        {
            if (window == null)
            {
                return;
            }

            // 1. Raccourci CTRL+SHIFT+I
            if (window.Content is Microsoft.UI.Xaml.UIElement root)
            {
                var accelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
                {
                    Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
                    Key = VirtualKey.I
                };

                accelerator.Invoked += (s, e) =>
                {
                    MainThread.BeginInvokeOnMainThread(() => onHotkey?.Invoke());
                    e.Handled = true;
                };

                root.KeyboardAccelerators.Add(accelerator);

                // ★ AJOUT (02/09, état des lieux) : CTRL+B ("Basculer l'explorateur")
                // déjà annoncé comme raccourci dans CommandPaletteEngine.cs (label
                // affiché uniquement, jamais un vrai raccourci clavier — vérifié par
                // recherche complète, aucun VirtualKey.B nulle part avant ceci). La
                // route (view.explorer -> ToggleSide) existait déjà et marchait, seul
                // l'écouteur manquait. Même mécanisme que CTRL+SHIFT+I ci-dessus.
                if (onToggleExplorer != null)
                {
                    var explorerAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
                    {
                        Modifiers = VirtualKeyModifiers.Control,
                        Key = VirtualKey.B
                    };

                    explorerAccelerator.Invoked += (s, e) =>
                    {
                        MainThread.BeginInvokeOnMainThread(() => onToggleExplorer.Invoke());
                        e.Handled = true;
                    };

                    root.KeyboardAccelerators.Add(explorerAccelerator);
                }

                // ★ AJOUT (02/09, "vrai registre de commandes" — retour de test) : F5
                // ("Compiler") était affiché comme raccourci dans CommandPaletteEngine.cs
                // et dans SettingsCatalog, mais AUCUN VirtualKey.F5 n'existait nulle part
                // dans le dépôt avant ceci (vérifié par recherche complète) — confirmé
                // cassé par Tom en testant le nouveau CommandRegistry. Même mécanisme que
                // Ctrl+B ci-dessus, aucun modificateur (F5 seule).
                if (onBuild != null)
                {
                    var buildAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
                    {
                        Key = VirtualKey.F5
                    };

                    buildAccelerator.Invoked += (s, e) =>
                    {
                        MainThread.BeginInvokeOnMainThread(() => onBuild.Invoke());
                        e.Handled = true;
                    };

                    root.KeyboardAccelerators.Add(buildAccelerator);
                }

                // ★ AJOUT (08/09, chantier "rendu 100% custom", point "plein écran
                // manuel") : F11, sans modificateur — comme la quasi-totalité des
                // éditeurs/navigateurs. N'existait nulle part avant (vérifié par
                // recherche complète : 0 VirtualKey.F11 dans tout le dépôt).
                if (onToggleFullScreen != null)
                {
                    var fullScreenAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
                    {
                        Key = VirtualKey.F11
                    };

                    fullScreenAccelerator.Invoked += (s, e) =>
                    {
                        MainThread.BeginInvokeOnMainThread(() => onToggleFullScreen.Invoke());
                        e.Handled = true;
                    };

                    root.KeyboardAccelerators.Add(fullScreenAccelerator);
                }

                // ★ AJOUT (25/09, passe « moyen → élevé ») : Ctrl+S. La commande « file.save » existait et le conseil affiché
                // disait « Ctrl+S sauvegarde », mais aucune touche ne l'appelait (0 VirtualKey.S dans le dépôt). Quand le
                // curseur est dans le code, c'est l'éditeur WebView qui transmet Ctrl+S (voir MainPage.Shortcuts.cs).
                if (onSave != null)
                {
                    var saveAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
                    {
                        Modifiers = VirtualKeyModifiers.Control,
                        Key = VirtualKey.S
                    };

                    saveAccelerator.Invoked += (s, e) =>
                    {
                        MainThread.BeginInvokeOnMainThread(() => onSave.Invoke());
                        e.Handled = true;
                    };

                    root.KeyboardAccelerators.Add(saveAccelerator);
                }
            }

            // ★ RETIRÉ (27/09, point 3 de Tom) : « 2. Activation de la fenêtre » rouvrait la barre IA à chaque retour dans MOTO
            // (alt-tab, clic sur l'icône). La barre ne vient plus que sur demande.
        }
    }
}
#endif
