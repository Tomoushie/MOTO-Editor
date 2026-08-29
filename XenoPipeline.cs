// Snake2000.Engine/AgentIntegrated/Pipeline/XenoPipeline.cs
using Snake2000.Engine.AgentIntegrated.Core;
using Snake2000.Engine.AgentIntegrated.Scanner;
using Snake2000.Engine.AgentIntegrated.Analyzer;
using Snake2000.Engine.AgentIntegrated.Synthesizer;
using Snake2000.Engine.AgentIntegrated.Connector;
using Snake2000.Engine.AgentIntegrated.Validator;

namespace Snake2000.Engine.AgentIntegrated.Pipeline
{
    /// <summary>
    /// XENO-SSS∞ : orchestrateur principal du pipeline AgentIntegrated.
    /// Pipeline : Scanner → Analyzer → Synthesizer → Connector → Validator.
    /// </summary>
    public class XenoPipeline
    {
        private readonly AgentScanner _scanner = new AgentScanner();
        private readonly AgentAnalyzer _analyzer = new AgentAnalyzer();
        private readonly AgentSynthesizer _synthesizer = new AgentSynthesizer();
        private readonly AgentConnector _connector = new AgentConnector();
        private readonly AgentValidator _validator = new AgentValidator();

        /// <summary>
        /// Exécute le pipeline complet.
        /// Cette signature publique doit rester stable pour ne pas casser l'intégration existante.
        /// </summary>
        public void Run(AgentContext context)
        {
            // Étape 1 : Scanner
            // Produire la liste des fichiers et la ProjectMap.
            var scan = _scanner.ScanProject(context);

            // Étape 2 : Analyzer
            // Analyser la ProjectMap et produire un rapport d'architecture.
            var analysis = _analyzer.Analyze(context, scan);

            // Étape 3 : Synthesizer
            // Générer les fichiers manquants ou incomplètement implémentés.
            var synth = _synthesizer.Synthesize(context, analysis);

            // Étape 4 : Connector
            // Préparer l'intégration des fichiers générés dans le projet existant.
            var connect = _connector.Connect(context, synth);

            // Étape 5 : Validator
            // Vérifier la cohérence finale après intégration.
            var validate = _validator.Validate(context, connect);

            // TODO: exploiter validate dans un rapport, une UI ou un logger.
            _ = validate;
        }
    }
}
