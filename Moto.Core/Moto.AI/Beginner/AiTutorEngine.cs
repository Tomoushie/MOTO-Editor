// Moto.Core/AI/Beginner/AiTutorEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Core.AI.Beginner
{
    public enum TutorKind { Question, Exercise, Explanation, Praise, Correction }

    public class TutorMessage
    {
        public TutorKind Kind { get; set; }
        public string Text { get; set; } = string.Empty;
        public string ExpectedAnswer { get; set; } = string.Empty;
    }

    /// <summary>
    /// 23. Mode "AI Tutor" : pose des questions, propose des exercices,
    /// explique les concepts, corrige les erreurs, félicite l'utilisateur.
    /// </summary>
    public class AiTutorEngine
    {
        private readonly Random _rnd = new();

        public int Score { get; private set; }
        public int Streak { get; private set; }

        private static readonly Dictionary<string, (string Question, string Answer, string Hint)> Bank = new()
        {
            ["classe"] = ("Qu'est-ce qu'une classe ?", "un plan pour créer des objets", "C'est comme un moule à gâteau."),
            ["interface"] = ("Qu'est-ce qu'une interface ?", "un contrat", "Elle dit quelles actions une classe doit fournir."),
            ["méthode"] = ("Qu'est-ce qu'une méthode ?", "une action", "C'est un verbe du programme."),
            ["variable"] = ("Qu'est-ce qu'une variable ?", "une boîte qui range une valeur", "Elle a un nom et un contenu."),
            ["boucle"] = ("Qu'est-ce qu'une boucle ?", "répéter un bloc", "for, foreach, while."),
            ["namespace"] = ("Qu'est-ce qu'un namespace ?", "une boîte de rangement", "Il évite les conflits de noms."),
            ["système"] = ("Qu'est-ce qu'un système dans un moteur de jeu ?", "un module qui fait une chose précise", "Santé, mouvement, combat..."),
        };

        private static readonly string[] Praises =
        {
            "🎉 Excellent ! Continue comme ça.",
            "💪 Bravo, tu progresses vite !",
            "🌟 Parfait, c'est exactement ça !",
            "👏 Très bien joué !"
        };

        private static readonly string[] Corrections =
        {
            "Presque ! Indice : {0}",
            "Pas tout à fait. Rappel : {0}",
        };

        /// <summary>Prochaine interaction : question ou exercice aléatoire.</summary>
        public TutorMessage Next()
        {
            var keys = Bank.Keys.ToList();
            var key = keys[_rnd.Next(keys.Count)];
            var entry = Bank[key];

            if (_rnd.Next(3) == 0)
            {
                return new TutorMessage
                {
                    Kind = TutorKind.Exercise,
                    Text = $"Exercice : essaie d'ajouter une petite méthode qui affiche « Bonjour » dans un fichier, puis dis-moi quand c'est fait.",
                    ExpectedAnswer = "fait"
                };
            }

            return new TutorMessage
            {
                Kind = TutorKind.Question,
                Text = entry.Question,
                ExpectedAnswer = entry.Answer
            };
        }

        /// <summary>Évalue la réponse : félicite ou corrige.</summary>
        public TutorMessage Evaluate(string userAnswer, TutorMessage current)
        {
            var expected = current.ExpectedAnswer ?? "";
            var ok = !string.IsNullOrWhiteSpace(userAnswer) &&
                     (expected.Split(' ').Any(w => w.Length > 3 && userAnswer.Contains(w, StringComparison.OrdinalIgnoreCase))
                      || userAnswer.Contains("fait", StringComparison.OrdinalIgnoreCase));

            if (ok)
            {
                Score += 10;
                Streak++;
                return new TutorMessage { Kind = TutorKind.Praise, Text = $"{Praises[_rnd.Next(Praises.Length)]} (+10 points, série : {Streak})" };
            }

            Streak = 0;
            var hint = Bank.Values.FirstOrDefault(v => v.Answer == expected).Hint;
            return new TutorMessage { Kind = TutorKind.Correction, Text = string.Format(Corrections[_rnd.Next(Corrections.Length)], hint) };
        }

        /// <summary>Explique un concept à la demande.</summary>
        public TutorMessage ExplainConcept(string concept)
        {
            var key = Bank.Keys.FirstOrDefault(k => concept.Contains(k, StringComparison.OrdinalIgnoreCase));

            if (key == null)
            {
                return new TutorMessage { Kind = TutorKind.Explanation, Text = "Je peux t'expliquer : classe, interface, méthode, variable, boucle, namespace, système." };
            }

            var entry = Bank[key];
            return new TutorMessage { Kind = TutorKind.Explanation, Text = $"{entry.Question.Replace("Qu'est-ce que ", "").Replace(" ?", "")} = {entry.Answer}. {entry.Hint}" };
        }
    }
}
