// Moto.Core.Tests/Integration/OrchestratorClientTests.cs
// Tests EN DIRECT contre l'orchestrateur réel (start_orchestrator_studio.py
// --os-v5 --port 5001). xUnit 2.7 n'a pas de "skip dynamique" natif : si le
// process n'est pas lancé, chaque test le détecte via GetHealthAsync() et
// s'arrête proprement (message clair + retour) plutôt que d'échouer en rouge —
// pour ne pas casser la suite le jour où quelqu'un lance `dotnet test` sans
// l'orchestrateur démarré.
using System;
using System.IO;
using System.Threading.Tasks;
using Moto.Core.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Moto.Core.Tests.Integration
{
    public class OrchestratorClientTests
    {
        private readonly ITestOutputHelper _output;

        public OrchestratorClientTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private async Task<OrchestratorClient?> CreateIfReachableAsync()
        {
            var client = new OrchestratorClient();
            var health = await client.GetHealthAsync();

            if (!health.Reachable || !health.Ok)
            {
                _output.WriteLine(
                    $"[SKIP] Orchestrateur injoignable sur {client.Endpoint} " +
                    $"(reachable={health.Reachable}, ok={health.Ok}, error={health.Error}). " +
                    "Lancer 'python start_orchestrator_studio.py --os-v5 --port 5001' pour exécuter ce test pour de vrai.");
                return null;
            }

            return client;
        }

        [Fact]
        public async Task GetHealthAsync_WhenOrchestratorIsRunning_ReturnsOkAndRoutes()
        {
            var client = new OrchestratorClient();
            var health = await client.GetHealthAsync();

            if (!health.Reachable)
            {
                _output.WriteLine($"[SKIP] Orchestrateur injoignable sur {client.Endpoint} : {health.Error}");
                return;
            }

            Assert.True(health.Ok, $"Orchestrateur joignable mais status != ok (error={health.Error}).");
            _output.WriteLine($"Routes annoncées : {string.Join(", ", health.Routes)}");
            Assert.Contains("/chat", health.Routes);
        }

        [Fact]
        public async Task ChatAsync_RealRoundTrip_ReturnsRealAnswer()
        {
            var client = await CreateIfReachableAsync();
            if (client == null) return;

            var result = await client.ChatAsync(
                "raisonnement",
                "Reponds uniquement par le mot OK.");

            Assert.True(result.Success, $"Échec chat : {result.Error}");
            Assert.False(string.IsNullOrWhiteSpace(result.Texte));
            _output.WriteLine($"Réponse réelle ({result.ModeleReel}) : {result.Texte}");
        }

        [Fact]
        public async Task GenerateWithoutWritingAsync_NeverWritesTheRealFile()
        {
            var client = await CreateIfReachableAsync();
            if (client == null) return;

            var tempFile = Path.Combine(Path.GetTempPath(), $"moto-xeno-test-{Guid.NewGuid():N}.cs");
            const string originalContent = "// fichier original — ne doit jamais être modifié par ce test\n";
            await File.WriteAllTextAsync(tempFile, originalContent);

            try
            {
                var result = await client.GenerateWithoutWritingAsync(new OrchestratorGenerateRequest
                {
                    FilePath = tempFile,
                    Instruction = "Ajoute un commentaire XML de résumé au-dessus de ce fichier."
                });

                // La garantie de sécurité, vérifiée pour de vrai : quel que soit le
                // verdict de l'orchestrateur (valide ou non), le VRAI fichier sur
                // disque ne doit jamais avoir bougé — seul un brouillon externe
                // (BROUILLONS_DIR côté orchestrateur) peut recevoir le résultat.
                var actualContent = await File.ReadAllTextAsync(tempFile);
                Assert.Equal(originalContent, actualContent);

                _output.WriteLine($"Success={result.Success} Valide={result.Valide} Raison={result.Raison}");
                if (result.Valide)
                {
                    Assert.False(string.IsNullOrWhiteSpace(result.ProposedContent));
                    _output.WriteLine($"Contenu proposé (brouillon, {result.DraftPath}) : {result.ProposedContent?.Length} caractères.");
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
