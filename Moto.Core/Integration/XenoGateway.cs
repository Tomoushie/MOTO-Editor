// Moto.Core/Integration/XenoGateway.cs
using System.Collections.Generic;
using Moto.Core.AI.Internal.Models;
using Snake2000.Engine.AgentIntegrated.Core;
using Snake2000.Engine.AgentIntegrated.Scanner;
using Snake2000.Engine.AgentIntegrated.Analyzer;
using Snake2000.Engine.AgentIntegrated.Synthesizer;
using Snake2000.Engine.AgentIntegrated.Connector;
using Snake2000.Engine.AgentIntegrated.Validator;

namespace Moto.Core.Integration
{
    /// <summary>
    /// Rapport produit par XENO-SSS∞.
    /// </summary>
    public class XenoReport
    {
        public bool Success { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<string> Details { get; } = new List<string>();
        public List<(string path, string content)> GeneratedFiles { get; } = new List<(string path, string content)>();
    }

    /// <summary>
    /// Contrat pour appeler XENO-SSS∞ depuis MOTO AI.
    /// </summary>
    public interface IXenoGateway
    {
        XenoReport RunFullPipeline(string workspacePath);
    }

    /// <summary>
    /// Implémentation locale qui appelle réellement les agents XENO-SSS∞.
    /// </summary>
    public class LocalXenoGateway : IXenoGateway
    {
        public XenoReport RunFullPipeline(string workspacePath)
        {
            var context = new AgentContext
            {
                RootPath = workspacePath
            };

            var scanner = new AgentScanner();
            var analyzer = new AgentAnalyzer();
            var synthesizer = new AgentSynthesizer();
            var connector = new AgentConnector();
            var validator = new AgentValidator();

            var scan = scanner.ScanProject(context);
            var analysis = analyzer.Analyze(context, scan);
            var synth = synthesizer.Synthesize(context, analysis);
            var connect = connector.Connect(context, synth);
            var validate = validator.Validate(context, connect);

            var report = new XenoReport
            {
                Success = validate.Status != "error",
                Summary = validate.Summary
            };

            report.Details.AddRange(scan.Details);
            report.Details.AddRange(analysis.Details);
            report.Details.AddRange(synth.Details);
            report.Details.AddRange(connect.Details);
            report.Details.AddRange(validate.Details);

            if (synth.Payload.TryGetValue("GeneratedFiles", out var generated))
            {
                if (generated is List<(string path, string content)> files)
                {
                    report.GeneratedFiles.AddRange(files);
                }
            }

            return report;
        }
    }
}
