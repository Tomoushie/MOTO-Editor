// Moto.Editor/Services/GitIgnoreMatcher.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Moto.Editor.Services
{
    /// <summary>
    /// ★ AJOUT (01/10) : lecture du <c>.gitignore</c> du dossier ouvert, pour que le réglage
    /// <c>pp_hide_gitignore</c> (« Cache les entrées gitignorées ») repose sur une VRAIE donnée
    /// et non sur une supposition.
    ///
    /// POURQUOI PAS `git check-ignore` : le dépôt dispose bien d'un <c>GitService</c> (git CLI),
    /// mais l'explorateur se rafraîchit à chaque dépliage de dossier — lancer un processus git
    /// par entrée serait à la fois lent et fragile (git peut ne pas être installé ; cf.
    /// FileExplorerView qui lit déjà `.git/HEAD` à la main plutôt que d'appeler git, même
    /// raison). Ce lecteur travaille donc sur le seul fichier `.gitignore` de la racine.
    ///
    /// PORTÉE ASSUMÉE — ce n'est PAS une réimplémentation complète de la spécification git :
    ///   - un seul `.gitignore` (celui de la racine), pas la hiérarchie de fichiers imbriqués ;
    ///   - les motifs sont appliqués au NOM de l'entrée (pas au chemin relatif complet), donc
    ///     un motif contenant un « / » interne (ex. `build/output`) n'est PAS évalué ;
    ///   - la négation `!motif` est reconnue mais ne peut que ré-inclure une entrée déjà
    ///     masquée par un motif PLUS TÔT dans le fichier (ordre respecté) — pas de ré-inclusion
    ///     d'un dossier parent, qui demanderait le chemin complet.
    /// Ces limites sont volontaires : mieux vaut masquer un peu moins que masquer à tort un
    /// fichier que l'utilisateur cherche. Le cas courant (`.gitignore` simple de type
    /// « bin/ », « *.user », « node_modules/ ») est couvert exactement.
    /// </summary>
    internal sealed class GitIgnoreMatcher
    {
        private readonly List<(string Pattern, bool Negated, bool DirectoryOnly)> _rules = new();

        private GitIgnoreMatcher()
        {
        }

        /// <summary>Charge le .gitignore de <paramref name="rootPath"/> (jamais null : objet vide si absent).</summary>
        internal static GitIgnoreMatcher Load(string rootPath)
        {
            var matcher = new GitIgnoreMatcher();

            try
            {
                if (string.IsNullOrWhiteSpace(rootPath)) return matcher;

                var path = Path.Combine(rootPath, ".gitignore");
                if (!File.Exists(path)) return matcher;

                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();

                    // Lignes vides et commentaires : ignorés (règle explicite de git).
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                    var negated = line.StartsWith("!", StringComparison.Ordinal);
                    if (negated) line = line.Substring(1).Trim();
                    if (line.Length == 0) continue;

                    // Un motif qui commence par « / » n'est ancré qu'à la racine : n'ayant que
                    // le nom, on le ramène au motif relatif équivalent.
                    if (line.StartsWith("/", StringComparison.Ordinal)) line = line.Substring(1);
                    if (line.Length == 0) continue;

                    var directoryOnly = line.EndsWith("/", StringComparison.Ordinal);
                    if (directoryOnly) line = line.TrimEnd('/');
                    if (line.Length == 0) continue;

                    matcher._rules.Add((line, negated, directoryOnly));
                }
            }
            catch
            {
                // .gitignore illisible : aucune règle appliquée, comme s'il n'existait pas.
            }

            return matcher;
        }

        /// <summary>Vrai si l'entrée nommée <paramref name="name"/> est couverte par un motif.</summary>
        internal bool IsIgnored(string name, bool isDirectory)
        {
            if (_rules.Count == 0 || string.IsNullOrEmpty(name)) return false;

            var ignored = false;
            foreach (var (pattern, negated, directoryOnly) in _rules)
            {
                if (directoryOnly && !isDirectory) continue;
                if (!Matches(pattern, name)) continue;

                ignored = !negated; // dernier motif gagnant, comme git
            }

            return ignored;
        }

        /// <summary>
        /// Correspondance d'un motif sur un NOM : « * » (n'importe quoi sauf séparateur),
        /// « ? » (un caractère). Pas de « ** » (qui suppose un chemin, hors de cette portée).
        /// </summary>
        private static bool Matches(string pattern, string name)
        {
            if (pattern.IndexOf('*') < 0 && pattern.IndexOf('?') < 0)
                return string.Equals(pattern, name, StringComparison.OrdinalIgnoreCase);

            // Motif sans joker interne : « *.user », « bin* », « ?ache »
            var p = 0;
            var n = 0;
            var starIdx = -1;
            var match = 0;

            while (n < name.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == name[n]))
                {
                    p++;
                    n++;
                }
                else if (p < pattern.Length && pattern[p] == '*')
                {
                    starIdx = p;
                    match = n;
                    p++;
                }
                else if (starIdx >= 0)
                {
                    p = starIdx + 1;
                    match++;
                    n = match;
                }
                else
                {
                    return false;
                }
            }

            while (p < pattern.Length && pattern[p] == '*') p++;
            return p == pattern.Length;
        }
    }
}
