// Moto.Core/Performance/AdaptiveModelSelector.cs
namespace Moto.Core.Performance;

/// <summary>
/// Choisit le modèle IA le plus petit suffisant selon la tâche.
/// </summary>
public sealed class AdaptiveModelSelector
{
    public static AdaptiveModelSelector Instance { get; private set; } = null!;

    public AdaptiveModelSelector()
    {
        Instance = this;
    }

    /// <summary>
    /// Sélectionne le modèle optimal pour une tâche.
    /// </summary>
    public AiModel SelectModel(TaskComplexity complexity)
    {
        return complexity switch
        {
            TaskComplexity.Simple => AiModel.Small, // 0.5B params
            TaskComplexity.Medium => AiModel.Medium, // 3B params
            TaskComplexity.Complex => AiModel.Large, // 7B+ params
            _ => AiModel.Medium
        };
    }
}

public enum TaskComplexity
{
    Simple,
    Medium,
    Complex
}

public enum AiModel
{
    Small,
    Medium,
    Large
}
