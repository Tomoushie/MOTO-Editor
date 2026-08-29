using System;
using System.Collections.Generic;
using System.Linq;
using Moto.Core.AI.Agents;

namespace Moto.Editor.Services;

public partial class CommandPaletteService
{
    private readonly LocalRlFeedbackLoop _rlLoop;

    // Constructeur étendu (à ajouter au constructeur existant)
    public CommandPaletteService(/* ...params existants... */, LocalRlFeedbackLoop rlLoop)
    {
        // ... initialisations existantes ...
        _rlLoop = rlLoop;
    }

    // Modification de RankCommands pour intégrer le boost RL
    public IReadOnlyList<RankedCommand> RankCommandsWithRl(IEnumerable<CommandDefinition> commands)
    {
        var now = DateTime.UtcNow;
        return commands
            .Select(cmd => new RankedCommand
            {
                Command = cmd,
                Score = ComputeScore(cmd, now) + _rlLoop.GetRankingBoost(cmd.Id)
            })
            .OrderByDescending(rc => rc.Score)
            .ToList();
    }
}
