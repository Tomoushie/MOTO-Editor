// Moto.Editor/Views/AiChatView.xaml.cs (régénéré)
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Moto.Editor.Models;
using Moto.Editor.Services;

namespace Moto.Editor.Views
{
    /// <summary>
    /// ★ AJOUT (03/09, bouton "copier" manquant sur le code) : choisit le
    /// template selon ChatContentSegment.IsCode — texte normal ou bloc de code
    /// (police mono + bouton copier).
    /// </summary>
    public sealed class ChatSegmentSelector : DataTemplateSelector
    {
        public DataTemplate? TextTemplate { get; set; }
        public DataTemplate? CodeTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
            => ((ChatContentSegment)item).IsCode ? CodeTemplate! : TextTemplate!;
    }

    /// <summary>
    /// Panneau de chat agent : liste de messages, saisie en bas, sélecteurs
    /// mode/modèle, pièces jointes. Branché comme les autres panneaux IA via
    /// AddFloatingPanel (MainPage.Panels.cs) — titre/fermeture/glisser fournis
    /// par ce wrapper commun, pas par ce fichier (voir en-tête XAML).
    /// </summary>
    public partial class AiChatView : ContentView
    {
        public ChatService Chat { get; }

        public event Action<string> ModelChanged;

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
            };

            MessageList.ItemsSource = Chat.ActiveThread?.Messages;
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

            // ★ CORRECTION (02/09, revue croisée) : ne testait que "interne", donc
            // choisir "Ollama (qwen2.5-coder:7b)" (qui utilise le MÊME chemin local
            // que "MOTO interne") désactivait PreferInternal — sautait justement
            // l'appel à Ollama. IsExternalProviderName (ChatService) ne reconnaît
            // comme externes que les vrais providers cloud (OpenAI/Anthropic/Mistral).
            Chat.PreferInternal = !ChatService.IsExternalProviderName(model);
            ModelChanged?.Invoke(model);
        }

        /// <summary>
        /// ★ AJOUT (03/09, bouton "copier" manquant sur le code, trouvé par Tom).
        /// </summary>
        private async void OnCopyCodeClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.BindingContext is ChatContentSegment segment)
                await Clipboard.SetTextAsync(segment.Text);
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
