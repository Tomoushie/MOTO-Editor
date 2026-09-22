// Moto.Editor/Models/Claude/ClaudeModels.cs
// Conversion HTML v4 → modèles MAUI. Additif, aucun type existant touché.
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Moto.Editor.Models.Claude;

/// <summary>Vue principale du shell type Claude Code.</summary>
public enum ClaudeViewKind { Chat, Cowork, Customize, Artifacts, Scheduled, Projects }

/// <summary>Onglet actif de la barre latérale.</summary>
public enum ClaudeTabKind { Code, Cowork }

/// <summary>Type de message de conversation (miroir des classes CSS v4).</summary>
public enum ClaudeMsgKind { User, UserCmd, Ai, AiBox, Act, List, Sys }

/// <summary>Base INPC locale (pas de dépendance MVVM externe).</summary>
public abstract class ClaudeNotifyBase : INotifyPropertyChanged
{
    /// <summary>Déclenche PropertyChanged.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Notifie un changement de propriété.</summary>
    protected void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Message de conversation (user / IA / activité / système).</summary>
public sealed class ClaudeChatMessage : ClaudeNotifyBase
{
    private bool _showDetails;

    /// <summary>Type de bulle.</summary>
    public ClaudeMsgKind Kind { get; init; }

    /// <summary>Texte principal.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Lignes additionnelles (AiBox / List).</summary>
    public List<string> Lines { get; init; } = new();

    /// <summary>Lignes ajoutées (diff vert).</summary>
    public int Add { get; init; }

    /// <summary>Lignes supprimées (diff rouge).</summary>
    public int Del { get; init; }

    /// <summary>Détails de l'activité dépliés.</summary>
    public bool ShowDetails
    {
        get => _showDetails;
        set { _showDetails = value; Notify(); }
    }

    /// <summary>Bascule l'affichage des détails (chevron).</summary>
    public void ToggleDetails() => ShowDetails = !ShowDetails;
}

/// <summary>Session de la barre latérale.</summary>
public sealed class ClaudeSession : ClaudeNotifyBase
{
    private bool _isSelected;

    /// <summary>Numéro de raccourci (1..6).</summary>
    public int Number { get; init; }

    /// <summary>Titre de la session.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Session sélectionnée.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; Notify(); }
    }
}

/// <summary>Projet Cowork.</summary>
public sealed class ClaudeProject : ClaudeNotifyBase
{
    private bool _isSelected;

    /// <summary>Nom du projet.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Projet sélectionné.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; Notify(); }
    }
}

/// <summary>Compétence (Personnaliser).</summary>
public sealed class ClaudeSkill
{
    /// <summary>Nom de la compétence.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Auteur.</summary>
    public string Author { get; init; } = "Anthropic";

    /// <summary>Description courte.</summary>
    public string Desc { get; init; } = string.Empty;

    /// <summary>Date relative.</summary>
    public string When { get; init; } = "hier";
}

/// <summary>Artéfact (carte avec aperçu).</summary>
public sealed class ClaudeArtifact
{
    /// <summary>Titre.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>"doc" ou "chart".</summary>
    public string Kind { get; init; } = "doc";

    /// <summary>Date relative de modification.</summary>
    public string When { get; init; } = string.Empty;

    /// <summary>Épinglé.</summary>
    public bool IsPinned { get; init; }

    /// <summary>Partagé.</summary>
    public bool IsShared { get; init; }
}

/// <summary>Ligne d'agent du workflow (Tâches en arrière-plan).</summary>
public sealed class ClaudeAgentRow : ClaudeNotifyBase
{
    private bool _showDetails;

    /// <summary>Nom de l'agent.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Modèle utilisé.</summary>
    public string Model { get; init; } = "Sonnet 5";

    /// <summary>Tokens consommés.</summary>
    public string Tokens { get; init; } = string.Empty;

    /// <summary>Durée.</summary>
    public string Time { get; init; } = string.Empty;

    /// <summary>Ligne mise en avant (gras).</summary>
    public bool IsHot { get; init; }

    /// <summary>Détails dépliés.</summary>
    public bool ShowDetails
    {
        get => _showDetails;
        set { _showDetails = value; Notify(); }
    }
}

/// <summary>Section de la barre latérale.</summary>
public sealed class ClaudeSidebarSection
{
    /// <summary>En-tête de section.</summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>Éléments (titres).</summary>
    public List<string> Items { get; init; } = new();
}
