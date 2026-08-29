// Moto.Core/Platform/PlatformEngine.cs (v3 — CI + incrémental + smart detect)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Moto.Core.Services;

namespace Moto.Core.Platform
{
    public class PlatformEngine
    {
        private readonly PlatformDetector _detector = new();
        private readonly BuildEngine _build = new();
        private readonly System.Timers.Timer _redetectTimer;

        private string _workspace;
        private List<string> _addedTfms = new();

        // ------------------------------------------------------------------
        // Options pilotées par les paramètres (MainPage)
        // ------------------------------------------------------------------

        /// <summary>1. Générer les pipelines CI.</summary>
        public bool GenerateCi { get; set; }

        /// <summary>1. Provider CI (GitHub / GitLab / Azure / All).</summary>
        public CiProvider CiProvider { get; set; } = CiProvider.GitHub;

        /// <summary>3. Validation incrémentale (uniquement TFM ajoutés).</summary>
        public bool IncrementalValidate { get; set; } = true;

        /// <summary>4. Validation automatique après génération.</summary>
        public bool AutoValidate { get; set; } = true;

        /// <summary>4. Détection continue intelligente (filtre patterns).</summary>
        public bool SmartContinuous { get; set; } = true;

        public bool IsBusy { get; private set; }

        public event Action<PlatformReport> DetectionReady;
        public event Action<string, double> Progress;
        public event Action<PlatformProposal, bool> GenerationDone;
        public event Action<bool, string> ValidationDone;

        public PlatformEngine()
        {
            // Debounce 3 s pour la détection continue
            _redetectTimer = new System.Timers.Timer(3000) { AutoReset = false };
            _redetectTimer.Elapsed += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(_workspace))
                {
                    AnalyzeNow(_workspace);
                }
            };
        }

        public void SetWorkspace(string workspace) => _workspace = workspace;

        /// <summary>
        /// 4. Détection continue INTELLIGENTE : branchée sur le watcher du
        /// DocEngine, mais ne re-analyse QUE si le fichier modifié contient
        /// un pattern plateforme (évite les re-analyses inutiles).
        /// </summary>
        public void AttachContinuousDetection(Moto.Core.Doc.DocEngine doc)
        {
            doc.SourceFileChanged += path =>
            {
                if (SmartContinuous && !PlatformDetector.ContainsPlatformSignal(path))
                {
                    return; // Pas de signal plateforme : on ignore.
                }

                _redetectTimer.Stop();
                _redetectTimer.Start();
            };
        }

        public PlatformReport AnalyzeNow(string workspace)
        {
            SetWorkspace(workspace);
            var report = _detector.Analyze(workspace);
            DetectionReady?.Invoke(report);
            return report;
        }

        public async Task ApplyAsync(PlatformProposal proposal, string workspace, string csprojPath)
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                await Task.Run(() =>
                {
                    // Capture les TFM AVANT réécriture (pour validation incrémentale)
                    string oldTfms = "";

                    if (File.Exists(csprojPath))
                    {
                        var cs = File.ReadAllText(csprojPath);
                        var m = Regex.Match(cs, @"<TargetFrameworks?>(.*?)</TargetFrameworks?>",
                            RegexOptions.Singleline);
                        if (m.Success) oldTfms = m.Groups[1].Value;
                    }

                    int total = proposal.Files.Count + 2;
                    int done = 0;

                    // Écriture des fichiers
                    foreach (var file in proposal.Files)
                    {
                        var path = Path.Combine(workspace, file.RelativePath);
                        var dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                        File.WriteAllText(path, file.Content);
                        done++;
                        Progress?.Invoke($"✅ {file.RelativePath}", done / (double)total);
                    }

                    // Réécriture csproj (TargetFrameworks)
                    if (!string.IsNullOrWhiteSpace(proposal.NewTargetFrameworks) && File.Exists(csprojPath))
                    {
                        var content = File.ReadAllText(csprojPath);
                        var newContent = Regex.Replace(content,
                            @"<TargetFrameworks?>(.*?)</TargetFrameworks?>",
                            $"<TargetFrameworks>{proposal.NewTargetFrameworks}</TargetFrameworks>",
                            RegexOptions.Singleline);
                        File.WriteAllText(csprojPath, newContent);
                    }
                    done++;
                    Progress?.Invoke("✅ csproj mis à jour", done / (double)total);

                    // 3. Calcule les TFM ajoutés (validation incrémentale)
                    _addedTfms = (proposal.NewTargetFrameworks ?? "")
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !oldTfms.Contains(t))
                        .ToList();

                    // 1. Pipelines CI multi-providers
                    if (GenerateCi)
                    {
                        var platforms = new List<TargetPlatform> { proposal.Platform };

                        foreach (var tfm in proposal.NewTargetFrameworks?.Split(';') ?? Array.Empty<string>())
                        {
                            var p = TfmToPlatform(tfm.Trim());
                            if (p.HasValue) platforms.Add(p.Value);
                        }

                        CiGenerator.Generate(workspace, CiProvider, platforms);
                    }
                    done++;
                    Progress?.Invoke("✅ pipelines CI générés", 1.0);
                });

                GenerationDone?.Invoke(proposal, true);

                if (AutoValidate)
                {
                    await ValidateAsync(workspace, proposal);
                }
            }
            catch (Exception ex)
            {
                Progress?.Invoke("❌ " + ex.Message, 1.0);
                GenerationDone?.Invoke(proposal, false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 3. Validation : build UNIQUEMENT les TFM ajoutés (incrémental),
        /// ou Moto.Linux pour le portage Linux, sinon build complet.
        /// </summary>
        private async Task ValidateAsync(string workspace, PlatformProposal proposal)
        {
            Progress?.Invoke("🔨 Validation…", 1.0);

            // Incrémental : un build par TFM ajouté
            if (IncrementalValidate && _addedTfms.Count > 0)
            {
                foreach (var tfm in _addedTfms)
                {
                    var r = await _build.BuildAsync(workspace, tfm);

                    if (!r.Success)
                    {
                        ValidationDone?.Invoke(false, $"❌ Build {tfm} : {FirstError(r)}");
                        return;
                    }
                }

                ValidationDone?.Invoke(true,
                    $"✅ Validation incrémentale OK ({string.Join(", ", _addedTfms)}).");
                return;
            }

            // Linux : valide le projet Avalonia Moto.Linux
            var linuxCsproj = Path.Combine(workspace, "Moto.Linux", "Moto.Linux.csproj");

            if (proposal.Platform == TargetPlatform.Linux && File.Exists(linuxCsproj))
            {
                var r = await _build.BuildProjectAsync(linuxCsproj);
                ValidationDone?.Invoke(r.Success,
                    r.Success ? "✅ Moto.Linux compile." : $"❌ Moto.Linux : {FirstError(r)}");
                return;
            }

            // Fallback : build complet
            var full = await _build.BuildAsync(workspace);
            ValidationDone?.Invoke(full.Success,
                full.Success ? "✅ Le projet compile." : $"❌ Build : {FirstError(full)}");
        }

        private static string FirstError(BuildResult r) =>
            r.Diagnostics.Count > 0 ? r.Diagnostics[0].Message : "erreurs de compilation";

        private static TargetPlatform? TfmToPlatform(string tfm)
        {
            var lower = tfm.ToLowerInvariant();

            if (lower.Contains("-android")) return TargetPlatform.Android;
            if (lower.Contains("-ios")) return TargetPlatform.iOS;
            if (lower.Contains("-maccatalyst")) return TargetPlatform.MacOS;
            if (lower.Contains("-windows")) return TargetPlatform.Windows;

            return null;
        }
    }
}
