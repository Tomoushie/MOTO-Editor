// Téléchargement silencieux en arrière-plan (staging), sans bloquer l'utilisateur
public async Task DownloadInBackgroundAsync(UpdateInfo info)
{
    var urls = BuildUrls(info.DownloadUrl);
    string staging = Path.Combine(Path.GetTempPath(), "moto-update", "payload.zip");
    await ResumableDownloader.DownloadAsync(urls, staging);
    File.WriteAllText(Path.Combine(Path.GetTempPath(), "moto-update", "update-pending.json"),
        System.Text.Json.JsonSerializer.Serialize(info));
}

// Au démarrage : si une MàJ est en attente → appliquer puis relancer
public async Task ApplyPendingIfAnyAsync()
{
    string pending = Path.Combine(Path.GetTempPath(), "moto-update", "update-pending.json");
    if (!File.Exists(pending)) return;
    var info = System.Text.Json.JsonSerializer.Deserialize<UpdateInfo>(File.ReadAllText(pending))!;
    await ApplyAsync(info); // délègue à l'installateur (atomique + rollback)
}

// Mirrors : URL principale + mirrors configurés
private string[] BuildUrls(string primary)
{
    var mirrors = _settings.Shared.Editor.Update.Mirrors.Value
        .Split(';', StringSplitOptions.RemoveEmptyEntries);
    return new[] { primary }.Concat(mirrors).ToArray();
}
