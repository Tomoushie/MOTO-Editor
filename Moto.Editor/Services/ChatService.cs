// Moto.Editor/Services/ChatService.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Moto.Core.AI.Generation;
using Moto.Core.AI.Internal;
using Moto.Core.AI.Internal.Models;
using Moto.Core.AI;
using Moto.Editor.Models;

namespace Moto.Editor.Services
{
    /// <summary>
    /// Service de chat IA côté éditeur : gère les threads de conversation affichés
    /// dans le panneau chat, et fait écrire les réponses en direct par Ollama
    /// (Moto.Core ChatStreamService, depuis le 24/09) ou, à défaut, par le
    /// FallbackEngine (services en ligne configurés).
    /// </summary>
    public class ChatService
    {
        private readonly FallbackEngine _fallback;
        private readonly MotoAiKernel _kernel;

        /// <summary>Fournit le texte actuellement sélectionné dans l'éditeur (pour le contexte).</summary>
        public Func<string>? SelectionProvider { get; set; }

        /// <summary>
        /// ★ AJOUT (02/09, "vrai système de plugins") : point d'extension optionnel
        /// — si non-null, appelé pour toute entrée commençant par "/" AVANT de
        /// router vers le modèle IA. Retourne la réponse d'un plugin (traitée
        /// exactement comme une réponse IA normale) ou null si aucun plugin ne
        /// gère cette commande. ChatService ne connaît rien des plugins eux-mêmes
        /// — câblé une fois par MainPage (ResolveExtensionServices) vers
        /// PluginRegistry. Comme SendAsync est le SEUL chemin d'envoi utilisé par
        /// toutes les surfaces de chat (AiChatView, bandeau IA, Accueil), câbler
        /// ici plutôt que dans chaque vue couvre tout d'un coup.
        /// </summary>
        public Func<string, Task<string?>>? PluginCommandHandler { get; set; }

        /// <summary>
        /// ★ AJOUT (25/09, « Appliquer » dans le chat) : le bouton « Appliquer » d'un bloc de code d'une réponse. Câblé une fois par MainPage
        /// (qui connaît l'éditeur et la boîte de confirmation) — toutes les fenêtres du chat (panneau, fenêtre détachée ⧉) passent par ici.
        /// Null : le bouton ne fait rien.
        /// </summary>
        public Func<ChatContentSegment, Task>? ApplyCodeHandler { get; set; }

        /// <summary>
        /// ★ AJOUT (03/09, vraies stats IA du Tableau de bord global) : point
        /// d'extension optionnel — même patron que PluginCommandHandler ci-dessus.
        /// Appelé après CHAQUE appel IA réussi (via TrackAsync, donc SendAsync
        /// ET le bandeau IA de l'éditeur) avec (modèle, tokens estimés). ChatService ne
        /// connaît rien de GlobalUsageEngine — câblé une fois par MainPage
        /// (ResolveExtensionServices). Avant cet ajout, GlobalUsageEngine.RecordAiCall
        /// n'était appelée par AUCUN code de production : les stats IA du Tableau de
        /// bord global (fenêtre "ai.globaldashboard") restaient bloquées à 0 en
        /// permanence, quel que soit l'usage réel.
        /// </summary>
        public Action<string, int>? AiCallRecorder { get; set; }

        /// <summary>Mode de l'IA (Beginner/Expert) pour le routage interne.</summary>
        public AiMode Mode { get; set; } = AiMode.Beginner;

        /// <summary>Racine du workspace courant (mise à jour à l'ouverture d'un dossier).</summary>
        public string WorkspaceRoot { get; set; }

        /// <summary>Threads de conversation (le plus récent en premier).</summary>
        public ObservableCollection<ChatThread> Threads { get; } = new();

        public ChatThread? CurrentThread => Threads.FirstOrDefault();

        // ★ AJOUT (02/09, réveil d'AiChatView — panneau IA réel) : ces membres
        // manquaient (AiChatView.xaml.cs les appelait déjà, dérive d'API jamais
        // recollée — voir la mémoire du chantier). ActiveThread est un simple alias
        // de CurrentThread (même convention "le plus récent = en tête de liste"
        // déjà utilisée par EnsureThread/MainPage.Routing.cs/HomeView — pas une
        // 2e source de vérité), avec un événement pour que le panneau se rebranche
        // sur le bon thread sans dupliquer la logique de sélection.
        public ChatThread? ActiveThread => CurrentThread;

        public event Action<ChatThread>? ActiveThreadChanged;

        /// <summary>
        /// ★ AJOUT (03/09, panneau "Tâches en arrière-plan" réel) : un enregistrement
        /// par appel IA en cours OU terminé récemment (SendAsync ET le bandeau IA de
        /// l'éditeur, via TrackAsync plus bas — un seul point de suivi pour les deux).
        /// Le plus récent en tête, même convention que Threads. Volontairement
        /// plafonné (voir TrimTasks) pour ne pas grossir indéfiniment sur une longue
        /// session.
        /// </summary>
        public ObservableCollection<ChatTaskRecord> Tasks { get; } = new();

        /// <summary>Éléments de contexte attachés à la PROCHAINE question envoyée
        /// (fichiers/sélections) — consommés (vidés) par SendAsync.
        ///
        /// ★ CORRIGÉ (22/09, prérequis de la vue fractionnée) : ce sac était GLOBAL
        /// à toutes les conversations. Joindre un fichier dans une conversation,
        /// changer de conversation (SwitchThread déplace le thread choisi en tête,
        /// donc change le thread actif), puis envoyer faisait voyager la pièce
        /// jointe vers le mauvais message — limite documentée dans CLAUDE.md.
        /// C'était surtout ce qui rendait la vue fractionnée IMPOSSIBLE : deux
        /// conversations affichées en même temps ne peuvent pas partager un seul
        /// sac de pièces jointes.
        ///
        /// Les pièces jointes vivent désormais PAR CONVERSATION (_pendingByThread).
        /// `Contexts` reste LA MÊME instance observable — celle que lie
        /// AiChatView.ContextList : elle n'est plus la source de vérité, elle
        /// AFFICHE les pièces jointes de la conversation active. Aucune signature
        /// publique ne change, aucune liaison XAML n'est touchée : un seul thread
        /// (cas courant) se comporte exactement comme avant.</summary>
        public ObservableCollection<ChatContextItem> Contexts { get; } = new();

        /// <summary>Pièces jointes en attente, par conversation. Clé = référence du
        /// ChatThread : ce modèle ne redéfinit ni Equals ni GetHashCode (vérifié),
        /// donc l'identité d'instance est bien le critère voulu.</summary>
        private readonly Dictionary<ChatThread, List<ChatContextItem>> _pendingByThread = new();

        /// <summary>File d'attente de la conversation donnée, créée à la demande.</summary>
        private List<ChatContextItem> PendingFor(ChatThread thread)
        {
            if (!_pendingByThread.TryGetValue(thread, out var list))
            {
                list = new List<ChatContextItem>();
                _pendingByThread[thread] = list;
            }
            return list;
        }

        /// <summary>Recalcule le contenu affiché de `Contexts` depuis la conversation
        /// ACTIVE. Même instance de collection (les liaisons existantes suivent),
        /// seul son contenu change.</summary>
        private void SyncContextsFromActiveThread()
        {
            Contexts.Clear();
            var thread = CurrentThread;
            if (thread is null) return;
            foreach (var item in PendingFor(thread))
            {
                Contexts.Add(item);
            }
        }

        /// <summary>Vrai = tente Ollama/MotoAiKernel avant tout repli externe (déjà le
        /// comportement historique de RouteAsync) ; faux = l'utilisateur a
        /// explicitement choisi un provider externe dans le sélecteur de modèle,
        /// on saute directement au FallbackEngine.</summary>
        public bool PreferInternal { get; set; } = true;

        // ★ CORRECTION (02/09, revue croisée) : "interne" ne devait PAS être le seul
        // mot qui compte — "MOTO interne" ET "Ollama (...)" utilisent tous les deux
        // exactement le même chemin local (_kernel.RouteAsync, ce même OllamaClient) ;
        // seuls OpenAI/Anthropic/Mistral sont de VRAIS providers externes. Avant ce
        // correctif, choisir "Ollama" par son nom dans le sélecteur de modèle
        // désactivait PreferInternal — donc sautait justement l'appel à... Ollama.
        // Utilisé à la fois ici (AskRawAsync, routage indépendant du panneau de
        // chat) et par AiChatView.xaml.cs (sélecteur de modèle) pour ne pas dupliquer
        // la liste des vrais providers externes à deux endroits.
        private static readonly string[] ExternalProviderNames = { "OpenAI", "Anthropic", "Mistral" };

        public static bool IsExternalProviderName(string model) =>
            !string.IsNullOrEmpty(model) &&
            ExternalProviderNames.Any(p => model.Contains(p, StringComparison.OrdinalIgnoreCase));

        /// <summary>★ AJOUT (26/09, décision de Tom) : dernière ligne des listes de modèles — ouvre Clés API, ce n'est pas un modèle.</summary>
        public const string AddOnlineServiceLabel = "Ajouter un service en ligne…";

        /// <summary>
        /// ★ AJOUT (26/09, décision de Tom : « les montrer seulement une fois leur clé ajoutée, avec une ligne « Ajouter un service en
        /// ligne… » qui ouvre Clés API ») : ce que proposent les listes de modèles — les modèles locaux, puis les services en ligne dont une clé
        /// est enregistrée, puis la ligne d'ajout. Une seule règle pour toutes les listes (chat, bandeau IA de l'éditeur, Accueil).
        /// </summary>
        public static IReadOnlyList<string> BuildModelChoices(IEnumerable<string> localModels, Func<string, bool> hasKey)
            => localModels.Concat(ExternalProviderNames.Where(hasKey)).Append(AddOnlineServiceLabel).ToList();

        /// <summary><see cref="BuildModelChoices"/> avec les clés réellement enregistrées dans Clés API.</summary>
        public IReadOnlyList<string> ModelChoices(params string[] localModels) => BuildModelChoices(localModels, HasOnlineKey);

        /// <summary>Une clé est enregistrée pour ce service en ligne (« OpenAI », « Anthropic », « Mistral »).</summary>
        public bool HasOnlineKey(string providerName)
            => Enum.TryParse<Moto.Core.AI.Models.AiProviderType>(providerName, out var type) && _fallback.HasApiKey(type);

        /// <summary>Une clé a pu être ajoutée ou retirée (Clés API vient de se fermer) : les listes de modèles se reconstruisent.</summary>
        public event Action? OnlineProvidersChanged;

        public void NotifyOnlineProvidersChanged() => OnlineProvidersChanged?.Invoke();

        /// <summary>Ouvre Clés API (ligne « Ajouter un service en ligne… » des listes de modèles) — fourni par MainPage.</summary>
        public Action? OpenApiKeysHandler { get; set; }

        /// <summary>
        /// ★ AJOUT (03/09, identité de l'IA locale) : envoyé comme vrai "system
        /// prompt" Ollama (pas un texte ajouté au message) — sans ça, le modèle
        /// local répond avec sa propre identité de base (ex. "qwen2.5-coder:7b"
        /// se présente comme Qwen/Alibaba Cloud), trouvé par Tom en testant.
        /// Scope volontairement limité au chemin interne (Ollama) : les
        /// providers externes (OpenAI/Anthropic/Mistral, via FallbackEngine) ne
        /// sont pas touchés ici — chantier séparé si le même souci s'y confirme.
        /// </summary>
        private const string InternalSystemPrompt =
            "Tu es MOTO AI, l'assistant intégré à MOTO Editor, un IDE léger " +
            "conçu par MOTO Software (fondé par Tom Nowak), inspiré de Zed et " +
            "VS Code. Réponds toujours à la première personne (\"je\"), en " +
            "français naturel.";

        public ChatService(string workspaceRoot, FallbackEngine? fallback, MotoAiKernel? kernel, ILogger<ChatService>? logger = null)
        {
            WorkspaceRoot = workspaceRoot ?? string.Empty;
            _fallback = fallback ?? new FallbackEngine();
            _kernel = kernel ?? new MotoAiKernel(workspaceRoot);

            // Threads est trié "plus récent en tête" par convention (EnsureThread/
            // CreateThread insèrent toujours à l'index 0) — un changement de
            // collection change donc toujours potentiellement le thread actif.
            Threads.CollectionChanged += (_, _) =>
            {
                var current = CurrentThread;
                // ★ ORDRE IMPORTANT (22/09) : resynchroniser les pièces jointes
                // AVANT de prévenir les vues — un abonné à ActiveThreadChanged
                // (AiChatView) doit voir le contexte de la NOUVELLE conversation
                // au moment où il réagit au changement.
                SyncContextsFromActiveThread();
                if (current != null) ActiveThreadChanged?.Invoke(current);
            };
        }

        /// <summary>Crée une nouvelle conversation vide et la rend active.</summary>
        public ChatThread CreateThread()
        {
            var thread = new ChatThread();
            Threads.Insert(0, thread);
            return thread;
        }

        /// <summary>
        /// ★ AJOUT (03/09, réveil de ThreadListView) : manquait — le panneau
        /// l'appelait déjà (ThreadListView.xaml.cs) sans que la méthode existe.
        /// Rend `thread` actif en le plaçant en tête de `Threads`, même
        /// convention que CreateThread/EnsureThread ("le plus récent en tête" =
        /// actif) : Threads.Move déclenche CollectionChanged, déjà écouté plus
        /// haut dans le constructeur pour lever ActiveThreadChanged — pas de
        /// 2e mécanisme de notification à maintenir en parallèle.
        /// </summary>
        public void SwitchThread(ChatThread thread)
        {
            if (thread is null) return;
            var index = Threads.IndexOf(thread);
            if (index <= 0) return; // introuvable, ou déjà actif (déjà en tête)
            Threads.Move(index, 0);
        }

        /// <summary>
        /// ★ AJOUT (03/09, réveil de ThreadListView) : manquait, même situation que
        /// SwitchThread ci-dessus. Recherche simple, insensible à la casse, sur le
        /// titre et le contenu des messages. Requête vide -> liste complète (pour
        /// que vider le champ de recherche restaure tous les threads).
        /// </summary>
        public IEnumerable<ChatThread> SearchThreads(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Threads;
            return Threads.Where(t =>
                t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Messages.Any(m => m.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        /// <summary>Attache un fichier au contexte de la prochaine question, DANS la
        /// conversation active (voir _pendingByThread).</summary>
        public void AddFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var thread = EnsureThread();
            var item = new ChatContextItem { Kind = "file", Path = path };
            PendingFor(thread).Add(item);
            // EnsureThread place le thread en tête s'il vient d'être créé : il est
            // donc toujours le thread actif ici, et `Contexts` le reflète.
            Contexts.Add(item);
        }

        /// <summary>Attache la sélection courante de l'éditeur au contexte de la
        /// prochaine question (via SelectionProvider, déjà utilisé par SendAsync),
        /// DANS la conversation active (voir _pendingByThread).</summary>
        public void AddSelection()
        {
            var selection = SelectionProvider?.Invoke();
            if (string.IsNullOrWhiteSpace(selection)) return;
            var thread = EnsureThread();
            var item = new ChatContextItem { Kind = "selection", Content = selection };
            PendingFor(thread).Add(item);
            Contexts.Add(item);
        }

        private ChatThread EnsureThread()
        {
            var thread = Threads.FirstOrDefault();
            if (thread is null)
            {
                thread = new ChatThread();
                Threads.Insert(0, thread);
            }
            return thread;
        }

        // ------------------------------------------------------------------
        // ★ CHAT EN FLUX (24/09, "écriture générative fonctionnelle")
        // Avant : chaque question partait SEULE vers le modèle (aucun historique, le
        // fichier ouvert jamais envoyé), par /api/generate sans fenêtre de contexte
        // (~4 000 jetons, début du prompt coupé en silence), et la réponse
        // n'apparaissait qu'à la toute fin, après parfois plus d'une minute.
        // Maintenant : la réponse s'écrit en direct (Moto.Core ChatStreamService),
        // avec l'historique de la conversation et le fichier affiché, et ■ l'arrête.
        // ------------------------------------------------------------------

        private readonly ChatStreamService _stream = new();
        private CancellationTokenSource? _replyCts;

        /// <summary>★ AJOUT (24/09) : le fichier affiché dans l'éditeur (chemin ou titre, texte actuel), ou null s'il n'y en a pas.</summary>
        public Func<(string Path, string Text)?>? ActiveFileProvider { get; set; }

        /// <summary>
        /// ★ AJOUT (24/09) : vrai (mode « Chat &amp; Write », par défaut) — le fichier affiché et la sélection partent avec la question, vers le
        /// modèle LOCAL seulement (jamais vers un service en ligne). Faux (mode « Chat ») : seulement ce que l'utilisateur joint avec 📎.
        /// </summary>
        public bool IncludeActiveFile { get; set; } = true;

        /// <summary>Vrai tant qu'une réponse s'écrit (une seule à la fois).</summary>
        public bool IsReplying => _replyCts is not null;

        /// <summary>Levé sur le fil de l'interface quand une réponse commence (vrai) ou se termine (faux) : le bouton d'envoi devient ■.</summary>
        public event Action<bool>? ReplyingChanged;

        /// <summary>■ : arrête la réponse en cours. Ce qui est déjà écrit reste affiché.</summary>
        public void StopReply()
        {
            try { _replyCts?.Cancel(); }
            catch (ObjectDisposedException) { /* la réponse venait de se terminer */ }
        }

        /// <summary>
        /// Envoie une question dans la conversation active et y fait écrire la réponse EN DIRECT. Renvoie le message ajouté en réponse — réponse du
        /// modèle (<see cref="ChatMessage.IsModelTurn"/>), réponse d'un plugin, ou message d'erreur (rôle « system ») — ou null si rien n'est
        /// parti (texte vide, ou une réponse s'écrit déjà).
        /// <paramref name="includeActiveFile"/> : force l'envoi (ou non) du fichier affiché et de la sélection pour CETTE question, quel que soit le
        /// mode choisi (« Expliquer » en a besoin même en mode « Chat »).
        /// </summary>
        public async Task<ChatMessage?> SendAsync(string text, bool? includeActiveFile = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text) || IsReplying) return null;

            var thread = EnsureThread();

            // L'historique AVANT d'ajouter la question : seulement les vrais échanges avec le modèle, et tels qu'ils ont été envoyés — c'est ce
            // qui permet à Ollama de reprendre ce qu'il a déjà lu au lieu de tout relire (voir Moto.Core ChatPrompts).
            var history = thread.Messages
                .Where(m => m.IsModelTurn && !m.IsStreaming)
                .Select(m => new ChatTurn(m.Role, m.SentContent ?? m.Content, m.IsUser ? m.Content : null))
                .ToList();

            var question = new ChatMessage { Role = "user", Content = text };
            thread.Messages.Add(question);
            thread.LastActivityUtc = DateTime.UtcNow;

            // ★ AJOUT (02/09, "vrai système de plugins") : voir PluginCommandHandler
            // ci-dessus. Court-circuite le modèle IA si un plugin gère la commande.
            if (text.StartsWith("/", StringComparison.Ordinal) && PluginCommandHandler != null)
            {
                var pluginReply = await PluginCommandHandler(text);
                if (pluginReply != null)
                {
                    var message = new ChatMessage { Role = "ai", Content = pluginReply };
                    thread.Messages.Add(message);
                    thread.LastActivityUtc = DateTime.UtcNow;
                    return message;
                }
            }

            var request = BuildRequest(thread, text, history, includeActiveFile ?? IncludeActiveFile);
            var reply = new ChatMessage { Role = "ai", IsStreaming = true, Footnote = "Réflexion…" };
            thread.Messages.Add(reply);
            var pump = new ReplyPump(reply);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _replyCts = cts;
            ReplyingChanged?.Invoke(true);
            try
            {
                var outcome = await TrackAsync(thread.Title, PreferInternal ? "Ollama (local)" : "Service en ligne",
                    () => AnswerAsync(request, pump, cts.Token), o => o.Content.Length);
                return Finish(thread, question, reply, outcome);
            }
            catch (OperationCanceledException)
            {
                var written = pump.Text;
                if (written.Length == 0) return ReplaceWithProblem(thread, reply, "Réponse arrêtée avant le premier mot.");

                // Gardée à l'écran mais pas rejouée ensuite : la question telle qu'envoyée n'est pas connue ici, et la réponse est incomplète.
                reply.Content = written;
                reply.Footnote = "■ Réponse arrêtée : elle est incomplète.";
                reply.IsStreaming = false;
                return reply;
            }
            catch (Exception ex)
            {
                return ReplaceWithProblem(thread, reply, "Erreur IA : " + ex.Message);
            }
            finally
            {
                _replyCts = null;
                thread.LastActivityUtc = DateTime.UtcNow;
                ReplyingChanged?.Invoke(false);
            }
        }

        /// <summary>Local d'abord (en flux) ; si Ollama est éteint ou n'a aucun modèle, les services en ligne configurés répondent à la place.</summary>
        private async Task<ChatOutcome> AnswerAsync(ChatRequest request, ReplyPump pump, CancellationToken ct)
        {
            if (!PreferInternal) return await AskOnlineAsync(request, ct);

            var local = await _stream.StreamAsync(request, pump.Append, pump.Status, ct);
            if (local.Succeeded || !local.LocalUnavailable) return local;

            pump.Status("Ollama ne répond pas : question envoyée aux services en ligne configurés (Réglages → IA)…");
            var online = await AskOnlineAsync(request, ct);
            if (!online.Succeeded)
                return online with { Problem = $"{local.Problem} Aucun service en ligne n'a pu répondre à la place : lance Ollama, ou ajoute une clé dans Réglages → IA." };

            var notes = new List<string> { $"Ollama ne répond pas : réponse de {online.Model}, un service en ligne." };
            notes.AddRange(online.Notes);
            return online with { Notes = notes };
        }

        private Task<ChatOutcome> AskOnlineAsync(ChatRequest request, CancellationToken ct)
            => _stream.RunOnlineAsync(request, async (prompt, token) =>
            {
                // Pas de « contexte » : les fournisseurs le collent tel quel dans le message envoyé (voir RouteAsync plus bas).
                var result = await _fallback.GenerateAsync(prompt, cancellationToken: token);
                if (!result.Success)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? "aucun service configuré" : result.Error);
                return (result.Content ?? string.Empty, string.IsNullOrWhiteSpace(result.ProviderName) ? "Service en ligne" : result.ProviderName);
            }, ct);

        /// <summary>
        /// La question et son contexte. ★ CORRIGÉ (22/09, conservé) : les pièces jointes consommées sont celles de la conversation QUI REÇOIT le
        /// message — une pièce jointe attachée dans une conversation ne part jamais dans une autre (vue fractionnée). Lues au moment de l'envoi
        /// (contenu le plus à jour), puis vidées : « j'attache pour CETTE question ».
        /// </summary>
        private ChatRequest BuildRequest(ChatThread thread, string text, List<ChatTurn> history, bool includeEditorContext)
        {
            var pending = PendingFor(thread);
            var attachments = pending.Select(ToAttachment).Where(a => a is not null).Select(a => a!).ToList();
            if (pending.Count > 0)
            {
                pending.Clear();
                SyncContextsFromActiveThread();
            }

            var file = includeEditorContext ? ActiveFileProvider?.Invoke() : null;
            return new ChatRequest
            {
                Message = text,
                History = history,
                FilePath = file?.Path,
                FileText = file?.Text,
                Selection = includeEditorContext ? SelectionProvider?.Invoke() : null,
                Attachments = attachments,
            };
        }

        private static ChatAttachment? ToAttachment(ChatContextItem item)
        {
            if (item.Kind == "selection") return new ChatAttachment("sélection jointe", item.Content ?? string.Empty);
            if (item.Kind != "file") return null;

            var name = System.IO.Path.GetFileName(item.Path);
            try
            {
                if (!System.IO.File.Exists(item.Path))
                    return new ChatAttachment(name, "(fichier introuvable : déplacé ou supprimé depuis qu'il a été joint)");
                if (new System.IO.FileInfo(item.Path).Length > 2_000_000)
                    return new ChatAttachment(name, "(fichier trop gros pour être joint : plus de 2 Mo)");
                return new ChatAttachment(name, System.IO.File.ReadAllText(item.Path));
            }
            catch (Exception ex)
            {
                return new ChatAttachment(name, $"(fichier illisible : {ex.Message})");
            }
        }

        private static ChatMessage Finish(ChatThread thread, ChatMessage question, ChatMessage reply, ChatOutcome outcome)
        {
            if (outcome.Succeeded)
            {
                question.SentContent = outcome.SentUserMessage;
                question.IsModelTurn = true;
                reply.Content = outcome.Content;
                reply.IsModelTurn = true;
                reply.Footnote = Footnote(outcome);
                reply.IsStreaming = false;
                return reply;
            }

            if (outcome.PartialContent.Length > 0)
            {
                // Le modèle a commencé puis s'est interrompu (délai dépassé…) : on garde ce qu'il a écrit, sans le rejouer ensuite.
                reply.Content = outcome.PartialContent;
                reply.Footnote = "⚠ " + outcome.Problem;
                reply.IsStreaming = false;
                return reply;
            }

            return ReplaceWithProblem(thread, reply, outcome.Problem ?? "L'IA n'a pas répondu.", outcome.Notes);
        }

        /// <summary>Remplace la bulle en attente par un message de l'éditeur (rôle « system », jamais rejoué au modèle).</summary>
        private static ChatMessage ReplaceWithProblem(ChatThread thread, ChatMessage placeholder, string problem, IReadOnlyList<string>? notes = null)
        {
            placeholder.IsStreaming = false;
            var message = new ChatMessage
            {
                Role = "system",
                Content = "⚠ " + problem,
                Footnote = notes is { Count: > 0 } ? string.Join("\n", notes.Select(n => "ℹ " + n)) : string.Empty,
            };

            var index = thread.Messages.IndexOf(placeholder);
            if (index >= 0) thread.Messages[index] = message;
            else thread.Messages.Add(message);
            return message;
        }

        /// <summary>« qwen2.5-coder:7b · 6,2 s · a lu : fichier ouvert (A.cs), sélection », puis les remarques utiles, une par ligne.</summary>
        private static string Footnote(ChatOutcome outcome)
        {
            var head = new List<string> { outcome.Model ?? "modèle" };
            if (outcome.TotalSeconds > 0) head.Add($"{outcome.TotalSeconds:0.#} s");
            if (outcome.SentContext.Count > 0) head.Add("a lu : " + string.Join(", ", outcome.SentContext));

            var lines = new List<string> { string.Join(" · ", head) };
            if (outcome.Truncated) lines.Add("⚠ Réponse coupée : elle a atteint la longueur maximale (demande « continue »).");
            if (!string.IsNullOrWhiteSpace(outcome.Note)) lines.Add("ℹ " + outcome.Note);
            lines.AddRange(outcome.Notes.Select(n => "ℹ " + n));
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Relaie le texte qui arrive (sur un fil d'arrière-plan) vers la bulle affichée, au plus dix fois par seconde : au-delà, l'interface
        /// passerait son temps à se redessiner. Le texte complet est posé à la fin par Finish.
        /// </summary>
        private sealed class ReplyPump
        {
            private readonly ChatMessage _message;
            private readonly StringBuilder _text = new();
            private readonly object _gate = new();
            private long _lastPost;

            public ReplyPump(ChatMessage message) => _message = message;

            public string Text
            {
                get { lock (_gate) return _text.ToString(); }
            }

            public void Append(string chunk)
            {
                string snapshot;
                lock (_gate)
                {
                    _text.Append(chunk);
                    var now = Environment.TickCount64;
                    if (now - _lastPost < 100) return;
                    _lastPost = now;
                    snapshot = _text.ToString();
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!_message.IsStreaming) return; // arrivé après la fin : le texte final est déjà posé
                    _message.Content = snapshot;
                    _message.Footnote = string.Empty;
                });
            }

            public void Status(string status) => MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_message.IsStreaming && _message.Content.Length == 0) _message.Footnote = status;
            });
        }

        /// <summary>
        /// ★ AJOUT (24/09, écriture générative) : même suivi que RunTrackedAsync (ligne dans « Tâches en arrière-plan », compteur d'appels IA)
        /// pour un travail qui ne rend pas simplement du texte — l'édition en ligne (InlineEditOutcome). <paramref name="sizeInChars"/> :
        /// taille de ce que le modèle a produit, pour l'estimation de jetons (chars/4).
        /// </summary>
        public async Task<T> TrackAsync<T>(string label, string model, Func<Task<T>> work, Func<T, int> sizeInChars)
        {
            var record = new ChatTaskRecord { Label = label, Model = model };
            Tasks.Insert(0, record);
            try
            {
                var response = await work();
                // Même heuristique déjà utilisée par MainPage.Panels.cs/RefreshHomeStats
                // (chars/4) — pas un vrai tokenizer, mais cohérente avec le chiffre déjà
                // affiché ailleurs plutôt que d'inventer une 2e estimation différente.
                AiCallRecorder?.Invoke(model, sizeInChars(response) / 4);
                return response;
            }
            catch
            {
                record.Failed = true;
                throw;
            }
            finally
            {
                record.EndedUtc = DateTime.UtcNow;
                TrimTasks();
            }
        }

        /// <summary>Garde un historique court (30 max) — ne retire jamais une tâche
        /// encore en cours, seulement les plus anciennes déjà terminées.</summary>
        private void TrimTasks()
        {
            for (var i = Tasks.Count - 1; i >= 0 && Tasks.Count > 30; i--)
            {
                if (!Tasks[i].IsRunning) Tasks.RemoveAt(i);
            }
        }

        /// <summary>
        /// ★ REMPLACE (24/09, écriture générative) AskWithCodeAsync — dont la consigne « réponds avec le code COMPLET modifié » servait à REMPLACER
        /// tout le fichier de l'éditeur par la réponse, sans contrôle. Envoie une consigne COMPLÈTE telle quelle au fournisseur choisi dans le
        /// sélecteur du bandeau IA (la consigne vient de InlineEditPrompts pour une édition, ou d'un texte d'explication).
        /// </summary>
        /// <remarks>
        /// ★ CORRECTION (02/09, revue croisée, conservée) : le routage interne/externe se calcule à partir du modèle choisi ICI (bandeau IA de
        /// l'éditeur), pas du PreferInternal PARTAGÉ du panneau de chat — sinon changer de modèle dans un des deux endroits affectait
        /// silencieusement l'autre.
        /// </remarks>
        public Task<string> AskRawAsync(string model, string fullPrompt)
            => RouteAsync(fullPrompt, !IsExternalProviderName(model));

        /// <summary>Route un prompt vers Ollama (MotoAiKernel) puis, en repli, vers le FallbackEngine.</summary>
        private async Task<string> RouteAsync(string prompt, bool preferInternal)
        {
            // ★ CORRECTION (02/09) : preferInternal existait déjà (sélecteur de
            // modèle d'AiChatView) mais RouteAsync l'ignorait totalement — "MOTO
            // interne" et "OpenAI"/"Anthropic"/"Mistral" avaient donc rigoureusement
            // le même effet. Faux = on saute l'essai Ollama et on va direct au
            // FallbackEngine (les providers externes configurés dans les Réglages),
            // au lieu d'attendre inutilement un Ollama que l'utilisateur n'a pas
            // choisi.
            if (preferInternal)
            {
                // Surcharge (string, int, CancellationToken) qui renvoie AiResponse (pas
                // la variante texte simple (string, CancellationToken) qui renvoie string).
                var kernelResponse = await _kernel.RouteAsync(prompt, 256, default, InternalSystemPrompt);
                if (kernelResponse is { Success: true } && !string.IsNullOrWhiteSpace(kernelResponse.Content))
                    return kernelResponse.Content;
            }

            // ★ CORRIGÉ (24/09) : le 2e argument (« contexte ») était WorkspaceRoot — les fournisseurs en ligne le collent tel quel dans le
            // message (« Contexte : … ») : le chemin local du projet, qui contient le nom d'utilisateur Windows, partait chez OpenAI/Anthropic/Mistral.
            var fallbackResult = await _fallback.GenerateAsync(prompt);
            if (fallbackResult.Success) return fallbackResult.Content;

            // ★ CORRIGÉ (25/09) : ce refus était RENVOYÉ comme une réponse du modèle — le bandeau IA le lisait comme du texte et disait
            // « le modèle n'a pas renvoyé de code ». Levé, il devient « le fournisseur n'a pas répondu (…) » (InlineEditService.RunWithAsync).
            throw new InvalidOperationException(ServiceUnavailableMessage(fallbackResult.Error));
        }

        /// <summary>Pourquoi aucun service en ligne n'a répondu, en clair. Sans service configuré, la demande échoue avant tout envoi.</summary>
        private string ServiceUnavailableMessage(string? error)
            => _fallback.ProviderManager.GetAllConfigs().Count == 0
                ? "aucun service en ligne n'est configuré : rien n'a été envoyé"
                : string.IsNullOrWhiteSpace(error) ? "aucun service en ligne n'a répondu" : error;
    }
}
