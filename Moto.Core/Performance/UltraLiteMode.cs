namespace Moto.Core.Performance;

/// <summary>
/// Mode Ultra-Lite : désactive tous les moteurs lourds.
/// L'IDE devient un éditeur de texte ultra-léger (style Notepad++).
/// </summary>
public sealed class UltraLiteMode
{
    public static UltraLiteMode Instance { get; private set; } = null!;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        private set
        {
            _isActive = value;
            ModeChanged?.Invoke(value);
        }
    }

    public event Action<bool>? ModeChanged;

    public UltraLiteMode()
    {
        Instance = this;
    }

    /// <summary>
    /// Active le mode Ultra-Lite. Tous les moteurs lourds sont désactivés.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Désactive le mode Ultra-Lite. Les moteurs sont rechargés à la demande.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Vérifie si une fonctionnalité est autorisée en mode Ultra-Lite.
    /// </summary>
    public bool IsAllowed(UltraLiteFeature feature) => !IsActive || AllowedInUltraLite.Contains(feature);

    /// <summary>
    /// Fonctionnalités toujours disponibles, même en Ultra-Lite.
    /// </summary>
    private static readonly HashSet<UltraLiteFeature> AllowedInUltraLite = new()
    {
        UltraLiteFeature.TextEditor,
        UltraLiteFeature.SyntaxHighlighting,
        UltraLiteFeature.FileExplorer,
        UltraLiteFeature.Terminal,
        UltraLiteFeature.BasicSearch,
        UltraLiteFeature.Tabs,
        UltraLiteFeature.Minimap,
        UltraLiteFeature.BasicThemes
    };
}

public enum UltraLiteFeature
{
    // ✅ Toujours autorisés
    TextEditor,
    SyntaxHighlighting,
    FileExplorer,
    Terminal,
    BasicSearch,
    Tabs,
    Minimap,
    BasicThemes,

    // ❌ Désactivés en Ultra-Lite
    XenoPipeline,
    CortexEngine,
    NeuralMode,
    AIWorkspace,
    CrdtCollab,
    Marketplace,
    Stripe,
    Analytics,
    Debugger,
    RoslynLsp,
    WebView,
    Plugins,
    AutoRefactor,
    CloudSync,
    AdvancedThemes,
    AiTranslation
}
