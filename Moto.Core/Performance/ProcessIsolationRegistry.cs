namespace Moto.Core.Performance;

/// <summary>
/// Registre des moteurs à isoler dans des processus séparés.
/// </summary>
public static class ProcessIsolationRegistry
{
    public static readonly Dictionary<string, ProcessIsolationConfig> Engines = new()
    {
        ["roslyn"] = new()
        {
            EngineName = "roslyn",
            Description = "Roslyn LSP (C# language server)",
            Args = new[] { "--lsp", "--port=0" },
            AutoStart = false, // Démarré à la demande
            RestartOnCrash = true
        },
        ["xeno"] = new()
        {
            EngineName = "xeno",
            Description = "XENO-SSS∞ pipeline (orchestrateur IA)",
            Args = new[] { "--pipeline=v5" },
            AutoStart = false,
            RestartOnCrash = true
        },
        ["crdt"] = new()
        {
            EngineName = "crdt",
            Description = "CRDT Automerge (collaboration temps réel)",
            Args = new[] { "--sync" },
            AutoStart = false,
            RestartOnCrash = false
        },
        ["dap"] = new()
        {
            EngineName = "dap",
            Description = "Debug Adapter Protocol (netcoredbg)",
            Args = new[] { "--adapter" },
            AutoStart = false,
            RestartOnCrash = false
        },
        ["marketplace"] = new()
        {
            EngineName = "marketplace",
            Description = "Marketplace client (HTTP + cache)",
            Args = new[] { "--client" },
            AutoStart = false,
            RestartOnCrash = false
        }
    };
}

public class ProcessIsolationConfig
{
    public string EngineName { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Args { get; set; } = Array.Empty<string>();
    public bool AutoStart { get; set; }
    public bool RestartOnCrash { get; set; }
}
