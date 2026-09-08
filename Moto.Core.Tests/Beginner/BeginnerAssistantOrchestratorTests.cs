// Moto.Core.Tests/Beginner/BeginnerAssistantOrchestratorTests.cs
// Vérifie EN DIRECT que BeginnerAssistant, une fois branché sur le vrai
// IOrchestratorClient (au lieu de l'ancien IXenoBridge jamais implémenté),
// produit un vrai patch et — surtout — n'écrit jamais le fichier réel tant
// que MOTO Editor (Pages/BeginnerAssistantPage) n'a pas confirmé.
using System;
using System.IO;
using System.Threading.Tasks;
using Moto.Core.Integration;
using Moto.Editor.AI.Beginner;
using Xunit;
using Xunit.Abstractions;

namespace Moto.Core.Tests.Beginner
{
    public class BeginnerAssistantOrchestratorTests
    {
        private readonly ITestOutputHelper _output;

        public BeginnerAssistantOrchestratorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private sealed class UnusedOllamaClient : IOllamaClient
        {
            public Task<string> GenerateAsync(string prompt) =>
                throw new InvalidOperationException("Ne devrait pas être appelé par les actions routées vers l'orchestrateur.");
        }

        private async Task<BeginnerAssistant?> CreateIfReachableAsync()
        {
            var orchestrator = new OrchestratorClient();
            var health = await orchestrator.GetHealthAsync();

            if (!health.Reachable || !health.Ok)
            {
                _output.WriteLine($"[SKIP] Orchestrateur injoignable ({health.Error}). Lancer start_orchestrator_studio.py --os-v5 --port 5001.");
                return null;
            }

            return new BeginnerAssistant(new UnusedOllamaClient(), orchestrator);
        }

        [Fact]
        public async Task FixFile_RealRoundTrip_NeverWritesWithoutConfirmation()
        {
            var assistant = await CreateIfReachableAsync();
            if (assistant == null) return;

            var tempFile = Path.Combine(Path.GetTempPath(), $"beginner-fix-{Guid.NewGuid():N}.cs");
            const string original = "// fichier original — ne doit jamais être modifié par ce test\n";
            await File.WriteAllTextAsync(tempFile, original);

            try
            {
                var result = await assistant.ExecuteAsync(new BeginnerRequest
                {
                    Action = BeginnerAction.FixFile,
                    FilePath = tempFile
                });

                // La garantie de sécurité que Tom a explicitement demandée :
                // BeginnerAssistant peut PROPOSER un patch, il ne l'écrit
                // jamais lui-même — seule Pages/BeginnerAssistantPage le fait,
                // après confirmation explicite.
                var onDisk = await File.ReadAllTextAsync(tempFile);
                Assert.Equal(original, onDisk);

                _output.WriteLine($"Success={result.Success} RequiresConfirmation={result.RequiresUserConfirmation} Patches={result.Patches.Count}");
                _output.WriteLine($"Explication : {result.Explanation}");

                if (result.Success)
                {
                    Assert.True(result.RequiresUserConfirmation);
                    Assert.Single(result.Patches);
                    Assert.Equal(tempFile, result.Patches[0].FilePath);
                    Assert.False(string.IsNullOrWhiteSpace(result.Patches[0].ProposedContent));
                }
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task MakeBetter_RealRoundTrip_EitherPatchOrTextSuggestion_NeverWritesFile()
        {
            var assistant = await CreateIfReachableAsync();
            if (assistant == null) return;

            var tempFile = Path.Combine(Path.GetTempPath(), $"beginner-better-{Guid.NewGuid():N}.cs");
            const string original = "public class Demo { public int Add(int a, int b) { return a + b; } }\n";
            await File.WriteAllTextAsync(tempFile, original);

            try
            {
                var result = await assistant.ExecuteAsync(new BeginnerRequest
                {
                    Action = BeginnerAction.MakeBetter,
                    FilePath = tempFile,
                    Content = original
                });

                var onDisk = await File.ReadAllTextAsync(tempFile);
                Assert.Equal(original, onDisk);

                // Les deux issues sont valides : un patch (via /generate-batch)
                // OU une suggestion texte (repli via /chat, plus de repli Ollama).
                _output.WriteLine($"Success={result.Success} RequiresConfirmation={result.RequiresUserConfirmation}");
                _output.WriteLine($"Explication : {result.Explanation}");
                Assert.False(string.IsNullOrWhiteSpace(result.Explanation));
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
