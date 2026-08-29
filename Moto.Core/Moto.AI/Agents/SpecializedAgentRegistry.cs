using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Agents;

/// <summary>Registre central des agents spécialisés + dispatch par id.</summary>
public sealed class SpecializedAgentRegistry
{
    private readonly Dictionary<string, ISpecializedAgent> _agents;

    public SpecializedAgentRegistry(IEnumerable<ISpecializedAgent> agents)
    {
        _agents = agents.ToDictionary(a => a.Descriptor.Id);
    }

    public IReadOnlyCollection<ISpecializedAgent> All => _agents.Values;

    public ISpecializedAgent? Get(string id) =>
        _agents.TryGetValue(id, out var agent) ? agent : null;

    public async Task<SpecializedAgentResult> DispatchAsync(
        string id, SpecializedAgentRequest request, CancellationToken ct = default)
    {
        var agent = Get(id);
        return agent is null
            ? SpecializedAgentResult.Fail($"Agent inconnu : {id}")
            : await agent.ExecuteAsync(request, ct);
    }
}
