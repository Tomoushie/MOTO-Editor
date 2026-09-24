// Moto.Core.Tests/AI/Generation/ChatStreamServiceTests.cs
// La réponse de chat de bout en bout, contre un faux serveur Ollama (aucun vrai modèle) : le texte arrive par morceaux, ce qui part vers le
// serveur (fenêtre de contexte, flux, fichier seulement dans le message système, rôles), les pannes lisibles, et le chemin « service en ligne ».
using System.Text;
using System.Text.Json.Nodes;
using Moto.Core.AI.Generation;
using Moto.Core.AI.Llm;
using Moto.Core.Tests.AI.V2;
using Xunit;

namespace Moto.Core.Tests.AI.Generation;

public class ChatStreamServiceTests
{
    private const string FileText = "class A\n{\n    int x = 1;\n}\n";

    private static ChatStreamService Service(FakeOllamaHandler fake, string model = "qwen2.5-coder:7b")
        => new(endpoint => new OllamaChatClient(endpoint, fake), () => new GenerationSettings { Endpoint = "http://127.0.0.1:11434", Model = model });

    private static ChatRequest Request(string message = "Que fait ce fichier ?", IReadOnlyList<ChatTurn>? history = null)
        => new() { Message = message, FilePath = "src/A.cs", FileText = FileText, History = history ?? Array.Empty<ChatTurn>() };

    /// <summary>Une réponse en plusieurs morceaux, comme un vrai flux Ollama.</summary>
    private static string Chunks(string doneReason, params string[] parts)
    {
        var sb = new StringBuilder();
        foreach (var part in parts)
            sb.Append(new JsonObject
            {
                ["model"] = "fake",
                ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = part },
                ["done"] = false,
            }.ToJsonString()).Append('\n');

        sb.Append(new JsonObject
        {
            ["model"] = "fake",
            ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = string.Empty },
            ["done"] = true,
            ["done_reason"] = doneReason,
            ["prompt_eval_count"] = 300,
            ["eval_count"] = parts.Length,
            ["total_duration"] = 2_000_000_000L,
            ["eval_duration"] = 1_000_000_000L,
        }.ToJsonString()).Append('\n');
        return sb.ToString();
    }

    [Fact]
    public async Task The_answer_arrives_in_pieces_and_comes_back_whole()
    {
        var fake = new FakeOllamaHandler().Enqueue(Chunks("stop", "Ce fichier ", "déclare ", "la classe A."));
        var pieces = new List<string>();

        var outcome = await Service(fake).StreamAsync(Request(), pieces.Add);

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Equal(new[] { "Ce fichier ", "déclare ", "la classe A." }, pieces);
        Assert.Equal("Ce fichier déclare la classe A.", outcome.Content);
        Assert.Equal("qwen2.5-coder:7b", outcome.Model);
        Assert.False(outcome.Truncated);
        Assert.Equal("Que fait ce fichier ?", outcome.SentUserMessage);
        Assert.Contains("fichier ouvert (A.cs)", outcome.SentContext);
    }

    [Fact]
    public async Task The_request_streams_in_the_16k_window_with_the_file_only_in_the_system_message()
    {
        var fake = new FakeOllamaHandler().Text("Réponse.");

        await Service(fake).StreamAsync(Request(history: new[]
        {
            new ChatTurn("user", "Bonjour"),
            new ChatTurn("ai", "Bonjour !"),
        }));

        var body = Assert.Single(fake.ChatRequests);
        Assert.True((bool?)body["stream"]);
        var options = (JsonObject)body["options"]!;
        Assert.Equal(ChatPrompts.SmallContext, (int?)options["num_ctx"]); // la fenêtre de l'agent : le modèle chargé est réutilisé
        Assert.InRange((int)options["num_predict"]!, 1, ChatPrompts.MaxOutputTokens);

        var messages = (JsonArray)body["messages"]!;
        Assert.Equal(new[] { "system", "user", "assistant", "user" }, messages.Select(m => (string?)m!["role"]));
        Assert.Contains("int x = 1;", (string)messages[0]!["content"]!);
        for (var i = 1; i < messages.Count; i++)
            Assert.DoesNotContain("int x = 1;", (string)messages[i]!["content"]!);
        Assert.Equal("Que fait ce fichier ?", (string?)messages[^1]!["content"]);
    }

    [Fact]
    public async Task A_reply_stopped_by_the_length_limit_is_flagged()
    {
        var fake = new FakeOllamaHandler().Enqueue(Chunks("length", "début de réponse"));

        var outcome = await Service(fake).StreamAsync(Request());

        Assert.True(outcome.Succeeded);
        Assert.True(outcome.Truncated);
    }

    [Fact]
    public async Task Ollama_being_off_is_reported_plainly_as_unavailable()
    {
        var fake = new FakeOllamaHandler { Unreachable = true };

        var outcome = await Service(fake).StreamAsync(Request());

        Assert.False(outcome.Succeeded);
        Assert.True(outcome.LocalUnavailable);
        Assert.Contains("Ollama est injoignable", outcome.Problem);
    }

    [Fact]
    public async Task No_installed_model_is_unavailable_with_a_helpful_message()
    {
        var fake = new FakeOllamaHandler();
        fake.Models.Clear();

        var outcome = await Service(fake).StreamAsync(Request());

        Assert.True(outcome.LocalUnavailable);
        Assert.Contains("ollama pull", outcome.Problem);
        Assert.Empty(fake.ChatRequests);
    }

    [Fact]
    public async Task An_installed_alternative_answers_when_the_configured_model_is_missing_and_the_note_says_so()
    {
        var fake = new FakeOllamaHandler().Text("Réponse.");
        fake.Models.Clear();
        fake.Models.Add("qwen3:8b");

        var outcome = await Service(fake, model: "absent:7b").StreamAsync(Request());

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Equal("qwen3:8b", outcome.Model);
        Assert.Contains("« absent:7b » n'est pas installé", outcome.Note);
    }

    [Fact]
    public async Task A_failure_in_the_middle_keeps_what_was_already_written()
    {
        var first = new JsonObject
        {
            ["model"] = "fake",
            ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = "Ce fichier " },
            ["done"] = false,
        }.ToJsonString();
        var fake = new FakeOllamaHandler().Enqueue(first + "\n{\"error\":\"model runner has unexpectedly stopped\"}\n");

        var outcome = await Service(fake).StreamAsync(Request());

        Assert.False(outcome.Succeeded);
        Assert.False(outcome.LocalUnavailable); // Ollama tourne : pas de bascule vers un service en ligne pour ça
        Assert.Equal("Ce fichier ", outcome.PartialContent);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Problem));
    }

    [Fact]
    public async Task An_empty_question_is_refused_before_calling_the_model()
    {
        var fake = new FakeOllamaHandler();

        var outcome = await Service(fake).StreamAsync(Request(message: "  "));

        Assert.False(outcome.Succeeded);
        Assert.Empty(fake.ChatRequests);
    }

    [Fact]
    public async Task Cancellation_propagates_instead_of_becoming_a_message()
    {
        var fake = new FakeOllamaHandler().Text("Réponse.");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(fake).StreamAsync(Request(), ct: cts.Token));
    }

    // ── Service en ligne ────────────────────────────────────────────────────

    [Fact]
    public async Task Online_the_provider_gets_one_text_without_the_open_file()
    {
        string? sent = null;

        var outcome = await new ChatStreamService().RunOnlineAsync(Request(history: new[]
        {
            new ChatTurn("user", "Bonjour", Typed: "Bonjour"),
            new ChatTurn("ai", "Salut !"),
        }), (prompt, _) =>
        {
            sent = prompt;
            return Task.FromResult(("Voici.", "OpenAI"));
        });

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Equal("Voici.", outcome.Content);
        Assert.Equal("OpenAI", outcome.Model);
        Assert.DoesNotContain("int x = 1;", sent);
        Assert.Contains("MOTO AI : Salut !", sent);
        Assert.EndsWith("Que fait ce fichier ?", sent);
        Assert.Contains(outcome.Notes, n => n.Contains("📎"));
    }

    [Fact]
    public async Task Online_a_failing_provider_gives_a_readable_problem()
    {
        var outcome = await new ChatStreamService().RunOnlineAsync(Request(),
            (_, _) => throw new InvalidOperationException("Tous les providers IA ont échoué."));

        Assert.False(outcome.Succeeded);
        Assert.Contains("Aucun service en ligne", outcome.Problem);
        Assert.Contains("Tous les providers", outcome.Problem);
    }

    [Fact]
    public async Task Online_an_empty_answer_is_a_problem_not_a_blank_bubble()
    {
        var outcome = await new ChatStreamService().RunOnlineAsync(Request(), (_, _) => Task.FromResult(("  ", "Mistral")));

        Assert.False(outcome.Succeeded);
        Assert.Contains("Mistral", outcome.Problem);
    }
}
