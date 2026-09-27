// Moto.Editor/MainPage.Shortcuts.cs
// ★ AJOUT (25/09, passe « moyen → élevé ») : point d'entrée UNIQUE des raccourcis clavier de la fenêtre.
// Deux chemins y mènent : les accélérateurs XAML (GlobalHotkeyService, OnWindowsPreviewKeyDown) quand le focus est ailleurs,
// et l'éditeur WebView (EditorPane.ShortcutPressed) quand le curseur est dans le code — les touches y vont alors à Chromium,
// pas au XAML. Si une même frappe arrivait par les deux chemins, la seconde est ignorée (fenêtre de 250 ms) : sans ça,
// Ctrl+B basculerait deux fois l'explorateur (= rien).
// Ctrl+S n'existait nulle part avant ce jour (« file.save » était enregistré, mais aucune touche ne l'appelait).
// Aussi ici (même objectif, une frappe fluide) : l'apprentissage Cortex reporté après la dernière frappe.
using System;

namespace Moto.Editor
{
    public partial class MainPage
    {
        private string? _lastShortcut;
        private long _lastShortcutTick;
        private System.Threading.CancellationTokenSource? _cortexLearnCts;

        /// <summary>
        /// ★ AJOUT (25/09) : Cortex apprend le style du fichier 1,5 s après la dernière frappe, pas à chaque touche
        /// (mesuré sur un fichier de 70 Ko : chaque envoi de texte bloquait le fil de l'interface — voir le rapport de la passe).
        /// </summary>
        private void ScheduleCortexLearning(string path, string text)
        {
            _cortexLearnCts?.Cancel();
            var cts = _cortexLearnCts = new System.Threading.CancellationTokenSource();
            _ = System.Threading.Tasks.Task.Delay(1500, cts.Token).ContinueWith(t =>
            {
                if (!t.IsCanceled) MainThread.BeginInvokeOnMainThread(() => _cortex?.LearnFromCode(path, text));
            }, System.Threading.Tasks.TaskScheduler.Default);
        }

        /// <summary>Exécute un raccourci (« ctrl+s », « ctrl+shift+p », « ctrl+shift+i », « ctrl+b », « f5 », « f11 »). Renvoie false si inconnu.</summary>
        internal bool RunShortcut(string combo)
        {
            var now = Environment.TickCount64;
            if (combo == _lastShortcut && now - _lastShortcutTick < 250) return true;
            _lastShortcut = combo;
            _lastShortcutTick = now;
            switch (combo)
            {
                case "ctrl+s":
                    _commandRegistry.Execute("file.save");
                    return true;
                case "ctrl+shift+p":
                    ToggleCommandPalette();
                    return true;
                case "ctrl+shift+i":
                    ToggleFileAiBar();
                    return true;
                case "ctrl+b":
                    ToggleSide(isExplorer: true);
                    return true;
                case "f5":
                    _commandRegistry.Execute("run.build");
                    return true;
                case "f11":
#if WINDOWS
                    if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                        Platforms.Windows.SnapLayoutsHelper.ToggleFullScreen(nativeWindow);
#endif
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// ★ AJOUT (27/09, point 3 de Tom : « le chat MOTO AI sur le côté, plus une petite barre qui n'apparaît que sur demande pour
        /// modifier le fichier ouvert ») : Ctrl+Maj+I et la palette ouvrent le bandeau IA de l'éditeur (diff et accord avant d'écrire)
        /// au lieu de l'ancienne barre centrale flottante, qui s'affichait d'elle-même à chaque fichier ouvert et à chaque retour dans
        /// la fenêtre. Fermé → ouvert, curseur dedans. Ouvert mais curseur ailleurs (dans le code) → curseur dedans. Curseur dedans → fermé.
        /// </summary>
        private void ToggleFileAiBar()
        {
            if (_viewModel.SelectedDocument == null)
            {
                StatusBar.SetStatus("Ouvre d'abord un fichier : cette barre sert à le modifier. Pour discuter avec l'IA, utilise le chat MOTO AI.");
                return;
            }
            if (EditorPane.IsAiBandFocused) EditorPane.CloseAiBand();
            else EditorPane.OpenAiBand();
        }

        /// <summary>★ AJOUT (27/09) : Échap ferme le bandeau IA, seulement si le curseur est dans son champ (ailleurs, Échap garde son rôle).</summary>
        private bool TryCloseFileAiBarOnEscape() => EditorPane.IsAiBandFocused && EditorPane.CloseAiBand();
    }
}
