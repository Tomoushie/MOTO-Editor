// Moto.Editor/Services/CommandPaletteService.cs — INSERTION ADDITIVE
using Moto.Core.DevOps;

public partial class CommandPaletteService
{
    // ★ CORRECTION 3 : champ ajouté
    private readonly FeatureFlagService _featureFlags;

    // Constructeur étendu (paramètre optionnel pour compatibilité ascendante)
    public CommandPaletteService(/* ...paramètres existants... */,
                                 FeatureFlagService? featureFlags = null)
    {
        // ... affectations existantes ...
        _featureFlags = featureFlags ?? new FeatureFlagService(
            Moto.Core.Settings.SettingsEngine.Shared,
            new Moto.Core.Logging.StructuredLogCollector());
    }

    // ★ Garde EN TÊTE de la méthode d'ouverture/affichage de la palette
    public void ShowPalette()
    {
        // Hook FeatureFlag : feature lourde désactivable à distance
        if (!_featureFlags.IsEnabled("feature.command_palette"))
            return;

        // ... logique existante inchangée ...
    }
}
