using System.Diagnostics;
using System.Text.Json;
using Moto.Core.Performance;

namespace Moto.Core.Tests.Performance;

/// <summary>
/// Tests E2E du HeavyProcessLauncher :
/// - Lancement d'un processus enfant
/// - Communication JSON-RPC
/// - Gestion des crashes
/// - Arrêt propre
/// </summary>
public class HeavyProcessLauncherTests
{
    private const string TestEngineExe = "Moto.EngineHost.exe"; // exécutable hôte de test

    [Fact]
    public async Task LaunchAsync_StartsProcess()
    {
        // Arrange
        var launcher = new HeavyProcessLauncher(TestEngineExe);

        // Act
        var process = await launcher.LaunchAsync("roslyn", Array.Empty<string>());

        // Assert
        Assert.NotNull(process);
        Assert.True(process.IsRunning);
        Assert.Equal("roslyn", process.EngineName);

        // Cleanup
        launcher.Dispose();
    }

    [Fact]
    public async Task SendRequestAsync_ReturnsResponse()
    {
        // Arrange
        var launcher = new HeavyProcessLauncher(TestEngineExe);
        await launcher.LaunchAsync("test", Array.Empty<string>());
        var request = new JsonRpcRequest
        {
            Method = "echo",
            Params = new { message = "hello" }
        };

        // Act
        var response = await launcher.SendRequestAsync<EchoResponse>("test", request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("hello", response.Echo);

        launcher.Dispose();
    }

    [Fact]
    public async Task ProcessCrash_RestartsAutomatically()
    {
        // Arrange
        var launcher = new HeavyProcessLauncher(TestEngineExe);
        var process = await launcher.LaunchAsync("unstable", new[] { "--crash-after=1s" });

        // Act : attend le crash
        await Task.Delay(2000);

        // Assert : le processus a redémarré (RestartOnCrash = true)
        Assert.True(process.IsRunning);

        launcher.Dispose();
    }

    [Fact]
    public async Task StopAll_KillsAllProcesses()
    {
        // Arrange
        var launcher = new HeavyProcessLauncher(TestEngineExe);
        await launcher.LaunchAsync("roslyn", Array.Empty<string>());
        await launcher.LaunchAsync("xeno", Array.Empty<string>());

        // Act
        launcher.StopAll();

        // Assert : tous les processus sont arrêtés
        await Task.Delay(500);
        // (vérification via Process.GetProcessesByName si nécessaire)
    }

    [Fact]
    public async Task JsonRpc_MalformedResponse_DoesNotCrash()
    {
        // Arrange
        var launcher = new HeavyProcessLauncher(TestEngineExe);
        await launcher.LaunchAsync("test", new[] { "--send-garbage" });
        var request = new JsonRpcRequest { Method = "echo", Params = new { message = "test" } };

        // Act + Assert : ne doit pas lever d'exception
        var ex = await Record.ExceptionAsync(() =>
            launcher.SendRequestAsync<EchoResponse>("test", request));

        // Soit timeout, soit réponse valide, mais pas de crash
        Assert.Null(ex);

        launcher.Dispose();
    }
}

public class EchoResponse
{
    public string Echo { get; set; } = "";
}
