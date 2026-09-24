// Moto.Core.Tests/AI/Generation/InlineEditServiceTests.cs
// Le chemin complet, contre un faux serveur Ollama (aucun vrai modèle) : ce qui part vers le serveur (fenêtre de contexte, flux, plafond de
// jetons, consigne), le choix du modèle, les pannes lisibles, et le chemin « fournisseur externe ».
using System.Text.Json.Nodes;
using Moto.Core.AI.Generation;
using Moto.Core.AI.Llm;
using Moto.Core.Tests.AI.V2;
using Xunit;

namespace Moto.Core.Tests.AI.Generation;

public class InlineEditServiceTests
{
    private const string Doc = "class A\n{\n    int x = 1;\n    int y = 2;\n}\n";

    private static InlineEditService Service(FakeOllamaHandler fake, string model = "qwen2.5-coder:7b")
        => new(endpoint => new OllamaChatClient(endpoint, fake), () => new GenerationSettings { Endpoint = "http://127.0.0.1:11434", Model = model });

    private static InlineEditRequest Request(string? selection = "    int x = 1;", string instruction = "mets x à 5")
        => new() { DisplayPath = "src/A.cs", DocumentText = Doc, Selection = selection, Instruction = instruction };

    [Fact]
    public async Task A_local_edit_streams_and_yields_a_plan()
    {
        var fake = new FakeOllamaHandler().Text("Voilà :\n```csharp\n    int x = 5;\n```");
        var messages = new List<string>();

        var outcome = await Service(fake).RunAsync(Request(), messages.Add);

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Equal("class A\n{\n    int x = 5;\n    int y = 2;\n}\n", outcome.Plan!.NewText);
        Assert.Equal("qwen2.5-coder:7b", outcome.Model);
        Assert.Null(outcome.Note);
        Assert.Contains(messages, m => m.Contains("Chargement du modèle"));
    }

    [Fact]
    public async Task The_request_asks_for_a_context_window_a_stream_and_a_token_cap()
    {
        var fake = new FakeOllamaHandler().Text("```csharp\n    int x = 5;\n```");

        await Service(fake).RunAsync(Request());

        var body = Assert.Single(fake.ChatRequests);
        Assert.Equal("qwen2.5-coder:7b", (string?)body["model"]);
        Assert.True((bool?)body["stream"]);

        var options = (JsonObject)body["options"]!;
        Assert.Equal(InlineEditPrompts.SmallContext, (int?)options["num_ctx"]); // pas les 4096 d'Ollama par défaut
        Assert.Equal(0.2, (double?)options["temperature"]);
        Assert.InRange((int)options["num_predict"]!, 400, InlineEditPrompts.SmallContext);

        var messages = (JsonArray)body["messages"]!;
        Assert.Equal(2, messages.Count);
        Assert.Equal("system", (string?)messages[0]!["role"]);
        var user = (string)messages[1]!["content"]!;
        Assert.Contains("PASSAGE À MODIFIER", user);
        Assert.Contains("    int x = 1;", user);
        Assert.Contains("mets x à 5", user);
        Assert.Contains("A.cs (C#)", user);
    }

    [Fact]
    public async Task An_installed_alternative_is_used_when_the_configured_model_is_missing_and_the_note_says_so()
    {
        var fake = new FakeOllamaHandler().Text("```csharp\n    int x = 5;\n```");
        fake.Models.Clear();
        fake.Models.Add("qwen3:8b");

        var outcome = await Service(fake, model: "absent:7b").RunAsync(Request());

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Equal("qwen3:8b", outcome.Model);
        Assert.Contains("« absent:7b » n'est pas installé", outcome.Note);
        Assert.Equal("qwen3:8b", (string?)fake.ChatRequests[0]["model"]);
    }

    [Fact]
    public async Task No_installed_model_gives_a_helpful_message()
    {
        var fake = new FakeOllamaHandler();
        fake.Models.Clear();

        var outcome = await Service(fake).RunAsync(Request());

        Assert.False(outcome.Succeeded);
        Assert.Contains("ollama pull", outcome.Problem);
        Assert.Empty(fake.ChatRequests);
    }

    [Fact]
    public async Task Ollama_being_off_is_reported_plainly_and_nothing_is_planned()
    {
        var fake = new FakeOllamaHandler { Unreachable = true };

        var outcome = await Service(fake).RunAsync(Request());

        Assert.False(outcome.Succeeded);
        Assert.Contains("Ollama est injoignable", outcome.Problem);
        Assert.Contains("Rien n'a été modifié", outcome.Problem);
    }

    [Fact]
    public async Task A_reply_that_cannot_be_applied_is_refused_by_the_planner_not_applied()
    {
        var fake = new FakeOllamaHandler().Text("Bien sûr ! Je te conseille de renommer x en count.");

        var outcome = await Service(fake).RunAsync(Request());

        Assert.False(outcome.Succeeded);
        Assert.Contains("pas renvoyé de code", outcome.Problem);
        Assert.Equal("qwen2.5-coder:7b", outcome.Model);
    }

    [Fact]
    public async Task A_missing_instruction_is_refused_before_calling_the_model()
    {
        var fake = new FakeOllamaHandler();

        var outcome = await Service(fake).RunAsync(Request(instruction: "  "));

        Assert.False(outcome.Succeeded);
        Assert.Empty(fake.ChatRequests);
    }

    [Fact]
    public async Task A_huge_text_is_sent_back_to_the_agent_idea_without_calling_the_model()
    {
        var fake = new FakeOllamaHandler();
        var request = new InlineEditRequest
        {
            DisplayPath = "Big.cs",
            DocumentText = new string('x', InlineEditPlanner.MaxTargetChars + 10),
            Instruction = "refactore tout",
        };

        var outcome = await Service(fake).RunAsync(request);

        Assert.False(outcome.Succeeded);
        Assert.Contains("/agent", outcome.Problem);
        Assert.Empty(fake.ChatRequests);
    }

    [Fact]
    public async Task Cancellation_propagates_instead_of_becoming_a_message()
    {
        var fake = new FakeOllamaHandler().Text("```csharp\n    int x = 5;\n```");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(fake).RunAsync(Request(), ct: cts.Token));
    }

    // ── Fournisseur externe ─────────────────────────────────────────────────

    [Fact]
    public async Task An_external_provider_reply_goes_through_the_same_checks()
    {
        string? sent = null;

        var outcome = await new InlineEditService().RunWithAsync(Request(), "OpenAI", (prompt, _) =>
        {
            sent = prompt;
            return Task.FromResult("```csharp\n    int x = 5;\n```");
        });

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Equal("OpenAI", outcome.Model);
        Assert.Contains("PASSAGE À MODIFIER", sent);
        Assert.Contains(InlineEditPrompts.SystemText, sent);
    }

    [Fact]
    public async Task An_external_provider_reply_that_is_abbreviated_is_refused()
    {
        var outcome = await new InlineEditService().RunWithAsync(Request(), "OpenAI",
            (_, _) => Task.FromResult("```csharp\n    int x = 5;\n    // ... reste inchangé\n```"));

        Assert.False(outcome.Succeeded);
        Assert.Contains("abrégé", outcome.Problem);
    }

    [Fact]
    public async Task An_external_provider_failing_gives_a_readable_message()
    {
        var outcome = await new InlineEditService().RunWithAsync(Request(), "Anthropic",
            (_, _) => throw new HttpRequestException("401"));

        Assert.False(outcome.Succeeded);
        Assert.Contains("fournisseur", outcome.Problem);
        Assert.Contains("401", outcome.Problem);
    }
}

public class InlineEditPromptsTests
{
    private static InlineEditRequest Req(string doc, string? selection = null, string path = "src/Foo.cs")
        => new() { DisplayPath = path, DocumentText = doc, Selection = selection, Instruction = "améliore" };

    [Fact]
    public void A_selection_prompt_shows_the_passage_and_its_neighbourhood()
    {
        var doc = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"ligne {i}")) + "\n";

        var prompt = InlineEditPrompts.Build(Req(doc, "ligne 50"), InlineEditPrompts.LargeContext, out var problem)!;

        Assert.Null(problem);
        Assert.Contains("PASSAGE À MODIFIER", prompt.User);
        Assert.Contains("Contexte AVANT", prompt.User);
        Assert.Contains("ligne 49", prompt.User);
        Assert.DoesNotContain("ligne 9\n", prompt.User); // le contexte est borné (40 lignes avant) : pas tout le fichier
        Assert.Contains("Contexte APRÈS", prompt.User);
        Assert.Contains("ligne 51", prompt.User);
        Assert.DoesNotContain("ligne 90", prompt.User);
        Assert.Contains("Foo.cs (C#)", prompt.User);
    }

    [Fact]
    public void A_whole_file_prompt_gives_the_whole_file_and_asks_for_all_of_it_back()
    {
        var prompt = InlineEditPrompts.Build(Req("class A { }\n"), InlineEditPrompts.LargeContext, out _)!;

        Assert.Contains("CONTENU ACTUEL", prompt.User);
        Assert.Contains("class A { }", prompt.User);
        Assert.Contains("COMPLET", prompt.User);
    }

    [Fact]
    public void An_empty_file_says_so()
    {
        var prompt = InlineEditPrompts.Build(Req(""), InlineEditPrompts.LargeContext, out _)!;

        Assert.Contains("(fichier vide)", prompt.User);
    }

    [Fact]
    public void The_fence_grows_when_the_text_contains_one()
    {
        var doc = "# Titre\n```bash\nls\n```\n";

        var prompt = InlineEditPrompts.Build(Req(doc, path: "README.md"), InlineEditPrompts.LargeContext, out _)!;

        Assert.Contains("````\n# Titre", prompt.User);
    }

    [Fact]
    public void Too_long_a_text_is_sent_to_the_agent_idea()
    {
        var prompt = InlineEditPrompts.Build(Req(new string('a', InlineEditPlanner.MaxTargetChars + 1)), InlineEditPrompts.LargeContext, out var problem);

        Assert.Null(prompt);
        Assert.Contains("/agent", problem);
    }

    [Fact]
    public void A_small_request_uses_the_16k_window_the_agent_also_uses_so_the_loaded_model_is_reused()
    {
        var prompt = InlineEditPrompts.Build(Req("class A { }\n"), InlineEditPrompts.LargeContext, out _)!;

        Assert.Equal(16384, prompt.NumCtx);
        Assert.True(prompt.MaxOutputTokens > 0);
    }

    [Fact]
    public void A_big_request_moves_to_the_32k_window()
    {
        var doc = new string('x', 20_000);

        var prompt = InlineEditPrompts.Build(Req(doc), InlineEditPrompts.LargeContext, out _)!;

        Assert.Equal(32768, prompt.NumCtx);
        Assert.True(InlineEditPrompts.EstimateTokens(prompt.User) + prompt.MaxOutputTokens < prompt.NumCtx);
    }

    [Fact]
    public void A_model_with_a_small_window_refuses_a_request_that_does_not_fit()
    {
        var doc = new string('x', 20_000);

        var prompt = InlineEditPrompts.Build(Req(doc), maxContext: 8192, out var problem);

        Assert.Null(prompt);
        Assert.Contains("trop grosse", problem);
    }

    [Fact]
    public void The_output_cap_never_exceeds_what_is_left_of_the_window()
    {
        foreach (var size in new[] { 100, 5_000, 12_000, 20_000, 29_000 })
        {
            var prompt = InlineEditPrompts.Build(Req(new string('x', size)), InlineEditPrompts.LargeContext, out _);
            if (prompt is null) continue;

            Assert.True(InlineEditPrompts.EstimateTokens(prompt.User) + prompt.MaxOutputTokens < prompt.NumCtx, $"size={size}");
        }
    }
}

public class GenerationModelsTests
{
    [Fact]
    public void The_configured_model_wins_when_installed()
        => Assert.Equal(("qwen3:8b", (string?)null), GenerationModels.Choose("qwen3:8b", new[] { "qwen2.5-coder:7b", "qwen3:8b" }));

    [Fact]
    public void A_name_without_tag_matches_latest()
        => Assert.Equal(("llama3", (string?)null), GenerationModels.Choose("llama3", new[] { "llama3:latest" }));

    [Fact]
    public void A_missing_model_falls_back_to_a_known_good_one_and_says_so()
    {
        var (model, note) = GenerationModels.Choose("absent:1b", new[] { "phi3:mini", "qwen3:8b" });

        Assert.Equal("qwen3:8b", model);
        Assert.Contains("absent:1b", note);
    }

    [Fact]
    public void An_unknown_only_model_is_still_used_rather_than_nothing()
    {
        var (model, note) = GenerationModels.Choose("", new[] { "phi3:mini" });

        Assert.Equal("phi3:mini", model);
        Assert.Contains("aucun modèle réglé", note);
    }

    [Fact]
    public void Nothing_installed_gives_no_model()
        => Assert.Equal(((string?)null, (string?)null), GenerationModels.Choose("qwen3:8b", Array.Empty<string>()));
}
