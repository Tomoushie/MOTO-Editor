// Moto.Editor/Views/AiChatView.xaml.cs (régénéré)
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

            // ★ CHANGÉ (24/09) : le mode reflète le réglage partagé (une 2e fenêtre « MOTO AI » ne remet plus « Chat & Write » d'office).
            ModePicker.SelectedIndex = Chat.IncludeActiveFile ? 1 : 0;
            ModelPicker.SelectedIndex = 0;  // MOTO interne

            // Rebind des messages à chaque changement de thread.
            Chat.ActiveThreadChanged += ShowThread;
            Chat.ReplyingChanged += OnReplyingChanged;

            ShowThread(Chat.ActiveThread);
            OnReplyingChanged(Chat.IsReplying);

            Loaded += OnLayoutChanged;
        }

        // ------------------------------------------------------------------
        // ★ AJOUT (25/09, chat en flux — trouvé en test hors écran) : le dock IA range ses panneaux dans une colonne qui défile
        // d'un bloc (ScrollView > VerticalStackLayout, MainPage.xaml). Le chat y recevait une hauteur illimitée : sa liste
        // s'étalait sur toute la conversation au lieu de défiler, la zone de saisie finissait sous le bord de la fenêtre, et
        // « suivre la réponse » n'avait rien à faire défiler. Le chat prend donc la hauteur VISIBLE de cette colonne (moins ce
        // qui est au-dessus de lui) : la liste défile seule, la saisie reste en bas. Fenêtre détachée (⧉) : pas de colonne qui
        // défile au-dessus du chat, sa hauteur n'est pas touchée.
        // ------------------------------------------------------------------

        private const double MinChatHeight = 260;
        private ScrollView? _viewport;
        private View? _viewportContent;

        // Jamais pendant le passage de mise en page lui-même (WinUI peut refuser un changement de taille en plein calcul).
        private void OnLayoutChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(FitToViewport);

        private void FitToViewport()
        {
            // Recherché à chaque fois : le panneau a pu être glissé vers l'autre dock depuis.
            ScrollView? viewport = null;
            for (var e = Parent; e is not null && viewport is null; e = e.Parent) viewport = e as ScrollView;

            if (!ReferenceEquals(viewport, _viewport))
            {
                if (_viewport is not null) _viewport.SizeChanged -= OnLayoutChanged;
                if (_viewportContent is not null) _viewportContent.SizeChanged -= OnLayoutChanged;
                _viewport = viewport;
                _viewportContent = viewport?.Content;
                // La colonne elle-même (fenêtre redimensionnée) et son contenu (un autre panneau apparaît au-dessus).
                if (_viewport is not null) _viewport.SizeChanged += OnLayoutChanged;
                if (_viewportContent is not null) _viewportContent.SizeChanged += OnLayoutChanged;
            }
            if (_viewport is null || _viewport.Height <= 0) return;

            // Au-dessus : en-tête du cadre, marges, panneaux placés avant celui-ci. En dessous : marges et bordures des cadres.
            double above = 0, below = 0;
            for (Element? e = this; e is not null && !ReferenceEquals(e, _viewport); e = e.Parent)
            {
                if (e is VisualElement v) above += v.Y;
                if (ReferenceEquals(e, this)) continue;
                if (e is Microsoft.Maui.IPadding p) below += p.Padding.Bottom;
                if (e is Border b) below += b.StrokeThickness;
            }

            var target = Math.Max(MinChatHeight, Math.Floor(_viewport.Height - above - below));
            if (Math.Abs(HeightRequest - target) > 1) HeightRequest = target;
        }

        // ------------------------------------------------------------------
        // ★ AJOUT (24/09, chat en flux) puis ★ CHANGÉ (25/09) : la conversation suit la réponse qui s'écrit — tant que
        // l'utilisateur est en bas. S'il remonte pour relire, elle ne le ramène plus en bas à chaque mot ; une nouvelle
        // question (ou réponse) l'y ramène.
        // ------------------------------------------------------------------

        private ObservableCollection<ChatMessage>? _shownMessages;
        private bool _stickToBottom = true;
        private double _autoScrollY = -1;

        private void ShowThread(ChatThread? thread)
        {
            if (_shownMessages is not null) _shownMessages.CollectionChanged -= OnMessagesChanged;
            _shownMessages = thread?.Messages;
            BindableLayout.SetItemsSource(MessagesStack, _shownMessages);
            _stickToBottom = true;
            if (_shownMessages is not null) _shownMessages.CollectionChanged += OnMessagesChanged;
        }

        private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add) _stickToBottom = true;
        }

        /// <summary>La pile de bulles (une bulle grandit, une bulle arrive) ou la zone visible a changé de hauteur.</summary>
        private void OnMessagesSizeChanged(object? sender, EventArgs e)
        {
            if (_stickToBottom) Dispatcher.Dispatch(ScrollToBottom);
        }

        private void ScrollToBottom()
        {
            if (!_stickToBottom) return; // l'utilisateur a remonté entre-temps
            _autoScrollY = Math.Max(0, MessagesStack.Height - MessageScroller.Height);
            // Jamais attendu : dans MAUI, une demande de défilement faite avant la fin de la précédente laisse celle-ci en attente
            // pour toujours (vu en test) — et pendant l'écriture, il en part une à chaque ligne.
            _ = MessageScroller.ScrollToAsync(0, _autoScrollY, false);
        }

        private void OnMessagesScrolled(object? sender, ScrolledEventArgs e)
        {
            if (Math.Abs(e.ScrollY - _autoScrollY) < 2) return; // notre propre défilement
            // L'utilisateur a fait défiler : on ne suit la réponse que s'il est resté (ou revenu) tout en bas.
            _stickToBottom = e.ScrollY >= MessagesStack.Height - MessageScroller.Height - 24;
        }

        /// <summary>
        /// ★ AJOUT (25/09) : la fenêtre détachée (⧉) qui affichait cette vue est fermée. Sans ceci, la vue restait abonnée au
        /// service de chat partagé et reconstruisait chaque bulle d'une fenêtre fermée — dix fois par seconde pendant qu'une
        /// réponse s'écrit.
        /// </summary>
        public void Detach()
        {
            Chat.ActiveThreadChanged -= ShowThread;
            Chat.ReplyingChanged -= OnReplyingChanged;
            ShowThread(null); // quitte la conversation et vide la pile
            ContextList.ItemsSource = null;
        }

        private void OnReplyingChanged(bool replying)
        {
            SendButton.Text = replying ? Moto.Editor.Controls.MotoIcons.Stop : Moto.Editor.Controls.MotoIcons.Send;
            ToolTipProperties.SetText(SendButton, replying ? "Arrêter la réponse" : "Envoyer");
        }

        private void OnModeChanged(object? sender, EventArgs e)
        {
            if (Chat is null || ModePicker.SelectedIndex < 0) return; // pendant InitializeComponent
            // « Chat » : seulement ce qui est joint ; « Chat & Write » / « Agent » : le fichier affiché et la sélection partent aussi.
            Chat.IncludeActiveFile = ModePicker.SelectedIndex != 0;
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

        /// <summary>★ AJOUT (06/10, câblage thinking_display) : clique sur l'en-tête « ✦ Thinking » pour déplier/replier le raisonnement.</summary>
        private void OnThinkingTapped(object sender, TappedEventArgs e)
        {
            if (sender is BindableObject b && b.BindingContext is ChatMessage m)
                m.ToggleThinking();
        }

        /// <summary>★ AJOUT (06/10, Claude Code .msg-foot) : copie le texte du message dans le presse-papiers.</summary>
        private async void OnCopyMessageClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.BindingContext is ChatMessage m && m.Content.Length > 0)
                await Clipboard.SetTextAsync(m.Content);
        }

        /// <summary>★ AJOUT (25/09, « Appliquer ») : pose le bloc dans le fichier affiché — tout le travail (où, diff, accord) est fait par MainPage.</summary>
        private async void OnApplyCodeClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.BindingContext is not ChatContentSegment segment) return;
            if (Chat.ApplyCodeHandler is not { } apply)
            {
                segment.ApplyStatus = "« Appliquer » n'est pas disponible dans cette fenêtre : utilise « Copier ».";
                return;
            }

            try
            {
                await apply(segment);
            }
            catch (Exception ex)
            {
                App.LogCrash("AiChatView.OnApplyCodeClicked", ex);
                segment.ApplyStatus = "⚠ Erreur : " + ex.Message;
            }
        }

        // ------------------------------------------------------------------
        // Saisie + slash
        // ------------------------------------------------------------------

        /// <summary>➤ envoie ; ■ (pendant qu'une réponse s'écrit) l'arrête.</summary>
        private void OnSendButtonClicked(object sender, EventArgs e)
        {
            if (Chat.IsReplying)
            {
                Chat.StopReply();
                return;
            }
            SendInput();
        }

        /// <summary>Entrée : envoie — mais n'arrête jamais une réponse en cours (le texte tapé reste dans la zone de saisie).</summary>
        private void OnInputCompleted(object sender, EventArgs e)
        {
            if (!Chat.IsReplying) SendInput();
        }

        private async void SendInput()
        {
            var text = InputEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text)) return;

            InputEntry.Text = string.Empty;
            SlashList.IsVisible = false;

            try
            {
                await Chat.SendAsync(text);
            }
            catch (Exception ex)
            {
                // async void : une exception ici ne doit pas remonter jusqu'au filet global de l'application sans rien montrer.
                App.LogCrash("AiChatView.SendInput", ex);
            }
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
