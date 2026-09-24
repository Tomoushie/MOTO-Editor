// Moto.Core.Tests/AI/Generation/ChatPromptsTests.cs
// Ce qui part vers le modèle pour une réponse de chat : où va le fichier ouvert (message système), où vont la sélection et les pièces jointes
// (avec la question), ce que l'historique rejoue (octet pour octet, pour qu'Ollama reprenne ce qu'il a déjà lu), ce qui est oublié quand la
// mémoire est pleine — et ce qui ne part JAMAIS vers un service en ligne.
using Moto.Core.AI.Generation;
using Moto.Core.AI.Llm;
using Xunit;

namespace Moto.Core.Tests.AI.Generation;

public class ChatPromptsTests
{
    private const string File = "class A\n{\n    int x = 1;\n}\n";

    private static ChatRequest Req(string message = "Que fait ce fichier ?", string? file = File, string? selection = null,
        IReadOnlyList<ChatTurn>? history = null, IReadOnlyList<ChatAttachment>? attachments = null, string path = "src/A.cs")
        => new()
        {
            Message = message,
            FilePath = path,
            FileText = file,
            Selection = selection,
            History = history ?? Array.Empty<ChatTurn>(),
            Attachments = attachments ?? Array.Empty<ChatAttachment>(),
        };

    private static ChatPrompt Build(ChatRequest request, int maxContext = ChatPrompts.LargeContext)
    {
        var prompt = ChatPrompts.Build(request, maxContext, out var problem);
        Assert.Null(problem);
        return prompt!;
    }

    private static int Tokens(ChatPrompt prompt)
        => prompt.Messages.Sum(m => ChatPrompts.EstimateTokens(m.Content) + ChatPrompts.MessageOverhead) + 36;

    [Fact]
    public void The_open_file_goes_in_the_system_message_and_the_question_comes_last()
    {
        var prompt = Build(Req());

        Assert.Equal(2, prompt.Messages.Count);
        Assert.Equal("system", prompt.Messages[0].Role);
        Assert.StartsWith(ChatPrompts.SystemText, prompt.Messages[0].Content);
        Assert.Contains("int x = 1;", prompt.Messages[0].Content);
        Assert.Contains("A.cs (C#)", prompt.Messages[0].Content);

        var user = prompt.Messages[^1];
        Assert.Equal("user", user.Role);
        Assert.Equal("Que fait ce fichier ?", user.Content);
        Assert.Equal(user.Content, prompt.SentUserMessage);
        Assert.Contains("fichier ouvert (A.cs)", prompt.SentContext);
    }

    [Fact]
    public void Without_a_file_the_system_message_is_only_the_instructions()
    {
        var prompt = Build(Req(file: null));

        Assert.Equal(ChatPrompts.SystemText, prompt.Messages[0].Content);
        Assert.Empty(prompt.SentContext);
    }

    [Fact]
    public void The_selection_and_the_attachments_travel_with_the_question_the_question_last()
    {
        var prompt = Build(Req(message: "Explique ça", selection: "int x = 1;",
            attachments: new[] { new ChatAttachment("B.cs", "class B { }") }));

        var user = prompt.Messages[^1].Content;
        Assert.Contains("Sélection dans A.cs", user);
        Assert.Contains("Pièce jointe — B.cs", user);
        Assert.Contains("class B { }", user);
        Assert.EndsWith("---\nExplique ça", user);
        Assert.DoesNotContain("class B", prompt.Messages[0].Content);
        Assert.Contains("sélection", prompt.SentContext);
        Assert.Contains("pièce jointe (B.cs)", prompt.SentContext);
    }

    [Fact]
    public void History_is_replayed_in_order_and_the_editor_role_ai_becomes_assistant()
    {
        var prompt = Build(Req(history: new[]
        {
            new ChatTurn("user", "Bonjour"),
            new ChatTurn("ai", "Bonjour, je suis MOTO AI."),
            new ChatTurn("system", "⚠ Ollama est injoignable."), // message de l'éditeur, pas du modèle : ignoré
            new ChatTurn("user", "   "),                         // vide : ignoré
        }));

        Assert.Equal(new[] { "system", "user", "assistant", "user" }, prompt.Messages.Select(m => m.Role));
        Assert.Equal("Bonjour", prompt.Messages[1].Content);
        Assert.Equal("Bonjour, je suis MOTO AI.", prompt.Messages[2].Content);
        Assert.Contains("2 message(s) précédent(s)", prompt.SentContext);
    }

    [Fact]
    public void A_follow_up_repeats_the_previous_exchange_byte_for_byte_so_ollama_can_reuse_what_it_read()
    {
        var first = Build(Req(message: "Explique", selection: "int x = 1;"));
        const string answer = "Ce passage déclare x.";

        var second = Build(Req(message: "Et ensuite ?", history: new[]
        {
            new ChatTurn("user", first.SentUserMessage, Typed: "Explique"),
            new ChatTurn("assistant", answer),
        }));

        Assert.Equal(first.Messages.Count + 2, second.Messages.Count);
        for (var i = 0; i < first.Messages.Count; i++)
        {
            Assert.Equal(first.Messages[i].Role, second.Messages[i].Role);
            Assert.Equal(first.Messages[i].Content, second.Messages[i].Content);
        }
        Assert.Equal(answer, second.Messages[first.Messages.Count].Content);
        Assert.Equal("Et ensuite ?", second.Messages[^1].Content);
    }

    [Fact]
    public void A_giant_old_message_is_cut_the_same_way_every_time()
    {
        var turns = new[] { new ChatTurn("user", new string('a', 20_000)), new ChatTurn("assistant", "ok") };

        var a = Build(Req(file: null, history: turns));
        var b = Build(Req(file: null, message: "autre chose", history: turns));

        Assert.Equal(a.Messages[1].Content, b.Messages[1].Content);
        Assert.EndsWith("(message tronqué)", a.Messages[1].Content);
    }

    [Fact]
    public void When_memory_is_full_the_oldest_messages_are_forgotten_first_and_the_user_is_told()
    {
        var history = Enumerable.Range(1, 40)
            .SelectMany(i => new[] { new ChatTurn("user", $"question {i} " + new string('q', 3000)), new ChatTurn("assistant", $"réponse {i} " + new string('r', 3000)) })
            .ToList();

        var prompt = Build(Req(history: history));

        Assert.Equal(ChatPrompts.SmallContext, prompt.NumCtx); // on oublie plutôt que de changer de taille (rechargement du modèle)
        Assert.Contains(prompt.Messages, m => m.Content.StartsWith("réponse 40 ", StringComparison.Ordinal));
        Assert.DoesNotContain(prompt.Messages, m => m.Content.StartsWith("question 1 ", StringComparison.Ordinal));
        Assert.Contains(prompt.Notes, n => n.Contains("plus ancien") && n.Contains("ne s'en souvient plus"));
        Assert.Equal("user", prompt.Messages[1].Role); // jamais une réponse dont la question a été oubliée
    }

    [Fact]
    public void The_budget_counts_the_open_file_in_the_system_message()
    {
        // Fichier de 24 000 caractères (~8 000 jetons) + long historique : sans compter le fichier, l'historique remplirait toute la fenêtre.
        var history = Enumerable.Range(1, 30)
            .SelectMany(i => new[] { new ChatTurn("user", new string('q', 2000)), new ChatTurn("assistant", new string('r', 2000)) })
            .ToList();

        var prompt = Build(Req(file: new string('f', 24_000), history: history));

        Assert.True(Tokens(prompt) + prompt.MaxOutputTokens <= prompt.NumCtx, $"{Tokens(prompt)} + {prompt.MaxOutputTokens} > {prompt.NumCtx}");
        Assert.Equal(ChatPrompts.MaxOutputTokens, prompt.MaxOutputTokens);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(5_000, 0, 10)]
    [InlineData(24_000, 0, 60)]
    [InlineData(40_000, 12_000, 60)]
    [InlineData(24_000, 30_000, 5)]
    [InlineData(1_000, 11_000, 200)]
    public void The_output_cap_never_exceeds_what_is_left_of_the_window(int fileChars, int selectionChars, int turns)
    {
        var history = Enumerable.Range(1, turns)
            .SelectMany(i => new[] { new ChatTurn("user", new string('q', 700)), new ChatTurn("assistant", new string('r', 1500)) })
            .ToList();

        var prompt = Build(Req(file: fileChars == 0 ? null : new string('f', fileChars),
            selection: selectionChars == 0 ? null : new string('s', selectionChars), history: history));

        Assert.True(Tokens(prompt) + prompt.MaxOutputTokens <= prompt.NumCtx, $"{Tokens(prompt)} + {prompt.MaxOutputTokens} > {prompt.NumCtx}");
        Assert.True(prompt.MaxOutputTokens > 1000);
    }

    [Fact]
    public void A_huge_open_file_is_cut_with_a_visible_marker_and_a_note()
    {
        var prompt = Build(Req(file: new string('f', 100_000)));

        Assert.Contains("(tronqué :", prompt.Messages[0].Content);
        Assert.Contains(prompt.Notes, n => n.Contains("A.cs") && n.Contains("premiers caractères"));
        Assert.Equal(ChatPrompts.SmallContext, prompt.NumCtx);
    }

    [Fact]
    public void What_the_user_pointed_at_comes_before_the_whole_file()
    {
        var prompt = Build(Req(file: new string('f', 20_000), selection: new string('s', 12_000), attachments: new[]
        {
            new ChatAttachment("B.cs", new string('b', 12_000)),
            new ChatAttachment("C.cs", new string('c', 12_000)),
        }));

        Assert.Contains(new string('s', 12_000), prompt.Messages[^1].Content);
        Assert.Contains(new string('b', 1_000), prompt.Messages[^1].Content);
        Assert.DoesNotContain("fichier ouvert (A.cs)", prompt.SentContext);
        Assert.Contains(prompt.Notes, n => n.Contains("A.cs") && n.Contains("pas été envoyé"));
    }

    [Fact]
    public void A_markdown_file_gets_a_longer_fence()
    {
        var prompt = Build(Req(file: "# Titre\n```bash\nls\n```\n", path: "README.md"));

        Assert.Contains("````\n# Titre", prompt.Messages[0].Content);
    }

    [Fact]
    public void Only_a_huge_message_moves_to_the_32k_window_and_a_bigger_one_is_refused()
    {
        var big = Build(Req(file: null, message: new string('m', 50_000)));
        Assert.Equal(ChatPrompts.LargeContext, big.NumCtx);

        var refused = ChatPrompts.Build(Req(file: null, message: new string('m', 120_000)), ChatPrompts.LargeContext, out var problem);
        Assert.Null(refused);
        Assert.Contains("trop gros", problem);

        var smallModel = ChatPrompts.Build(Req(file: null, message: new string('m', 50_000)), maxContext: 8192, out var problem2);
        Assert.Null(smallModel);
        Assert.Contains("8192", problem2);
    }

    // ── Service en ligne ────────────────────────────────────────────────────

    [Fact]
    public void Online_neither_the_open_file_nor_the_selection_is_sent_and_the_user_is_told()
    {
        var prompt = ChatPrompts.BuildForOnline(Req(selection: "int x = 1;"), out var problem)!;

        Assert.Null(problem);
        Assert.All(prompt.Messages, m => Assert.DoesNotContain("int x = 1;", m.Content));
        Assert.Equal(ChatPrompts.SystemText, prompt.Messages[0].Content);
        Assert.Contains(prompt.Notes, n => n.Contains("fichier ouvert") && n.Contains("📎"));
        Assert.Contains(prompt.Notes, n => n.Contains("sélection") && n.Contains("📎"));
        Assert.Empty(prompt.SentContext);
    }

    [Fact]
    public void Online_attachments_chosen_by_the_user_are_sent()
    {
        var prompt = ChatPrompts.BuildForOnline(Req(attachments: new[] { new ChatAttachment("B.cs", "class B { }") }), out _)!;

        Assert.Contains("class B { }", prompt.Messages[^1].Content);
    }

    [Fact]
    public void Online_history_replays_what_was_typed_not_the_context_sent_to_the_local_model()
    {
        var local = Build(Req(message: "Explique", selection: "SECRET_SELECTION"));
        var prompt = ChatPrompts.BuildForOnline(Req(file: null, message: "Et ensuite ?", history: new[]
        {
            new ChatTurn("user", local.SentUserMessage, Typed: "Explique"),
            new ChatTurn("ai", "Ce passage déclare x."),
        }), out _)!;

        Assert.All(prompt.Messages, m => Assert.DoesNotContain("SECRET_SELECTION", m.Content));
        Assert.Equal("Explique", prompt.Messages[1].Content);
    }

    [Fact]
    public void Flatten_gives_one_text_with_the_conversation_and_the_new_question_last()
    {
        var prompt = ChatPrompts.BuildForOnline(Req(file: null, message: "Et ensuite ?", history: new[]
        {
            new ChatTurn("user", "Bonjour", Typed: "Bonjour"),
            new ChatTurn("assistant", "Salut !"),
        }), out _)!;

        var text = ChatPrompts.Flatten(prompt);

        Assert.StartsWith(ChatPrompts.SystemText, text);
        Assert.Contains("Utilisateur : Bonjour", text);
        Assert.Contains("MOTO AI : Salut !", text);
        Assert.EndsWith("Nouveau message de l'utilisateur :\nEt ensuite ?", text);
    }

    [Fact]
    public void Flatten_without_history_is_the_instructions_then_the_question()
        => Assert.Equal(ChatPrompts.SystemText + "\n\nBonjour",
            ChatPrompts.Flatten(ChatPrompts.BuildForOnline(Req(file: null, message: "Bonjour"), out _)!));
}
