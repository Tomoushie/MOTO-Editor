// Moto.Editor/Services/ChatService.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moto.Core.AI.Internal;
using Moto.Core.AI.Internal.Models;
using Moto.Core.AI;
using Moto.Editor.Models;

namespace Moto.Editor.Services
{
    /// <summary>
    /// Service de chat IA côté éditeur : gère les threads de conversation affichés
    /// dans le panneau chat, et route les questions vers Ollama (via MotoAiKernel)
    /// ou le FallbackEngine (providers externes configurés).
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
        /// ★ AJOUT (03/09, vraies stats IA du Tableau de bord global) : point
        /// d'extension optionnel — même patron que PluginCommandHandler ci-dessus.
        /// Appelé après CHAQUE appel IA réussi (via RunTrackedAsync, donc SendAsync
        /// ET AskWithCodeAsync) avec (modèle, tokens estimés). ChatService ne
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
        /// par appel IA en cours OU terminé récemment (SendAsync ET AskWithCodeAsync,
        /// via RunTrackedAsync plus bas — un seul point de suivi pour les deux).
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
        // Utilisé à la fois ici (AskWithCodeAsync, routage indépendant du panneau de
        // chat) et par AiChatView.xaml.cs (sélecteur de modèle) pour ne pas dupliquer
        // la liste des vrais providers externes à deux endroits.
        private static readonly string[] ExternalProviderNames = { "OpenAI", "Anthropic", "Mistral" };

        public static bool IsExternalProviderName(string model) =>
            !string.IsNullOrEmpty(model) &&
            ExternalProviderNames.Any(p => model.Contains(p, StringComparison.OrdinalIgnoreCase));

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

        /// <summary>Envoie un message texte simple dans le thread courant (sans code attaché).</summary>
        public async Task SendAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var thread = EnsureThread();
            thread.Messages.Add(new ChatMessage { Role = "user", Content = text });
            thread.LastActivityUtc = DateTime.UtcNow;

            // ★ AJOUT (02/09, "vrai système de plugins") : voir PluginCommandHandler
            // ci-dessus. Court-circuite le modèle IA si un plugin gère la commande.
            if (text.StartsWith("/", StringComparison.Ordinal) && PluginCommandHandler != null)
            {
                var pluginReply = await PluginCommandHandler(text);
                if (pluginReply != null)
                {
                    thread.Messages.Add(new ChatMessage { Role = "ai", Content = pluginReply });
                    thread.LastActivityUtc = DateTime.UtcNow;
                    return;
                }
            }

            var selection = SelectionProvider?.Invoke() ?? string.Empty;
            var prompt = string.IsNullOrWhiteSpace(selection) ? text : $"{text}\n\nSélection :\n{selection}";

            // ★ AJOUT (02/09) : les pièces jointes (📎 fichier / sélection figée dans
            // AiChatView) n'étaient jusqu'ici QUE visuelles — Contexts n'était même
            // pas lu par SendAsync. Lues au moment de l'envoi (pas de l'attache, pour
            // capter le contenu le plus à jour du fichier) puis vidées : sémantique
            // "j'attache pour CETTE question", pas persistantes dans l'historique.
            //
            // ★ CORRIGÉ (22/09) : on consomme les pièces jointes de la conversation
            // QUI REÇOIT LE MESSAGE (`thread`, celui que EnsureThread vient de
            // résoudre), et non plus le sac global affiché. C'est ce qui garantit
            // qu'une pièce jointe attachée dans une conversation ne parte jamais
            // dans une autre — et ce qui rend la vue fractionnée possible.
            var pending = PendingFor(thread);
            if (pending.Count > 0)
            {
                var contextBlock = string.Join("\n\n", pending.Select(BuildContextBlock));
                prompt = $"{prompt}\n\nContexte attaché :\n{contextBlock}";
                pending.Clear();
                SyncContextsFromActiveThread();
            }

            var response = await RunTrackedAsync(
                thread.Title,
                PreferInternal ? "Ollama / MOTO interne" : "Fournisseur externe",
                () => RouteAsync(prompt, PreferInternal));
            thread.Messages.Add(new ChatMessage { Role = "ai", Content = response });
            thread.LastActivityUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// ★ AJOUT (03/09, panneau "Tâches en arrière-plan" réel) : enveloppe un
        /// appel IA (SendAsync ou AskWithCodeAsync) avec un ChatTaskRecord visible
        /// dans Tasks — point unique pour ne pas dupliquer la logique de suivi aux
        /// 2 endroits. `work` reste responsable du VRAI appel réseau/local.
        /// </summary>
        private async Task<string> RunTrackedAsync(string label, string model, Func<Task<string>> work)
        {
            var record = new ChatTaskRecord { Label = label, Model = model };
            Tasks.Insert(0, record);
            try
            {
                var response = await work();
                // Même heuristique déjà utilisée par MainPage.Panels.cs/RefreshHomeStats
                // (chars/4) — pas un vrai tokenizer, mais cohérente avec le chiffre déjà
                // affiché ailleurs plutôt que d'inventer une 2e estimation différente.
                AiCallRecorder?.Invoke(model, string.IsNullOrEmpty(response) ? 0 : response.Length / 4);
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

        private static string BuildContextBlock(ChatContextItem item)
        {
            if (item.Kind == "file")
            {
                try
                {
                    if (!System.IO.File.Exists(item.Path))
                        return $"Fichier {item.Path} : introuvable (déplacé/supprimé depuis l'attache ?).";

                    var content = System.IO.File.ReadAllText(item.Path);
                    // Plafond pour éviter qu'un gros fichier ne noie le prompt.
                    if (content.Length > 8000)
                        content = content.Substring(0, 8000) + "\n… (tronqué)";

                    return $"Fichier {System.IO.Path.GetFileName(item.Path)} :\n{content}";
                }
                catch (Exception ex)
                {
                    return $"Fichier {item.Path} : impossible à lire ({ex.Message}).";
                }
            }

            return $"Sélection attachée :\n{item.Content}";
        }

        /// <summary>
        /// Envoie un prompt avec le code courant au modèle choisi,
        /// pour modification en direct depuis le bandeau IA.
        /// </summary>
        public async Task<string> AskWithCodeAsync(string model, string prompt, string code)
        {
            var fullPrompt =
                "Tu es MOTO AI, un assistant de développement.\n" +
                $"Demande : {prompt}\n\n" +
                "Code actuel :\n" + code + "\n\n" +
                "Réponds avec le code COMPLET modifié dans un bloc ``` , sans explication.";

            // ★ CORRECTION (02/09, revue croisée) : le paramètre `model` de cette
            // méthode (bandeau IA inline de l'éditeur, indépendant du panneau de
            // chat) n'était jusqu'ici jamais utilisé — le routage retombait sur le
            // PreferInternal PARTAGÉ du panneau de chat, donc changer de modèle dans
            // un des deux endroits affectait silencieusement l'autre. Chacun calcule
            // maintenant sa propre préférence interne/externe à partir de SON propre
            // modèle sélectionné.
            return await RunTrackedAsync("Bandeau IA (code)", model,
                () => RouteAsync(fullPrompt, !IsExternalProviderName(model)));
        }

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

            var fallbackResult = await _fallback.GenerateAsync(prompt, WorkspaceRoot);
            return fallbackResult.Success
                ? fallbackResult.Content
                : "Aucun moteur IA disponible (Ollama et fallback injoignables). Vérifie tes paramètres IA.";
        }
    }
}
