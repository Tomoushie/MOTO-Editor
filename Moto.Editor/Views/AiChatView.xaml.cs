// Moto.Editor/Views/AiChatView.xaml.cs (régénéré)
using System;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Moto.Editor.Models;
using Moto.Editor.Services;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Panneau de chat agent : saisie en bas, sélecteurs mode/modèle,
    /// drag-and-drop pour dock gauche/droite.
    /// </summary>
    public partial class AiChatView : ContentView
    {
        public ChatService Chat { get; }

        public event Action<DockSide> SideChangeRequested;
        public event Action CloseRequested;
        public event Action<string> ModelChanged;

        private bool _dockedRight = false;
        private double _dragStartX;

        public AiChatView(ChatService chat)
        {
            InitializeComponent();

            Chat = chat;
            ContextList.ItemsSource = Chat.Contexts;

            ModePicker.SelectedIndex = 1;   // Chat & Write
            ModelPicker.SelectedIndex = 0;  // MOTO interne

            // Rebind des messages à chaque changement de thread.
            Chat.ActiveThreadChanged += thread =>
            {
                MessageList.ItemsSource = thread.Messages;
                TitleLabel.Text = thread.Title;
            };

            MessageList.ItemsSource = Chat.ActiveThread?.Messages;

            var pan = new PanGestureRecognizer();
            pan.PanUpdated += OnPanUpdated;
            DragHandle.GestureRecognizers.Add(pan);
        }

        // ------------------------------------------------------------------
        // Drag-and-drop dock
        // ------------------------------------------------------------------

        private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _dragStartX = TranslationX;
                    break;

                case GestureStatus.Running:
                    TranslationX = _dragStartX + e.TotalX;
                    break;

                case GestureStatus.Completed:
                    var center = Bounds.CenterX + TranslationX;
                    var screen = Application.Current.Windows[0].Width / 2;

                    SideChangeRequested?.Invoke(center < screen ? DockSide.Left : DockSide.Right);
                    this.TranslateTo(0, 0, 150, Easing.CubicOut);
                    break;
            }
        }

        private void OnDockToggleClicked(object sender, EventArgs e)
        {
            _dockedRight = !_dockedRight;
            SideChangeRequested?.Invoke(_dockedRight ? DockSide.Right : DockSide.Left);
        }

        private void OnCloseClicked(object sender, EventArgs e)
        {
            CloseRequested?.Invoke();
        }

        private void OnNewThreadClicked(object sender, EventArgs e)
        {
            Chat.CreateThread();
        }

        private void OnClearClicked(object sender, EventArgs e)
        {
            Chat.ActiveThread?.Messages.Clear();
        }

        private void OnModelChanged(object sender, EventArgs e)
        {
            var model = ModelPicker.SelectedItem as string ?? "MOTO interne";

            Chat.PreferInternal = model.Contains("interne", StringComparison.OrdinalIgnoreCase);
            ModelChanged?.Invoke(model);
        }

        // ------------------------------------------------------------------
        // Saisie + slash
        // ------------------------------------------------------------------

        private async void OnSendClicked(object sender, EventArgs e)
        {
            var text = InputEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text)) return;

            InputEntry.Text = string.Empty;
            SlashList.IsVisible = false;

            await Chat.SendAsync(text);
        }

        private void OnInputTextChanged(object sender, TextChangedEventArgs e)
        {
            var text = e.NewTextValue ?? string.Empty;

            if (text.StartsWith("/") && !text.Contains(" "))
            {
                var matches = SlashCommandProcessor.KnownCommands
                    .Where(c => c.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                SlashList.ItemsSource = matches;
                SlashList.IsVisible = matches.Count > 0;
            }
            else
            {
                SlashList.IsVisible = false;
            }
        }

        private void OnSlashSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is string cmd)
            {
                InputEntry.Text = cmd.Split(' ')[0] + " ";
                SlashList.IsVisible = false;
                InputEntry.Focus();
            }
        }

        private async void OnAttachClicked(object sender, EventArgs e)
        {
            var action = await DisplayActionSheetAsync();

            if (action == "fichier")
            {
                var result = await FilePicker.Default.PickAsync();
                if (result != null) Chat.AddFile(result.FullPath);
            }
            else if (action == "sélection")
            {
                Chat.AddSelection();
            }
        }

        private Task<string> DisplayActionSheetAsync()
        {
            return Application.Current.MainPage.DisplayActionSheet(
                "Attacher", "Annuler", null, "fichier", "sélection");
        }
    }
}
