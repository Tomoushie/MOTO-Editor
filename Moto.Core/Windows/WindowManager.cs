// Moto.Core/Windows/WindowManager.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Core.Windows
{
    public enum WindowKind { Main, Editor, Debug, Analytics, Plugin, Settings }

    public sealed class WindowDescriptor
    {
        public WindowKind Kind { get; init; }
        public string Title { get; init; } = string.Empty;
        public int Width { get; init; } = 1200;
        public int Height { get; init; } = 800;
        public string? WorkspaceRoot { get; init; }
    }

    public sealed class WindowManager
    {
        private readonly Dictionary<WindowKind, WeakReference<Microsoft.Maui.Controls.Window>> _windows = new();
        private readonly object _lock = new();

        public event Action<WindowKind, Microsoft.Maui.Controls.Window>? WindowOpened;
        public event Action<WindowKind>? WindowClosed;

        public void Register(WindowKind kind, Microsoft.Maui.Controls.Window window)
        {
            lock (_lock)
            {
                _windows[kind] = new WeakReference<Microsoft.Maui.Controls.Window>(window);
                window.Destroying += (s, e) =>
                {
                    lock (_lock) _windows.Remove(kind);
                    WindowClosed?.Invoke(kind);
                };
            }
            WindowOpened?.Invoke(kind, window);
        }

        public Microsoft.Maui.Controls.Window? Get(WindowKind kind)
        {
            lock (_lock)
            {
                if (_windows.TryGetValue(kind, out var weak) && weak.TryGetTarget(out var win))
                    return win;
                return null;
            }
        }

        public bool IsOpen(WindowKind kind) => Get(kind) != null;

        public IReadOnlyList<WindowKind> OpenWindows
        {
            get
            {
                lock (_lock)
                {
                    return _windows
                        .Where(kv => kv.Value.TryGetTarget(out _))
                        .Select(kv => kv.Key)
                        .ToList();
                }
            }
        }

        public void FocusOrOpen(WindowKind kind, Func<Microsoft.Maui.Controls.Window> factory)
        {
            var existing = Get(kind);
            if (existing != null)
            {
                // ★ Application.ActivateWindow(Window) n'existe pas encore dans MAUI 8.x
                // (ajouté dans une version ultérieure) — remis au premier plan par défaut
                // par le système lors de la prochaine interaction ; pas de mise en avant
                // forcée pour cette passe (fenêtres spécialisées = fonctionnalité annexe).
            }
            else
            {
                var window = factory();
                Register(kind, window);
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
                {
                    Application.Current?.OpenWindow(window);
                });
            }
        }
    }
}
