// Moto.SignTool/Program.cs
using System.Text;
using System.Text.Json;
using Moto.Shared;

namespace Moto.SignTool;

/// <summary>
/// Outil de signature Ed25519 (fait maison) : génération de clés + signature de manifeste.
/// Usage :
///   dotnet run --project Moto.SignTool -- --gen-keys --out keys
///   dotnet run --project Moto.SignTool -- --sign-manifest dist/manifest.json --key keys/update.priv
///   dotnet run --project Moto.SignTool -- --emit-buildkeys --pub keys/update.pub --out ..\Shared\BuildKeys.cs
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--gen-keys"))
                return GenKeys(Get(args, "--out") ?? "keys");

            if (args.Contains("--sign-manifest"))
                return SignManifest(Get(args, "--manifest")!, Get(args, "--key")!);

            if (args.Contains("--emit-buildkeys"))
                return EmitBuildKeys(Get(args, "--pub")!, Get(args, "--out") ?? "BuildKeys.cs");

            Usage();
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ {ex.Message}");
            return 1;
        }
    }

    // ── 1. Génération de la paire de clés ──
    private static int GenKeys(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var (pub, priv) = Ed25519.GenerateKey();

        File.WriteAllText(Path.Combine(outDir, "update.priv"), Sha256Helper.ToHex(priv));
        File.WriteAllText(Path.Combine(outDir, "update.pub"),  Sha256Helper.ToHex(pub));
        EmitBuildKeys(Sha256Helper.ToHex(pub), Path.Combine(outDir, "BuildKeys.cs"));

        Console.WriteLine("✅ Clés générées : update.priv / update.pub.");
        Console.WriteLine("⚠ Gardez update.priv SECRET (ne jamais committer).");
        return 0;
    }

    // ── 2. Signature du manifeste (Ed25519 sur le hash SHA256 du payload) ──
    private static int SignManifest(string manifestPath, string keyPath)
    {
        var priv = Sha256Helper.FromHex(File.ReadAllText(keyPath).Trim());
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(File.ReadAllText(manifestPath))
                       ?? throw new InvalidDataException("Manifeste invalide.");

        var msg = Encoding.UTF8.GetBytes(manifest.PayloadSha256);
        var sig = Ed25519.Sign(msg, priv);
        manifest.Signature = Sha256Helper.ToHex(sig);

        File.WriteAllText(manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"✅ Manifeste signé : {manifestPath}");
        return 0;
    }

    // ── 3. Régénération de BuildKeys.cs (clé publique embarquée) ──
    private static int EmitBuildKeys(string pubHex, string outPath)
    {
        var code =
            "// Auto-généré par Moto.SignTool — ne pas éditer manuellement.\n" +
            "namespace Moto.Shared;\n\n" +
            "public static class BuildKeys\n{\n" +
            $"    public const string UpdatePublicKeyHex = \"{pubHex}\";\n}\n";

        File.WriteAllText(outPath, code);
        Console.WriteLine($"✅ {outPath} régénéré avec la clé publique.");
        return 0;
    }

    private static void Usage()
    {
        Console.WriteLine("Moto.SignTool — chaîne de confiance MOTO");
        Console.WriteLine("  --gen-keys --out <dir>                 génère update.priv/update.pub + BuildKeys.cs");
        Console.WriteLine("  --sign-manifest <manifest> --key <priv> signe le manifeste");
        Console.WriteLine("  --emit-buildkeys --pub <pub> --out <cs> régénère BuildKeys.cs");
    }

    private static string? Get(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
