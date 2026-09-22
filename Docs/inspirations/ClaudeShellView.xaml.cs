// Moto.Editor/Views/Claude/ClaudeShellView.xaml.cs
// Code-behind du shell. Méthodes courtes (< 40 lignes), additif.
using Microsoft.Maui.Controls;
using Moto.Editor.Models.Claude;
using Moto.Editor.ViewModels;
using Microsoft.Maui.Graphics;

namespace Moto.Editor.Views.Claude;

/// <summary>Sélecteur de template par type de message.</summary>
public sealed class ClaudeMsgSelector : DataTemplateSelector
{
    /// <summary>Template bulle utilisateur.</summary>
    public DataTemplate? UserTemplate { get; set; }

    /// <summary>Template commande (/compact).</summary>
    public DataTemplate? CmdTemplate { get; set; }

    /// <summary>Template message IA.</summary>
    public DataTemplate? AiTemplate { get; set; }

    /// <summary>Template boîte IA.</summary>
    public DataTemplate? BoxTemplate { get; set; }

    /// <summary>Template activité (diff).</summary>
    public DataTemplate? ActTemplate { get; set; }

    /// <summary>Template système.</summary>
    public DataTemplate? SysTemplate { get; set; }

    /// <summary>Choisit le template selon le Kind.</summary>
    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        var m = (ClaudeChatMessage)item;
        return m.Kind switch
        {
            ClaudeMsgKind.User => UserTemplate!,
            ClaudeMsgKind.UserCmd => CmdTemplate!,
            ClaudeMsgKind.AiBox => BoxTemplate!,
            ClaudeMsgKind.Act => ActTemplate!,
            ClaudeMsgKind.Sys => SysTemplate!,
            _ => AiTemplate!
        };
    }
}

/// <summary>Dessine la heatmap d'activité (accueil).</summary>
public sealed class ClaudeHeatmapDrawable : IDrawable
{
    /// <summary>Dessine la grille 26×7.</summary>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float cell = 15f, gap = 3f;
        for (int c = 0; c < 26; c++)
        {
            for (int r = 0; r < 7; r++)
            {
                canvas.FillColor = LevelColor(c, r);
                float x = c * (cell + gap), y = r * (cell + gap);
                canvas.FillRoundedRectangle(x, y, cell, cell, 3.5f);
            }
        }
    }

    private static Color LevelColor(int c, int r)
    {
        int seed = (c * 13 + r * 7) % 10;
        int lv = c >= 19 ? (c >= 22 ? (seed < 2 ? 1 : seed < 5 ? 2 : 3) : (seed < 3 ? 0 : seed < 7 ? 1 : 2)) : 0;
        return lv switch
        {
            1 => Color.FromArgb("#9DC0F5"),
            2 => Color.FromArgb("#5B96EE"),
            3 => Color.FromArgb("#2F7DE0"),
            _ => Color.FromArgb("#26282A")
        };
    }
}

/// <summary>Shell complet type Claude Code (conversion HTML v4).</summary>
public partial class ClaudeShellView : ContentView
{
    private readonly ClaudeShellViewModel _vm = new();
    private ClaudeViewKind _rightOpen = ClaudeViewKind.Chat;

    /// <summary>Construit la vue et lie le ViewModel.</summary>
    public ClaudeShellView()
    {
        InitializeComponent();
        BindingContext = _vm;
        HeatmapView.Drawable = new ClaudeHeatmapDrawable();
        ApplyView();
    }

    /// <summary>Applique la vue courante (commutation IsVisible).</summary>
    private void ApplyView()
    {
        ChatHeader.IsVisible = _vm.CurrentView == ClaudeViewKind.Chat;
        BottomZone.IsVisible = _vm.CurrentView == ClaudeViewKind.Chat;
        ChatList.IsVisible = _vm.CurrentView == ClaudeViewKind.Chat;
        HomePanel.IsVisible = _vm.CurrentView == ClaudeViewKind.Scheduled;
        CoworkPanel.IsVisible = _vm.CurrentView == ClaudeViewKind.Cowork;
        CustomizePanel.IsVisible = _vm.CurrentView == ClaudeViewKind.Customize;
        ArtifactsPanel.IsVisible = _vm.CurrentView == ClaudeViewKind.Artifacts;
        var coworkTop = _vm.ActiveTab == ClaudeTabKind.Cowork;
        CodeTop.IsVisible = !coworkTop;
        CoworkTop.IsVisible = coworkTop;
    }

    private void OnTabCode(object? s, EventArgs e) { _vm.SwitchTab(ClaudeTabKind.Code); _vm.SwitchView(ClaudeViewKind.Chat); ApplyView(); }
    private void OnTabCowork(object? s, EventArgs e) { _vm.SwitchTab(ClaudeTabKind.Cowork); _vm.SwitchView(ClaudeViewKind.Cowork); ApplyView(); }
    private void OnNavArtifacts(object? s, EventArgs e) { _vm.SwitchView(ClaudeViewKind.Artifacts); ApplyView(); }
    private void OnNavCustomize(object? s, EventArgs e) { _vm.SwitchView(ClaudeViewKind.Customize); ApplyView(); }
    private void OnNavScheduled(object? s, EventArgs e) { _vm.SwitchView(ClaudeViewKind.Scheduled); ApplyView(); }
    private void OnNavProjects(object? s, EventArgs e) { _vm.SwitchView(ClaudeViewKind.Projects); ApplyView(); }
    private void OnMenuNewSession(object? s, EventArgs e) { _vm.SwitchTab(ClaudeTabKind.Code); _vm.SwitchView(ClaudeViewKind.Chat); ApplyView(); }

    private void OnToggleSidebar(object? s, EventArgs e)
        => SidebarBox.IsVisible = !SidebarBox.IsVisible;

    private void OnSidebarPanUpdated(object? s, PanUpdatedEventArgs e)
    {
        if (e.StatusType == GestureStatus.Running)
        {
            double w = Math.Clamp(280 + e.TotalX, 210, 430);
            SidebarBox.WidthRequest = w;
        }
    }

    private void OnActChevronClicked(object? s, EventArgs e)
    {
        if ((s as Button)?.BindingContext is ClaudeChatMessage m) m.ToggleDetails();
    }

    private void OnSendClicked(object? s, EventArgs e)
    {
        var txt = ChatInput.Text?.Trim();
        if (string.IsNullOrEmpty(txt)) return;
        _vm.Messages.Add(new ClaudeChatMessage
        {
            Kind = txt.StartsWith("/") ? ClaudeMsgKind.UserCmd : ClaudeMsgKind.User,
            Text = txt
        });
        ChatInput.Text = string.Empty;
        SimulateReply();
    }

    private async void SimulateReply()
    {
        _vm.Busy = true;
        await Task.Delay(700);
        _vm.Messages.Add(new ClaudeChatMessage { Kind = ClaudeMsgKind.Act, Text = "Lu 2 fichiers, recherché code", Add = 14, Del = 1 });
        await Task.Delay(700);
        _vm.Messages.Add(new ClaudeChatMessage { Kind = ClaudeMsgKind.Ai, Text = "Proposition prête (diff affiché, jamais auto-appliqué). Build : 0 erreur. Tu valides ?" });
        _vm.Busy = false;
    }

    private void OnStopClicked(object? s, EventArgs e) => _vm.Busy = false;

    private void ShowRightOnly(View target)
    {
        TermPanel.IsVisible = target == TermPanel;
        BrowserPanel.IsVisible = target == BrowserPanel;
        TasksPanel.IsVisible = target == TasksPanel;
        TransPanel.IsVisible = target == TransPanel;
        RightHost.IsVisible = true;
    }

    private void OnToggleTerminal(object? s, EventArgs e)
    {
        if (TermPanel.IsVisible) { RightHost.IsVisible = false; return; }
        ShowRightOnly(TermPanel);
        if (string.IsNullOrEmpty(TermOut.Text))
            TermOut.Text = "Windows PowerShell\nCopyright (C) Microsoft Corporation. All rights reserved.\n";
    }

    private void OnToggleBrowser(object? s, EventArgs e)
    {
        if (BrowserPanel.IsVisible) { RightHost.IsVisible = false; return; }
        ShowRightOnly(BrowserPanel);
    }

    private void OnToggleTasks(object? s, EventArgs e)
    {
        if (TasksPanel.IsVisible) { RightHost.IsVisible = false; return; }
        ShowRightOnly(TasksPanel);
    }

    private void OnToggleTranscription(object? s, EventArgs e)
    {
        if (TransPanel.IsVisible) { RightHost.IsVisible = false; return; }
        ShowRightOnly(TransPanel);
    }

    private void OnTermCommand(object? s, EventArgs e)
    {
        var cmd = TermIn.Text?.Trim() ?? string.Empty;
        TermIn.Text = string.Empty;
        TermOut.Text += "\nPS E:\\Corpus\\MOTO-Editor> " + cmd + "\n" + TermReply(cmd);
        TermScroll.ScrollToAsync(0, TermScroll.ContentSize.Height, false);
    }

    private static string TermReply(string cmd)
    {
        var c = cmd.Split(' ')[0].ToLowerInvariant();
        return c switch
        {
            "help" => "commandes : help · clear · echo · dir · build · ollama",
            "dir" => "Moto.Editor/  Moto.Core/  Snake2000.Engine/  Docs/  CLAUDE.md",
            "build" => "Compilation Moto.Editor…\n0 erreur · 47 warnings (objectif < 50)",
            "ollama" => "modèles locaux : moto-ai:7b (actif) · codellama:13b",
            "clear" => "",
            _ => "commande inconnue : " + c
        };
    }

    private void OnBrowserNavigate(object? s, EventArgs e)
    {
        var url = BrowserUrl.Text?.Trim();
        if (!string.IsNullOrEmpty(url)) BrowserWeb.Source = new UrlWebViewSource { Url = url };
    }

    private void OnBrowserReload(object? s, EventArgs e) => BrowserWeb.Reload();

    private void OnContextTapped(object? s, TappedEventArgs e)
        => ContextPop.IsVisible = !ContextPop.IsVisible;

    private void OnOpenFeedback(object? s, EventArgs e) => FeedbackOverlay.IsVisible = true;
    private void OnFeedbackCancel(object? s, EventArgs e) => FeedbackOverlay.IsVisible = false;
    private void OnFeedbackSend(object? s, EventArgs e) { FeedbackOverlay.IsVisible = false; FeedbackText.Text = string.Empty; }

    private void OnSearchClicked(object? s, EventArgs e) => ChatInput.Focus();
    private void OnUserMenuClicked(object? s, EventArgs e) { /* flyout natif */ }
    private void OnUserSettings(object? s, EventArgs e) => _vm.SwitchView(ClaudeViewKind.Customize);
    private void OnMenuClicked(object? s, EventArgs e) { /* flyout natif */ }

    private void OnWindowMinClicked(object? s, EventArgs e) => MinimizeNative();
    private void OnWindowMaxClicked(object? s, EventArgs e) => ToggleMaxNative();
    private void OnWindowCloseClicked(object? s, EventArgs e) => CloseNative();

#if WINDOWS
    private Microsoft.UI.Xaml.Window? NativeWindow =>
        Application.Current?.Windows?.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;

    private void MinimizeNative()
    {
        var w = NativeWindow;
        if (w?.AppWindow is not null) w.AppWindow.Presenter = null;
    }

    private void ToggleMaxNative()
    {
        var w = NativeWindow;
        if (w is null) return;
        // Bascule plein écran via le présentateur système.
    }

    private void CloseNative() => Application.Current?.Windows?.FirstOrDefault()?.Destroy();
#else
    private void MinimizeNative() { }
    private void ToggleMaxNative() { }
    private void CloseNative() { }
#endif
}
