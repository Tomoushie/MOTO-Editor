// Moto.Editor/ViewModels/ClaudeShellViewModel.cs
// ViewModel du shell type Claude Code. Données d'exemple = captures v4.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Moto.Editor.Models.Claude;

namespace Moto.Editor.ViewModels;

/// <summary>ViewModel du ClaudeShellView (navigation + données).</summary>
public sealed class ClaudeShellViewModel : INotifyPropertyChanged
{
    private ClaudeViewKind _currentView = ClaudeViewKind.Chat;
    private ClaudeTabKind _activeTab = ClaudeTabKind.Code;
    private string _inputText = string.Empty;
    private string _currentProject = "WebSite";
    private bool _busy;

    /// <summary>Événement de changement de propriété.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Vue affichée dans la zone principale.</summary>
    public ClaudeViewKind CurrentView
    {
        get => _currentView;
        set { _currentView = value; Notify(); }
    }

    /// <summary>Onglet latéral actif.</summary>
    public ClaudeTabKind ActiveTab
    {
        get => _activeTab;
        set { _activeTab = value; Notify(); }
    }

    /// <summary>Texte de la zone de saisie.</summary>
    public string InputText
    {
        get => _inputText;
        set { _inputText = value; Notify(); }
    }

    /// <summary>Projet Cowork courant.</summary>
    public string CurrentProject
    {
        get => _currentProject;
        set { _currentProject = value; Notify(); }
    }

    /// <summary>Génération IA en cours.</summary>
    public bool Busy
    {
        get => _busy;
        set { _busy = value; Notify(); }
    }

    /// <summary>Sessions (onglet Code).</summary>
    public ObservableCollection<ClaudeSession> Sessions { get; } = new();

    /// <summary>Projets (onglet Cowork).</summary>
    public ObservableCollection<ClaudeProject> Projects { get; } = new();

    /// <summary>Messages de la conversation.</summary>
    public ObservableCollection<ClaudeChatMessage> Messages { get; } = new();

    /// <summary>Compétences (Personnaliser).</summary>
    public ObservableCollection<ClaudeSkill> Skills { get; } = new();

    /// <summary>Artéfacts.</summary>
    public ObservableCollection<ClaudeArtifact> Artifacts { get; } = new();

    /// <summary>Agents du workflow (Tâches).</summary>
    public ObservableCollection<ClaudeAgentRow> Agents { get; } = new();

    /// <summary>Statistiques d'accueil (8 tuiles).</summary>
    public List<(string Label, string Value)> HomeStats { get; } = new()
    {
        ("Sessions","44"), ("Messages","52 563"), ("Total de tokens","37.6M"), ("Jours actifs","26"),
        ("Série actuelle","3j"), ("Plus longue série","12j"), ("Heure de pointe","13 h"), ("Modèle favori","Sonnet 5")
    };

    /// <summary>Construit le VM avec les données d'exemple des captures.</summary>
    public ClaudeShellViewModel()
    {
        SeedSessions();
        SeedProjects();
        SeedMessages();
        SeedSkills();
        SeedArtifacts();
        SeedAgents();
    }

    /// <summary>Change la vue principale.</summary>
    public void SwitchView(ClaudeViewKind view) => CurrentView = view;

    /// <summary>Change d'onglet latéral.</summary>
    public void SwitchTab(ClaudeTabKind tab) => ActiveTab = tab;

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void SeedSessions()
    {
        Sessions.Add(new ClaudeSession { Number = 1, Title = "Supervised Self-Repair - Keeping a Code" });
        Sessions.Add(new ClaudeSession { Number = 2, Title = "Chaturbate Recorder - Part 10" });
        Sessions.Add(new ClaudeSession { Number = 3, Title = "Stream Recorder Pro - Part 1" });
        Sessions.Add(new ClaudeSession { Number = 4, Title = "Snake 2000 - Part 4" });
        Sessions.Add(new ClaudeSession { Number = 5, Title = "Xeno-SSS Orchestrator Agent - Part7" });
        Sessions.Add(new ClaudeSession { Number = 6, Title = "MOTO Editor - Part1", IsSelected = true });
    }

    private void SeedProjects()
    {
        Projects.Add(new ClaudeProject { Name = "WebSite", IsSelected = true });
        Projects.Add(new ClaudeProject { Name = "Chaturbate Recording" });
    }

    private void SeedMessages()
    {
        Messages.Add(new ClaudeChatMessage { Kind = ClaudeMsgKind.User, Text = "29% de budget restant, grâce à l'orchestrateur le chantier F5 n'a pris que 1%." });
        Messages.Add(new ClaudeChatMessage { Kind = ClaudeMsgKind.AiBox, Lines = { "On a de la marge pour 2-3 petits chantiers de plus. Lesquels ?", "Bouton «détacher» un panneau, Réveiller les 2 derniers fichiers «quasi prêts», Nettoyer l'exclusivité des panneaux" } });
        Messages.Add(new ClaudeChatMessage { Kind = ClaudeMsgKind.Act, Text = "Recherché code, modifié 2 fichiers, exécuté une commande, lu un fichier", Add = 30, Del = 28 });
        Messages.Add(new ClaudeChatMessage { Kind = ClaudeMsgKind.Ai, Text = "0 erreur du premier coup. Build Release aussi, puis lancement pour test." });
        Messages.Add(new ClaudeChatMessage { Kind = ClaudeMsgKind.Act, Text = "Exécuté 3 commandes" });
        Messages.Add(new ClaudeChatMessage { Kind = ClaudeMsgKind.Ai, Text = "Commité (b6f0b9d). Trois chantiers faits aujourd'hui." });
        Messages.Add(new ClaudeChatMessage { Kind = ClaudeMsgKind.UserCmd, Text = "/compact" });
    }

    private void SeedSkills()
    {
        Skills.Add(new ClaudeSkill { Name = "import-memory", Desc = "Import a memory export from another AI assistant into Claude's memory — conversa…" });
        Skills.Add(new ClaudeSkill { Name = "morning", Desc = "Render the user's morning brief as a styled HTML artifact, or set it up as a recurring w…" });
        Skills.Add(new ClaudeSkill { Name = "setup-writing-style", Desc = "Learns how the user writes from their own sent messages and docs, and builds a voic…" });
        Skills.Add(new ClaudeSkill { Name = "skill-creator", Desc = "Create new skills, modify and improve existing skills, and measure skill performance. …" });
    }

    private void SeedArtifacts()
    {
        Artifacts.Add(new ClaudeArtifact { Title = "Supervised Self-Repair - Keeping a Code-Fixing Agent on a Leash", Kind = "doc", When = "Modifié il y a 4 jours", IsPinned = true });
        Artifacts.Add(new ClaudeArtifact { Title = "Gains des réducteurs", Kind = "chart", When = "Modifié il y a 2 semaines", IsShared = true });
    }

    private void SeedAgents()
    {
        Agents.Add(new ClaudeAgentRow { Name = "panels", Tokens = "130.7k", Time = "3min 43s" });
        Agents.Add(new ClaudeAgentRow { Name = "commands", Tokens = "121.1k", Time = "3min 44s" });
        Agents.Add(new ClaudeAgentRow { Name = "settings", Tokens = "137.5k", Time = "2min 31s" });
        Agents.Add(new ClaudeAgentRow { Name = "plugins", Tokens = "136.4k", Time = "4min 16s", IsHot = true });
        Agents.Add(new ClaudeAgentRow { Name = "persistence", Tokens = "115.3k", Time = "3min 22s" });
    }
}
