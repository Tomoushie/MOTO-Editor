using FluentAssertions;
using Moto.Core.AI.Internal;
using Xunit;

namespace Moto.Tests.E2E;

/// <summary>
/// Tests E2E : circuit breaker déclenché après 3 échecs consécutifs.
/// </summary>
public class CircuitBreakerTests
{
    private readonly AiWatchdogService _watchdog;

    public CircuitBreakerTests()
    {
        _watchdog = new AiWatchdogService();
    }

    [Fact]
    public async Task ThreeConsecutiveFailures_ShouldOpenCircuit()
    {
        // Arrange : simule 3 échecs consécutifs
        await _watchdog.RecordFailureAsync("test-model");
        await _watchdog.RecordFailureAsync("test-model");
        await _watchdog.RecordFailureAsync("test-model");

        // Act
        var state = await _watchdog.GetStateAsync("test-model");

        // Assert : le circuit doit être ouvert
        state.Should().Be(WatchdogState.CircuitOpen);
    }

    [Fact]
    public async Task CircuitOpen_ShouldBlockSubsequentRequests()
    {
        // Arrange : ouvre le circuit
        await _watchdog.RecordFailureAsync("test-model");
        await _watchdog.RecordFailureAsync("test-model");
        await _watchdog.RecordFailureAsync("test-model");

        // Act : tente une nouvelle requête
        var canExecute = await _watchdog.CanExecuteAsync("test-model");

        // Assert : la requête doit être bloquée
        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task CircuitHalfOpen_ShouldAllowSingleTestRequest()
    {
        // Arrange : ouvre le circuit puis force le passage en HalfOpen
        await _watchdog.RecordFailureAsync("test-model");
        await _watchdog.RecordFailureAsync("test-model");
        await _watchdog.RecordFailureAsync("test-model");
        await _watchdog.ForceHalfOpenAsync("test-model");

        // Act : tente une requête de test
        var canExecute = await _watchdog.CanExecuteAsync("test-model");

        // Assert : une seule requête de test doit être autorisée
        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task SuccessfulRequest_ShouldCloseCircuit()
    {
        // Arrange : ouvre le circuit puis passe en HalfOpen
        await _watchdog.RecordFailureAsync("test-model");
        await _watchdog.RecordFailureAsync("test-model");
        await _watchdog.RecordFailureAsync("test-model");
        await _watchdog.ForceHalfOpenAsync("test-model");

        // Act : enregistre un succès
        await _watchdog.RecordSuccessAsync("test-model");
        var state = await _watchdog.GetStateAsync("test-model");

        // Assert : le circuit doit être fermé
        state.Should().Be(WatchdogState.Closed);
    }
}
