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

        /// <summary>Éléments de contexte attachés à la PROCHAINE question envoyée
        /// (fichiers/sélections) — consommés (vidés) par SendAsync, pas persistés
        /// par thread : reflète l'usage "j'attache un truc, je pose ma question".</summary>
        public ObservableCollection<ChatContextItem> Contexts { get; } = new();

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

        /// <summary>Attache un fichier au contexte de la prochaine question.</summary>
        public void AddFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            Contexts.Add(new ChatContextItem { Kind = "file", Path = path });
        }

        /// <summary>Attache la sélection courante de l'éditeur au contexte de la
        /// prochaine question (via SelectionProvider, déjà utilisé par SendAsync).</summary>
        public void AddSelection()
        {
            var selection = SelectionProvider?.Invoke();
            if (string.IsNullOrWhiteSpace(selection)) return;
            Contexts.Add(new ChatContextItem { Kind = "selection", Content = selection });
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
            if (Contexts.Count > 0)
            {
                var contextBlock = string.Join("\n\n", Contexts.Select(BuildContextBlock));
                prompt = $"{prompt}\n\nContexte attaché :\n{contextBlock}";
                Contexts.Clear();
            }

            var response = await RouteAsync(prompt, PreferInternal);
            thread.Messages.Add(new ChatMessage { Role = "ai", Content = response });
            thread.LastActivityUtc = DateTime.UtcNow;
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
            return await RouteAsync(fullPrompt, !IsExternalProviderName(model));
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
                var kernelResponse = await _kernel.RouteAsync(prompt, 256, default);
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
