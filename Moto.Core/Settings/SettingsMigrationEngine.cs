// Moto.Core/Settings/SettingsMigrationEngine.cs — Remplacer la détection
public MigrationResult MigrateIfNeeded(string settingsPath)
{
    if (!File.Exists(settingsPath))
    {
        _logger.LogInformation("[Migration] Aucun settings.json trouvé, rien à migrer.");
        return MigrationResult.Ok(0, null);
    }

    try
    {
        var json = File.ReadAllText(settingsPath);

        // ── DÉTECTION FINE : Parse le JSON et vérifie Version == 1 ──
        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(json);

            if (parsed.ValueKind == JsonValueKind.Object &&
                parsed.TryGetProperty("Version", out var versionProp) &&
                versionProp.ValueKind == JsonValueKind.Number &&
                versionProp.GetInt32() >= 1)
            {
                _logger.LogInformation("[Migration] Format déjà migré (Version={Version}), skip.", versionProp.GetInt32());
                return MigrationResult.Ok(0, null);
            }

            // Vérifie aussi en minuscule (camelCase)
            if (parsed.ValueKind == JsonValueKind.Object &&
                parsed.TryGetProperty("version", out var versionPropLower) &&
                versionPropLower.ValueKind == JsonValueKind.Number &&
                versionPropLower.GetInt32() >= 1)
            {
                _logger.LogInformation("[Migration] Format déjà migré (version={Version}), skip.", versionPropLower.GetInt32());
                return MigrationResult.Ok(0, null);
            }
        }
        catch (JsonException)
        {
            // JSON invalide : on continue avec la migration
            _logger.LogWarning("[Migration] JSON invalide, tentative de migration.");
        }

        // Backup avant toute modification
        var backupPath = CreateBackup(settingsPath);

        // Parse de l'ancien format (flat key-value)
        var oldSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        if (oldSettings == null || oldSettings.Count == 0)
        {
            _logger.LogWarning("[Migration] Fichier vide ou invalide.");
            return MigrationResult.Fail("Fichier source vide ou invalide.");
        }

        // Conversion vers le nouveau format avec scopes
        var migrated = new MigratedSettings
        {
            Version = 1,
            MigratedUtc = DateTime.UtcNow,
            Global = oldSettings,
            Project = new Dictionary<string, Dictionary<string, object>>()
        };

        // Écriture du nouveau format
        var newJson = JsonSerializer.Serialize(migrated, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(settingsPath, newJson);

        _logger.LogInformation(
            "[Migration] Migré {Count} clés. Backup : {Backup}",
            oldSettings.Count, backupPath);

        return MigrationResult.Ok(oldSettings.Count, backupPath);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[Migration] Échec de la migration.");
        return MigrationResult.Fail($"Erreur : {ex.Message}");
    }
}
