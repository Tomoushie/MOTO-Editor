using System.Collections.Generic;

namespace Moto.Shared;

public sealed class UpdateFileEntry
{
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
}

/// <summary>Manifeste de version : liste de fichiers + hash + signature.</summary>
public sealed class UpdateManifest
{
    public string Version { get; set; } = "";
    public string PayloadSha256 { get; set; } = "";
    public List<UpdateFileEntry> Files { get; set; } = new();
    public string? DeltaFrom { get; set; }      // version source si paquet delta
    public string Signature { get; set; } = ""; // Ed25519 (hex) du hash
}
