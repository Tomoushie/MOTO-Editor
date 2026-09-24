// Moto.Core/Moto.AI/Generation/ChatPrompts.cs
// ★ AJOUT (24/09, "écriture générative fonctionnelle") : ce qui part vers le modèle pour une réponse de CHAT — historique de la conversation,
// fichier ouvert dans l'éditeur, sélection, pièces jointes — tenu dans la fenêtre de contexte demandée.
// Avant : chaque question partait SEULE (aucun historique, le fichier ouvert n'était jamais envoyé), par /api/generate sans fenêtre de contexte :
// Ollama gardait ~4 000 jetons et TRONQUAIT en silence le début du prompt — donc la consigne — dès qu'une pièce jointe était un peu longue.
//
// Disposition choisie sur mesure (24/09, qwen2.5-coder:7b, fichier de ~5 000 jetons) : le fichier ouvert dans le message SYSTÈME, la question —
// avec sa sélection et ses pièces jointes — dans le DERNIER message. D'une question à la suivante, tout ce qui précède la nouvelle question est
// alors identique octet pour octet : Ollama reprend ce qu'il a déjà lu et commence à répondre en ~0,1 s, au lieu de ~2 s quand le fichier
// accompagnait chaque question (il le relisait à chaque fois). Cela suppose que l'historique rejoue EXACTEMENT ce qui a été envoyé
// (ChatPrompt.SentUserMessage) et exactement ce que le modèle a écrit.
using System.Text;
using Moto.Core.AI.Llm;

namespace Moto.Core.AI.Generation;

/// <summary>
/// Un tour de conversation déjà terminé. <see cref="Role"/> : « user », ou « assistant » (« ai », le nom utilisé par l'éditeur, est accepté ;
/// tout autre rôle est ignoré). <see cref="Content"/> : ce qui a été envoyé au modèle (question + contexte) ou ce qu'il a écrit.
/// <see cref="Typed"/> : pour une question, le seul texte tapé — c'est lui qui part vers un fournisseur en ligne, jamais le contexte joint
/// automatiquement.
/// </summary>
public sealed record ChatTurn(string Role, string Content, string? Typed = null);

/// <summary>Un texte joint à la question par l'utilisateur (📎 fichier ou sélection).</summary>
public sealed record ChatAttachment(string Name, string Text);

public sealed class ChatRequest
{
    /// <summary>La question posée.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Tours précédents, du plus ancien au plus récent, sans la question en cours.</summary>
    public IReadOnlyList<ChatTurn> History { get; init; } = Array.Empty<ChatTurn>();

    /// <summary>Le fichier affiché dans l'éditeur (chemin ou titre) et son texte ACTUEL. Texte null : ne pas l'envoyer.</summary>
    public string? FilePath { get; init; }
    public string? FileText { get; init; }

    /// <summary>Texte sélectionné dans l'éditeur.</summary>
    public string? Selection { get; init; }

    public IReadOnlyList<ChatAttachment> Attachments { get; init; } = Array.Empty<ChatAttachment>();
}

/// <param name="Messages">La conversation à envoyer : consigne système (avec le fichier ouvert), tours retenus, question avec son contexte.</param>
/// <param name="NumCtx">Fenêtre de contexte à demander.</param>
/// <param name="MaxOutputTokens">Plafond de jetons générés.</param>
/// <param name="SentUserMessage">Le dernier message tel qu'envoyé : à garder pour le rejouer tel quel dans l'historique des tours suivants.</param>
/// <param name="Notes">Ce qui n'est PAS parti tel quel (fichier tronqué, anciens messages oubliés…) : à montrer à l'utilisateur.</param>
/// <param name="SentContext">Ce qui est parti en plus de la question (« fichier ouvert (A.cs) », « sélection »…) : ce que le modèle a vu.</param>
public sealed record ChatPrompt(IReadOnlyList<LlmMessage> Messages, int NumCtx, int MaxOutputTokens, string SentUserMessage,
    IReadOnlyList<string> Notes, IReadOnlyList<string> SentContext);

public static class ChatPrompts
{
    public const int SmallContext = InlineEditPrompts.SmallContext;
    public const int LargeContext = InlineEditPrompts.LargeContext;

    /// <summary>Plafond de jetons d'une réponse : assez pour un fichier de quelques centaines de lignes, pas de quoi tourner des minutes.</summary>
    public const int MaxOutputTokens = 3072;

    /// <summary>Balises du gabarit de conversation, par message (estimation prudente).</summary>
    public const int MessageOverhead = 12;

    /// <summary>Total des blocs de contexte (sélection, pièces jointes, fichier ouvert) : ≈ 10 000 jetons, pour rester dans la fenêtre de
    /// 16 384 avec la réponse et un peu d'historique.</summary>
    private const int ContextCharsBudget = 30_000;
    private const int MaxFileChars = 24_000;
    private const int MaxBlockChars = 12_000;   // sélection ou pièce jointe
    private const int MinFileChars = 1_500;     // en dessous, un fichier tronqué ne sert à rien : on ne l'envoie pas
    private const int MaxTurnChars = 8_000;     // un ancien message géant ne mange pas toute la mémoire (coupé toujours au même endroit)

    /// <summary>Identité de l'IA : envoyée comme vrai message « system » (sinon le modèle se présente sous son nom de base, ex. Qwen).</summary>
    public const string Identity =
        "Tu es MOTO AI, l'assistant intégré à MOTO Editor, un IDE léger conçu par MOTO Software (fondé par Tom Nowak), inspiré de Zed et VS Code. " +
        "Réponds toujours à la première personne (\"je\"), en français naturel.";

    public const string SystemText =
        Identity + " Sois concis. " +
        "Quand tu écris du code, mets-le dans un bloc ``` avec le langage et, s'il est destiné à un fichier précis, le nom de ce fichier juste après " +
        "(par exemple ```csharp Program.cs) ; écris le fichier ou la fonction EN ENTIER, sans abréger (pas de « ... », pas de « reste du code inchangé »). " +
        "Tu ne vois que ce qu'on te montre ici (fichier ouvert, sélection, pièces jointes), pas les autres fichiers du projet.";

    /// <summary>Environ 3 caractères par jeton pour du code : volontairement prudent (on préfère surestimer).</summary>
    public static int EstimateTokens(string text) => (text.Length + 2) / 3;

    /// <summary>
    /// Construit la conversation à envoyer à un modèle local, ou renvoie null avec <paramref name="problem"/> si la question elle-même ne tient
    /// pas dans la mémoire du modèle. <paramref name="maxContext"/> : la plus grande fenêtre que le modèle accepte.
    /// </summary>
    public static ChatPrompt? Build(ChatRequest request, int maxContext, out string? problem)
        => Build(request, maxContext, online: false, out problem);

    /// <summary>
    /// Même conversation pour un fournisseur EN LIGNE (OpenAI, Anthropic, Mistral…) : la question, ses pièces jointes et l'historique — mais ni le
    /// fichier ouvert ni la sélection, et, dans l'historique, les questions telles que tapées (<see cref="ChatTurn.Typed"/>) sans le contexte que
    /// l'éditeur y avait joint pour le modèle local. Le code ne quitte la machine que si l'utilisateur l'a joint lui-même (📎).
    /// Tenue dans une fenêtre de 16 384 jetons : le coût d'un appel en ligne reste borné.
    /// </summary>
    public static ChatPrompt? BuildForOnline(ChatRequest request, out string? problem)
        => Build(request, SmallContext, online: true, out problem);

    /// <summary>La conversation en un seul texte, pour un fournisseur qui ne prend qu'un message (FallbackEngine).</summary>
    public static string Flatten(ChatPrompt prompt)
    {
        var sb = new StringBuilder(prompt.Messages[0].Content).Append("\n\n");
        var history = prompt.Messages.Skip(1).Take(prompt.Messages.Count - 2).ToList();
        if (history.Count > 0)
        {
            sb.Append("Conversation jusqu'ici :\n\n");
            foreach (var m in history)
                sb.Append(m.Role == "user" ? "Utilisateur : " : "MOTO AI : ").Append(m.Content).Append("\n\n");
            sb.Append("Nouveau message de l'utilisateur :\n");
        }
        return sb.Append(prompt.Messages[^1].Content).ToString();
    }

    // ── Construction ────────────────────────────────────────────────────────

    private static ChatPrompt? Build(ChatRequest request, int maxContext, bool online, out string? problem)
    {
        problem = null;
        var notes = new List<string>();
        var sent = new List<string>();
        var name = DisplayName(request.FilePath);

        if (online)
        {
            if (!string.IsNullOrWhiteSpace(request.FileText))
                notes.Add($"Le fichier ouvert « {name} » n'a pas été envoyé au service en ligne (ton code reste sur ta machine) : joins-le avec 📎 s'il doit le lire.");
            if (!string.IsNullOrWhiteSpace(request.Selection))
                notes.Add("La sélection n'a pas été envoyée au service en ligne : joins-la avec 📎 s'il doit la lire.");
        }

        // Ce que l'utilisateur a désigné (sélection, pièces jointes) passe avant le fichier entier.
        var remaining = ContextCharsBudget;
        var user = BuildUserMessage(request, online ? null : request.Selection, name, ref remaining, notes, sent);
        var system = SystemText + (online ? string.Empty : BuildFileBlock(request, name, remaining, notes, sent));

        // Le fichier est DANS le message système : il compte dans la partie fixe, sinon l'historique déborderait la fenêtre et Ollama
        // couperait le début en silence — exactement le défaut que ce fichier corrige.
        var fixedTokens = EstimateTokens(system) + EstimateTokens(user) + 2 * MessageOverhead + 36;
        var reserve = MaxOutputTokens + 128;

        // Deux tailles seulement : quand la taille demandée change d'un appel à l'autre, Ollama recharge le modèle (~13 s pour un 7B). On préfère
        // donc oublier de vieux messages plutôt que passer à la grande fenêtre pour eux : celle-ci n'est demandée que si la QUESTION l'exige.
        var small = Math.Min(SmallContext, maxContext);
        int numCtx;
        if (fixedTokens + reserve <= small) numCtx = small;
        else if (maxContext >= LargeContext && fixedTokens + reserve <= LargeContext) numCtx = LargeContext;
        else
        {
            problem = $"Ton message est trop gros pour la mémoire de travail du modèle (environ {fixedTokens + reserve} jetons, il en accepte {maxContext}) : "
                    + "raccourcis-le, ou joins moins de texte.";
            return null;
        }

        var history = PickHistory(request.History, online, numCtx - fixedTokens - reserve, out var historyTokens, out var dropped);
        if (dropped > 0)
            notes.Add($"{dropped} message(s) plus ancien(s) de la conversation n'ont pas été renvoyés au modèle (mémoire pleine) : il ne s'en souvient plus.");
        if (history.Count > 0)
            sent.Add($"{history.Count} message(s) précédent(s)");

        var messages = new List<LlmMessage>(history.Count + 2) { LlmMessage.System(system) };
        messages.AddRange(history);
        messages.Add(LlmMessage.User(user));

        var maxOutput = Math.Min(MaxOutputTokens, numCtx - fixedTokens - historyTokens - 64);
        return new ChatPrompt(messages, numCtx, maxOutput, user, notes, sent);
    }

    /// <summary>Contexte d'abord, question en dernier : c'est la dernière chose que le modèle lit avant de répondre.</summary>
    private static string BuildUserMessage(ChatRequest request, string? selection, string name, ref int remaining, List<string> notes, List<string> sent)
    {
        var blocks = new List<string>();

        if (!string.IsNullOrWhiteSpace(selection))
        {
            var text = Fit(selection, Math.Min(MaxBlockChars, remaining), "La sélection", notes);
            remaining -= text.Length;
            blocks.Add($"Sélection dans {name} :\n{InlineEditPrompts.Fenced(text)}");
            sent.Add("sélection");
        }

        foreach (var attachment in request.Attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.Text)) continue;
            if (remaining < MinFileChars)
            {
                notes.Add($"« {attachment.Name} » n'a pas été envoyé : il ne restait plus de place.");
                continue;
            }

            var text = Fit(attachment.Text, Math.Min(MaxBlockChars, remaining), $"« {attachment.Name} »", notes);
            remaining -= text.Length;
            blocks.Add($"Pièce jointe — {attachment.Name} :\n{InlineEditPrompts.Fenced(text)}");
            sent.Add($"pièce jointe ({attachment.Name})");
        }

        var message = request.Message.Trim();
        return blocks.Count == 0 ? message : string.Join("\n\n", blocks) + "\n\n---\n" + message;
    }

    private static string BuildFileBlock(ChatRequest request, string name, int remaining, List<string> notes, List<string> sent)
    {
        if (string.IsNullOrWhiteSpace(request.FileText)) return string.Empty;

        if (remaining < MinFileChars)
        {
            notes.Add($"Le fichier ouvert « {name} » n'a pas été envoyé : la sélection et les pièces jointes prenaient déjà toute la place.");
            return string.Empty;
        }

        var text = Fit(request.FileText, Math.Min(MaxFileChars, remaining), $"Le fichier ouvert « {name} »", notes);
        sent.Add($"fichier ouvert ({name})");
        return $"\n\nFichier ouvert dans l'éditeur : {name} ({CodeBlocks.LanguageLabel(request.FilePath ?? name)}). "
             + $"Sers-t'en quand la question le concerne.\n{InlineEditPrompts.Fenced(text)}";
    }

    /// <summary>Les tours les plus récents qui tiennent dans <paramref name="room"/> jetons, dans l'ordre.</summary>
    private static List<LlmMessage> PickHistory(IReadOnlyList<ChatTurn> turns, bool online, int room, out int usedTokens, out int dropped)
    {
        var usable = new List<LlmMessage>();
        foreach (var turn in turns)
        {
            var role = NormalizeRole(turn.Role);
            if (role is null) continue;

            var content = online && role == "user" ? turn.Typed ?? turn.Content : turn.Content;
            if (string.IsNullOrWhiteSpace(content)) continue;
            usable.Add(new LlmMessage { Role = role, Content = Clip(content) });
        }

        var kept = new List<LlmMessage>();
        usedTokens = 0;
        for (var i = usable.Count - 1; i >= 0; i--)
        {
            var cost = EstimateTokens(usable[i].Content) + MessageOverhead;
            if (usedTokens + cost > room) break;
            usedTokens += cost;
            kept.Insert(0, usable[i]);
        }

        // Une réponse dont la question a été oubliée n'éclaire rien : on la retire aussi.
        while (kept.Count > 0 && kept[0].Role == "assistant")
        {
            usedTokens -= EstimateTokens(kept[0].Content) + MessageOverhead;
            kept.RemoveAt(0);
        }

        dropped = usable.Count - kept.Count;
        return kept;
    }

    private static string? NormalizeRole(string? role) => role?.Trim().ToLowerInvariant() switch
    {
        "user" => "user",
        "assistant" or "ai" => "assistant",
        _ => null,
    };

    private static string DisplayName(string? path) => string.IsNullOrWhiteSpace(path) ? "sans nom" : Path.GetFileName(path);

    /// <summary>Le texte, coupé à <paramref name="max"/> caractères avec un marqueur explicite (et une note pour l'utilisateur) s'il est plus long.</summary>
    private static string Fit(string text, int max, string label, List<string> notes)
    {
        text = InlineEditPlanner.Normalize(text);
        if (text.Length <= max) return text;

        notes.Add($"{label} est long : seuls ses {max} premiers caractères ont été envoyés.");
        return text[..max] + $"\n… (tronqué : {text.Length - max} caractères non envoyés)";
    }

    private static string Clip(string content)
        => content.Length <= MaxTurnChars ? content : content[..MaxTurnChars] + "\n… (message tronqué)";
}
