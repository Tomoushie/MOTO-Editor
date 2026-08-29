// Ajouter dans Moto.Marketplace.Api/Controllers/PluginsController.cs
[HttpPost("upload")]
public async Task<IActionResult> UploadPlugin(IFormFile file, [FromForm] string authorEmail)
{
    if (file == null || file.Length == 0)
        return BadRequest(new { error = "Fichier requis" });

    var scanner = HttpContext.RequestServices.GetRequiredService<PluginMalwareScanner>();

    // Sauvegarder temporairement
    var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
    using (var stream = new FileStream(tempPath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    // Scanner
    var scanResult = await scanner.ScanPluginArchiveAsync(tempPath);

    // Nettoyer le fichier temporaire
    File.Delete(tempPath);

    if (!scanResult.IsClean)
    {
        return BadRequest(new
        {
            error = "Plugin rejeté : menace détectée",
            threats = scanResult.Threats,
            riskScore = scanResult.RiskScore
        });
    }

    // Procéder à l'installation normale
    return Ok(new { message = "Plugin uploadé et validé", hash = scanResult.HashSha256 });
}
