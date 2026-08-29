// Moto.Core/Behaviors/KeyboardShortcutBehavior.cs
// Behavior MAUI attachable pour raccourcis clavier.
// Sur Windows : utilise KeyDown natif via handler.
// Sur autres plateformes : MAUI gère via Focused + événements.
using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Input;

namespace Moto.Core.Behaviors
{
    /// <summary>Modificateurs supportés.</summary>
    [Flags]
    public enum ShortcutModifiers
    {
        None = 0,
        Ctrl = 1,
        Shift = 2,
        Alt = 4,
        Cmd = 8
    }

    /// <summary>
    /// Behavior MAUI pour raccourcis clavier cross-platform.
    /// Usage en XAML :
    /// <ContentPage.Behaviors>
    ///     <behaviors:KeyboardShortcutBehavior
    ///         Key="P"
    ///         Modifiers="Ctrl,Shift"
    ///         Command="{Binding OpenPaletteCommand}" />
    /// </ContentPage.Behaviors>
    /// </summary>
    public sealed class KeyboardShortcutBehavior : Behavior<VisualElement>
    {
        public static readonly BindableProperty KeyProperty =
            BindableProperty.Create(nameof(Key), typeof(string), typeof(KeyboardShortcutBehavior), "P");

        public static readonly BindableProperty ModifiersProperty =
            BindableProperty.Create(nameof(Modifiers), typeof(ShortcutModifiers), typeof(KeyboardShortcutBehavior), ShortcutModifiers.Ctrl);

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(System.Windows.Input.ICommand), typeof(KeyboardShortcutBehavior));

        public string Key
        {
            get => (string)GetValue(KeyProperty);
            set => SetValue(KeyProperty, value);
        }

        public ShortcutModifiers Modifiers
        {
            get => (ShortcutModifiers)GetValue(ModifiersProperty);
            set => SetValue(ModifiersProperty, value);
        }

        public System.Windows.Input.ICommand? Command
        {
            get => (System.Windows.Input.ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        /// <summary>Déclenché quand le raccourci est activé.</summary>
        public event EventHandler? Activated;

        private VisualElement? _attachedElement;

        protected override void OnAttachedTo(VisualElement bindable)
        {
            base.OnAttachedTo(bindable);
            _attachedElement = bindable;

            // MAUI gère les raccourcis via KeyboardAccelerators sur les éléments focusables.
            // Sur Windows, on branche aussi sur le handler natif pour Ctrl+Shift+P global.
            bindable.HandlerChanged += OnHandlerChanged;
        }

        protected override void OnDetachingFrom(VisualElement bindable)
        {
            bindable.HandlerChanged -= OnHandlerChanged;
            _attachedElement = null;
            base.OnDetachingFrom(bindable);
        }

        private void OnHandlerChanged(object? sender, EventArgs e)
        {
#if WINDOWS
            if (_attachedElement?.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement native)
            {
                native.KeyDown += OnWindowsKeyDown;
            }
#endif
        }

#if WINDOWS
        private void OnWindowsKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (!string.Equals(e.Key.ToString(), Key, StringComparison.OrdinalIgnoreCase))
                return;

            var modifiers = ShortcutModifiers.None;
            var kbState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread;

            if (kbState(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                modifiers |= ShortcutModifiers.Ctrl;
            if (kbState(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                modifiers |= ShortcutModifiers.Shift;
            if (kbState(Windows.System.VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                modifiers |= ShortcutModifiers.Alt;

            if (modifiers == Modifiers)
            {
                e.Handled = true;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (Command?.CanExecute(null) == true)
                        Command.Execute(null);
                    Activated?.Invoke(this, EventArgs.Empty);
                });
            }
        }
#endif

        /// <summary>Déclenche manuellement le raccourci (pour tests).</summary>
        public void Trigger()
        {
            if (Command?.CanExecute(null) == true)
                Command.Execute(null);
            Activated?.Invoke(this, EventArgs.Empty);
        }
    }
}
