// Moto.Core/Analytics/HomeStats.cs
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Moto.Core.Analytics
{
    /// <summary>Une tuile de la rangée de chiffres de l'Accueil.</summary>
    public sealed record HomeStatTile(string Value, string Title);

    /// <summary>
    /// ★ AJOUT (26/09, décision de Tom : « brancher les vrais compteurs, et masquer la rangée tant qu'il n'y a rien à montrer ») : les
    /// chiffres de l'Accueil viennent des compteurs gardés depuis l'installation (global-usage.json, <see cref="GlobalUsageEngine"/> — les mêmes
    /// que le tableau de bord complet) et des motifs appris du projet ouvert (Cortex). Avant : les conversations et messages de la session en
    /// cours, jamais sauvegardés, donc 0 à chaque lancement. Une tuile à zéro n'est pas montrée ; liste vide = rien à montrer (rangée masquée).
    /// </summary>
    public static class HomeStats
    {
        private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

        public static IReadOnlyList<HomeStatTile> Build(UsageStats? usage, int learnedPatterns)
        {
            var tiles = new List<HomeStatTile>();
            if (usage is not null)
            {
                // Temps d'ouverture de l'appli, compté à la fermeture d'une session (pas le temps de frappe).
                if (usage.TotalWorkSeconds >= 60) tiles.Add(new HomeStatTile(FormatDuration(usage.TotalWorkSeconds), "Temps dans MOTO"));
                if (usage.AiCallsTotal > 0) tiles.Add(new HomeStatTile(Compact(usage.AiCallsTotal), "Demandes à l'IA"));
                // Estimation (longueur des réponses / 4), pas un vrai décompte du fournisseur : d'où « ≈ ».
                if (usage.TokensConsumed > 0) tiles.Add(new HomeStatTile("≈ " + Compact(usage.TokensConsumed), "Tokens (estimés)"));
            }
            if (learnedPatterns > 0) tiles.Add(new HomeStatTile(Compact(learnedPatterns), "Motifs appris"));
            return tiles;
        }

        /// <summary>« 15 h 20 », « 12 min ».</summary>
        public static string FormatDuration(long seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ts.TotalHours >= 1 ? $"{(long)ts.TotalHours} h {ts.Minutes:D2}" : $"{ts.Minutes} min";
        }

        /// <summary>« 812 », « 4,1 k », « 2,5 M » (virgule française, quelle que soit la langue de Windows).</summary>
        public static string Compact(long n) =>
            n >= 1_000_000 ? (n / 1_000_000.0).ToString("0.#", French) + " M" :
            n >= 1_000 ? (n / 1_000.0).ToString("0.#", French) + " k" :
            n.ToString(French);
    }
}
