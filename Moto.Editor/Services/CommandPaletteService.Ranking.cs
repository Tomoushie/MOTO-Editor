using System;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Editor.Services;

/// <summary>
/// A CONNECTER — relie RankCommands() à la palette de commandes.
/// Combine récence (decay 7j), fréquence, contexte et fuzzy sous-séquence.
/// </summary>
public partial class CommandPaletteService
{
    /// <summary>
    /// Classe les commandes selon le score d'usage.
    /// À appeler avant l'affichage de la liste filtrée.
    /// </summary>
    public IReadOnlyList<RankedCommand> RankCommands(IEnumerable<CommandDefinition> commands)
    {
        if (commands == null) return Array.Empty<RankedCommand>();
        var now = DateTime.UtcNow;

        return commands
            .Select(cmd => new RankedCommand
            {
                Command = cmd,
                Score = ComputeScore(cmd, now)
            })
            .OrderByDescending(rc => rc.Score)
            .ToList();
    }

    /// <summary>
    /// Filtrage fuzzy par sous-séquence + classement.
    /// Utilisé par la palette quand l'utilisateur tape une recherche.
    /// </summary>
    public IReadOnlyList<RankedCommand> SearchAndRank(string query, IEnumerable<CommandDefinition> commands)
    {
        if (string.IsNullOrWhiteSpace(query))
            return RankCommands(commands);

        return commands
            .Where(cmd => FuzzyMatch(query, cmd.Title))
            .Select(cmd => new RankedCommand
            {
                Command = cmd,
                Score = ComputeScore(cmd, DateTime.UtcNow) + FuzzyBonus(query, cmd.Title)
            })
            .OrderByDescending(rc => rc.Score)
            .ToList();
    }

    private double ComputeScore(CommandDefinition cmd, DateTime now)
    {
        var usage = History.GetUsage(cmd.Id);
        if (usage is null) return 0;

        // Décroissance exponentielle sur 7 jours
        double ageDays = (now - usage.LastUsedUtc).TotalDays;
        double recency = Math.Exp(-ageDays / 7.0);

        // Fréquence logarithmique (évite domination d'une seule commande)
        double frequency = Math.Log1p(usage.Count);

        // Bonus contexte si pertinent pour le fichier actif
        double contextBonus = usage.IsContextRelevant ? 0.5 : 0.0;

        return recency * 2.0 + frequency + contextBonus;
    }

    /// <summary>Match fuzzy par sous-séquence (ex: "opn" matche "Open File").</summary>
    private static bool FuzzyMatch(string query, string target)
    {
        if (string.IsNullOrEmpty(query)) return true;
        int qi = 0;
        foreach (var c in target)
        {
            if (qi < query.Length && char.ToLowerInvariant(c) == char.ToLowerInvariant(query[qi]))
                qi++;
        }
        return qi == query.Length;
    }

    private static double FuzzyBonus(string query, string target)
    {
        // Bonus si la requête est un préfixe exact
        return target.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
    }
}

public sealed class RankedCommand
{
    public CommandDefinition Command { get; set; } = null!;
    public double Score { get; set; }
}
