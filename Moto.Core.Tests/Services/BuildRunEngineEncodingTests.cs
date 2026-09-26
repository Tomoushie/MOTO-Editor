// Moto.Core.Tests/Services/BuildRunEngineEncodingTests.cs
using System.Collections.Concurrent;
using Moto.Core.Services;
using Xunit;

namespace Moto.Core.Tests.Services;

/// <summary>
/// ★ AJOUT (26/09, option D choisie par Tom) : panneaux Build et Exécuter. dotnet (MSBuild) et le
/// programme lancé écrivent en UTF-8 ; l'ancien décodage abîmait accents et chemins (« DÃ©mo_Ã©tÃ© »).
/// Chaque test compile un vrai petit projet rangé dans un dossier accentué (quelques secondes).
/// </summary>
public class BuildRunEngineEncodingTests
{
    private static string CreateProject(string programSource)
    {
        var root = Directory.CreateTempSubdirectory("moto-build-").FullName;
        // Même SDK que la suite de tests : « dotnet test » transmet ses chemins MSBuild (SDK du
        // global.json de MOTO) aux programmes qu'elle lance ; sans le même global.json, le petit
        // projet serait compilé par un autre SDK mêlé à ces chemins (MissingMethodException dans
        // CreateAppHost, vu le 26/09). Sans rapport avec MOTO lancé normalement.
        var globalJson = FindUp(AppContext.BaseDirectory, "global.json");
        if (globalJson != null)
            File.Copy(globalJson, Path.Combine(root, "global.json"));

        var project = Path.Combine(root, "Démo_été");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "Démo_été.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType>"
            + "<TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(project, "Program.cs"), programSource);
        return project;
    }

    private static string? FindUp(string start, string fileName)
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static void DeleteProject(string project)
    {
        try { Directory.Delete(Path.GetDirectoryName(project)!, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* sans conséquence */ }
    }

    /// <summary>
    /// Le chemin accentué doit rester intact dans la liste des avertissements (sinon impossible d'y
    /// sauter), et BuildAsync doit avoir lu toute la sortie avant de conclure (avertissement compté).
    /// </summary>
    [Fact]
    public async Task BuildEngine_RealDotnetBuild_KeepsAccentedPathInDiagnostics()
    {
        if (!OperatingSystem.IsWindows()) return;

        var project = CreateProject("int inutilisee;\nSystem.Console.WriteLine(\"ok\");\n");
        try
        {
            var result = await new BuildEngine().BuildAsync(project);

            Assert.True(result.Success, string.Join("\n", result.Output));
            var file = Assert.Single(result.Diagnostics.Where(d => d.Code == "CS0168").Select(d => d.File).Distinct());
            Assert.EndsWith(Path.Combine("Démo_été", "Program.cs"), file);
            Assert.All(result.Output, line => Assert.DoesNotContain("Ã", line));
        }
        finally
        {
            DeleteProject(project);
        }
    }

    [Fact]
    public async Task RunEngine_RealDotnetRun_ShowsProgramAccents()
    {
        if (!OperatingSystem.IsWindows()) return;

        var project = CreateProject("System.Console.WriteLine(\"Programme: é à ç\");\n");
        var lines = new ConcurrentQueue<string>();
        var run = new RunEngine();
        run.OutputReceived += line => lines.Enqueue(line);
        try
        {
            run.Run(project);

            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline && !lines.Contains("Programme: é à ç"))
                await Task.Delay(100);
        }
        finally
        {
            run.Stop();
            DeleteProject(project);
        }

        Assert.Contains("Programme: é à ç", lines);
    }
}
