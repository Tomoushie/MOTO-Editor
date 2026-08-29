// Moto.Core/Services/BuildEngine.cs (v2 — build par framework)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Moto.Core.Services
{
    public class BuildDiagnostic
    {
        public string Severity { get; set; } = "error";
        public string File { get; set; } = string.Empty;
        public int Line { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class BuildResult
    {
        public bool Success { get; set; }
        public List<string> Output { get; } = new List<string>();
        public List<BuildDiagnostic> Diagnostics { get; } = new List<BuildDiagnostic>();
    }

    /// <summary>
    /// Compile via dotnet CLI.
    /// v2 : support du build ciblé par framework (-f net8.0-android)
    /// pour la validation incrémentale.
    /// </summary>
    public class BuildEngine
    {
        public event Action<string, bool> OutputReceived;

        private static readonly Regex DiagRegex = new Regex(
            @"^(?<file>.+)\((?<line>\d+),(?<col>\d+)\):\s+(?<sev>error|warning)\s+(?<code>\w+):\s+(?<msg>.+)$",
            RegexOptions.Compiled);

        /// <summary>Build du .sln/.csproj trouvé dans projectPath.</summary>
        public Task<BuildResult> BuildAsync(string projectPath, string framework = null)
        {
            var target = FindBuildTarget(projectPath);

            if (target == null)
            {
                var empty = new BuildResult { Success = false };
                empty.Output.Add("Aucun .csproj ou .sln trouvé.");
                return Task.FromResult(empty);
            }

            return RunBuildAsync(target, projectPath, framework);
        }

        /// <summary>Build d'un csproj explicite (ex : Moto.Linux).</summary>
        public Task<BuildResult> BuildProjectAsync(string csprojPath, string framework = null)
        {
            if (!File.Exists(csprojPath))
            {
                var empty = new BuildResult { Success = false };
                empty.Output.Add($"csproj introuvable : {csprojPath}");
                return Task.FromResult(empty);
            }

            return RunBuildAsync(csprojPath, Path.GetDirectoryName(csprojPath), framework);
        }

        private async Task<BuildResult> RunBuildAsync(string target, string workDir, string framework)
        {
            var result = new BuildResult();

            // -f <tfm> : validation incrémentale (un seul TFM compilé)
            var args = $"build \"{target}\" -nologo -v q" +
                       (string.IsNullOrWhiteSpace(framework) ? "" : $" -f {framework}");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    WorkingDirectory = workDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        result.Output.Add(e.Data);
                        ParseLine(e.Data, result);
                        OutputReceived?.Invoke(e.Data, false);
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        result.Output.Add(e.Data);
                        ParseLine(e.Data, result);
                        OutputReceived?.Invoke(e.Data, true);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                result.Success = process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Output.Add($"Erreur build : {ex.Message}");
            }

            return result;
        }

        private void ParseLine(string line, BuildResult result)
        {
            var match = DiagRegex.Match(line);

            if (match.Success)
            {
                result.Diagnostics.Add(new BuildDiagnostic
                {
                    File = match.Groups["file"].Value,
                    Line = int.TryParse(match.Groups["line"].Value, out var l) ? l : 0,
                    Severity = match.Groups["sev"].Value,
                    Code = match.Groups["code"].Value,
                    Message = match.Groups["msg"].Value
                });
            }
        }

        private string FindBuildTarget(string path)
        {
            var sln = Directory.GetFiles(path, "*.sln").FirstOrDefault();
            if (sln != null) return sln;

            return Directory.GetFiles(path, "*.csproj").FirstOrDefault();
        }
    }
}
