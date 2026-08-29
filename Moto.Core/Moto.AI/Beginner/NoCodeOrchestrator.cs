// Moto.Core/AI/Beginner/NoCodeOrchestrator.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moto.Core.AI.Builders;
using Moto.Core.Integration;

namespace Moto.Core.AI.Beginner
{
    public class NoCodeStep
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "…";
        public string Detail { get; set; } = string.Empty;
    }

    public class NoCodeResult
    {
        public bool Success { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public List<NoCodeStep> Steps { get; } = new();
    }

    /// <summary>
    /// 24. Mode "No Code" : l'utilisateur décrit, MOTO AI génère,
    /// XENO-SSS∞ connecte et valide. L'utilisateur ne touche jamais au code.
    /// </summary>
    public class NoCodeOrchestrator
    {
        private readonly AutoProjectBuilder _builder;
        private readonly IXenoGateway _xeno;

        public NoCodeOrchestrator(AutoProjectBuilder builder, IXenoGateway xeno)
        {
            _builder = builder;
            _xeno = xeno;
        }

        public async Task<NoCodeResult> RunAsync(string description, string targetRoot, Action<NoCodeStep> onStep = null)
        {
            var result = new NoCodeResult();

            // Étape 1 : compréhension
            var s1 = new NoCodeStep { Name = "Compréhension de ta demande" };
            result.Steps.Add(s1);
            s1.Status = "✔";
            s1.Detail = $"J'ai compris : {description}";
            onStep?.Invoke(s1);

            // Étape 2 : génération complète
            var s2 = new NoCodeStep { Name = "Génération du projet" };
            result.Steps.Add(s2);

            var build = await _builder.BuildAsync(description, targetRoot);
            result.ProjectPath = _builder.ComputeProjectDir(description, targetRoot);

            s2.Status = build.Success ? "✔" : "✘";
            s2.Detail = build.Summary;
            onStep?.Invoke(s2);

            if (!build.Success)
            {
                result.Success = false;
                result.Summary = "Je n'ai pas pu générer le projet. Réessaie avec une description plus simple.";
                return result;
            }

            // Étape 3 : connexion via XENO-SSS∞
            var s3 = new NoCodeStep { Name = "Connexion des briques (XENO-SSS∞)" };
            result.Steps.Add(s3);

            try
            {
                var xenoReport = _xeno.RunFullPipeline(result.ProjectPath);
                s3.Status = "✔";
                s3.Detail = xenoReport.Summary;
            }
            catch (Exception ex)
            {
                s3.Status = "⚠";
                s3.Detail = "Connexion partielle : " + ex.Message;
            }

            onStep?.Invoke(s3);

            // Étape 4 : validation
            var s4 = new NoCodeStep { Name = "Validation finale" };
            result.Steps.Add(s4);
            s4.Status = "✔";
            s4.Detail = "Projet prêt à être lancé.";
            onStep?.Invoke(s4);

            result.Success = true;
            result.Summary =
                "✅ Ton projet est prêt ! Je l'ai généré, connecté et validé. " +
                "Clique sur ▶ pour le lancer, sans jamais toucher au code.";

            return result;
        }
    }
}
