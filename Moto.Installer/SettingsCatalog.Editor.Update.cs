public SettingItem<bool> SilentUpdate { get; } = new("editor.update.silent", true, "Télécharger en arrière-plan, appliquer au redémarrage");
public SettingItem<string> Mirrors { get; } = new("editor.update.mirrors", "", "Mirrors CDN séparés par ; (fallback)");
public SettingItem<bool> DeltaUpdates { get; } = new("editor.update.delta", true, "Préférer les paquets delta");
