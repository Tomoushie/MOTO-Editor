// Moto.Editor/Platforms/Windows/GlobalHotkeyService.cs
#if WINDOWS
using System;
using Microsoft.Maui.ApplicationModel;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Moto.Editor.Platforms.Windows
{
    /// <summary>
    /// Enregistre CTRL+SHIFT+I, CTRL+B et l'activation de la fenêtre (clic icône
    /// barre des tâches). Utilise KeyboardAccelerator WinUI : fonctionne quand la
    /// fenêtre a le focus.
    /// </summary>
    public partial class GlobalHotkeyService
    {
        /// <summary>
        /// À appeler une fois la fenêtre native disponible.
        /// </summary>
        public static void Register(
            Microsoft.UI.Xaml.Window window,
            Action onHotkey,
            Action onWindowActivated,
            Action onToggleExplorer = null,
            Action onBuild = null)
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
            }

            // 2. Activation de la fenêtre (clic sur l'icône barre des tâches).
            bool wasMinimizedOrDeactivated = false;

            window.Activated += (s, e) =>
            {
                var state = e.WindowActivationState;

                if (state == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
                {
                    wasMinimizedOrDeactivated = true;
                    return;
                }

                // La fenêtre revient au premier plan : on ouvre la barre IA.
                if (wasMinimizedOrDeactivated)
                {
                    wasMinimizedOrDeactivated = false;
                    MainThread.BeginInvokeOnMainThread(() => onWindowActivated?.Invoke());
                }
            };
        }
    }
}
#endif
