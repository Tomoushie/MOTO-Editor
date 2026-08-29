// Moto.Core/AI/Beginner/PairProgrammingEngine.cs
using System;
using System.Text.RegularExpressions;

namespace Moto.Core.AI.Beginner
{
    /// <summary>
    /// 25. Mode "AI Pair Programming" : copilot local.
    /// Propose du code "ghost" pendant que l'utilisateur écrit (Tab pour accepter).
    /// </summary>
    public class PairProgrammingEngine
    {
        /// <summary>Propose une suite au code, selon la ligne courante.</summary>
        public string Suggest(string code, int caretLine)
        {
            var lines = (code ?? "").Split('\n');

            if (caretLine < 1 || caretLine > lines.Length) return null;

            var line = lines[caretLine - 1].TrimEnd();
            var t = line.Trim();

            if (t.Length < 3) return null;

            // Classe sans corps → squelette complet
            var classMatch = Regex.Match(t, @"^(public\s+|internal\s+)?class\s+(\w+)\s*$");
            if (classMatch.Success)
            {
                var name = classMatch.Groups[2].Value;
                return $"{{\n    public {name}() {{ }}\n\n    // TODO : ajoute tes méthodes ici\n}}";
            }

            // Système ECS → squelette Update
            var systemMatch = Regex.Match(t, @"class\s+(\w*System)\b");
            if (systemMatch.Success)
            {
                return @"{
    public void Initialize() { }

    public void Update(float deltaTime)
    {
        // TODO : logique du système
    }
}";
            }

            // Méthode sans corps
            var methodMatch = Regex.Match(t, @"^(public|private|internal|protected).*\w+\s*\([^)]*\)\s*$");
            if (methodMatch.Success)
            {
                return "{\n    // TODO : implémente ici\n}";
            }

            // if sans bloc
            if (Regex.IsMatch(t, @"\bif\s*\([^)]*\)\s*$"))
            {
                return "{\n    \n}";
            }

            // boucle sans bloc
            if (Regex.IsMatch(t, @"\b(for|foreach|while)\b.*\)\s*$"))
            {
                return "{\n    \n}";
            }

            return null;
        }
    }
}
