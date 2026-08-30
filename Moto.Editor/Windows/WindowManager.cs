// Moto.Editor/Windows/WindowManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Windows
{
    public enum WindowKind { Main, Editor, Debug, Analytics, Plugin, Settings, Marketplace }

    public sealed class WindowManager
    {
        private readonly Dictionary<WindowKind, WeakReference<Window>> _windows = new();
        private readonly object _lock = new();

        public event Action<WindowKind, Window>? WindowOpened;
        public event Action<WindowKind>? WindowClosed;

        public void Register(WindowKind kind, Window window)
        {
            lock (_lock)
            {
                _windows[kind] = new WeakReference<Window>(window);
                window.Destroying += (s, e) =>
                {
                    lock (_lock) _windows.Remove(kind);
                    WindowClosed?.Invoke(kind);
                };
            }
            WindowOpened?.Invoke(kind, window);
        }

        public Window? Get(WindowKind kind)
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
                    return _windows.Where(kv => kv.Value.TryGetTarget(out _))
                                   .Select(kv => kv.Key).ToList();
            }
        }

        public void OpenOrFocus(WindowKind kind, Func<Window> factory)
        {
            var existing = Get(kind);
            if (existing != null)
            {
                // Application.ActivateWindow(Window) n'existe pas en MAUI 8.x (voir
                // Moto.Core/Windows/WindowManager.cs pour la même note).
                return;
            }
            var window = factory();
            Register(kind, window);
            MainThread.BeginInvokeOnMainThread(() => Application.Current?.OpenWindow(window));
        }

        public void Close(WindowKind kind)
        {
            var win = Get(kind);
            if (win != null) MainThread.BeginInvokeOnMainThread(() => Application.Current?.CloseWindow(win));
        }
    }

    public sealed class SpecializedWindowPage : ContentPage
    {
        public SpecializedWindowPage(string title, View content)
        {
            Title = title;
            Content = content;
            BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#1E1F24");
        }
    }
}
