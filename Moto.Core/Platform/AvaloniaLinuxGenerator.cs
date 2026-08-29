// Moto.Core/Platform/AvaloniaLinuxGenerator.cs (v2 — vrai éditeur Linux)
using System.Collections.Generic;

namespace Moto.Core.Platform
{
    /// <summary>
    /// 2. Génère Moto.Linux : un VRAI éditeur Avalonia qui réutilise
    /// Moto.Core (MotoAiKernel + AutoLinkEngine) pour parité fonctionnelle.
    /// </summary>
    public static class AvaloniaLinuxGenerator
    {
        public static List<PlatformFileAction> Generate(string rootNamespace)
        {
            var files = new List<PlatformFileAction>();

            // ------------------------------------------------------------
            // Projet
            // ------------------------------------------------------------
            files.Add(new PlatformFileAction
            {
                RelativePath = "Moto.Linux/Moto.Linux.csproj",
                Reason = "Projet Avalonia Linux (référence Moto.Core).",
                Content = @"<Project Sdk='Microsoft.NET.Sdk'>
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include='Avalonia' Version='11.0.10' />
    <PackageReference Include='Avalonia.Desktop' Version='11.0.10' />
    <PackageReference Include='Avalonia.Themes.Fluent' Version='11.0.10' />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include='../Moto.Core/Moto.Core.csproj' />
  </ItemGroup>
</Project>
"
            });

            files.Add(new PlatformFileAction
            {
                RelativePath = "Moto.Linux/Program.cs",
                Reason = "Point d'entrée Avalonia.",
                Content = @"using Avalonia;
using System;

namespace Moto.Linux;

class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
"
            });

            files.Add(new PlatformFileAction
            {
                RelativePath = "Moto.Linux/App.axaml",
                Reason = "Application Avalonia.",
                Content = @"<Application xmlns='https://github.com/avaloniaui'
             xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
             x:Class='Moto.Linux.App'>
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
</Application>
"
            });

            files.Add(new PlatformFileAction
            {
                RelativePath = "Moto.Linux/App.axaml.cs",
                Reason = "Code-behind application.",
                Content = @"using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Moto.Linux;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
"
            });

            // ------------------------------------------------------------
            // Fenêtre principale : explorateur + éditeur + panneau IA
            // ------------------------------------------------------------
            files.Add(new PlatformFileAction
            {
                RelativePath = "Moto.Linux/MainWindow.axaml",
                Reason = "Éditeur Linux complet (explorateur + éditeur + IA).",
                Content = @"<Window xmlns='https://github.com/avaloniaui'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        x:Class='Moto.Linux.MainWindow'
        Title='MOTO Editor (Linux)'
        Width='1280' Height='820'>
    <DockPanel>
        <Menu DockPanel.Dock='Top'>
            <MenuItem Header='Fichier'>
                <MenuItem Header='Ouvrir un projet' Click='OnOpenFolder' />
                <MenuItem Header='Sauvegarder' Click='OnSave' />
            </MenuItem>
            <MenuItem Header='IA'>
                <MenuItem Header='AutoLink' Click='OnAutoLink' />
            </MenuItem>
        </Menu>

        <TextBlock DockPanel.Dock='Bottom' x:Name='Status'
                   Text='Prêt.' Padding='8,4' FontSize='12' />

        <Grid ColumnDefinitions='250,*,330'>
            <TreeView Grid.Column='0' x:Name='Explorer'
                      SelectionChanged='OnFileSelected' />

            <TextBox Grid.Column='1' x:Name='Editor'
                     AcceptsReturn='True' AcceptsTab='True'
                     FontFamily='Consolas,monospace' FontSize='14' />

            <DockPanel Grid.Column='2' Margin='6,0,0,0'>
                <Button DockPanel.Dock='Top' x:Name='SendAi'
                        Content='Envoyer à MOTO AI' Click='OnSendAi' />
                <TextBox DockPanel.Dock='Top' x:Name='AiInput'
                         Watermark='Question / commande…' />
                <TextBox x:Name='AiLog' IsReadOnly='True'
                         AcceptsReturn='True' TextWrapping='Wrap' />
            </DockPanel>
        </Grid>
    </DockPanel>
</Window>
"
            });

            // ------------------------------------------------------------
            // Logique : réutilise Moto.Core (kernel IA + AutoLink)
            // ------------------------------------------------------------
            files.Add(new PlatformFileAction
            {
                RelativePath = "Moto.Linux/MainWindow.axaml.cs",
                Reason = "Logique de l'éditeur Linux (Moto.Core).",
                Content = @"using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Moto.Core.AI.AutoLink;
using Moto.Core.AI.Internal;
using Moto.Core.AI.Internal.Models;

namespace Moto.Linux;

/// <summary>
/// Éditeur Linux : parité fonctionnelle via Moto.Core.
/// </summary>
public partial class MainWindow : Window
{
    private string _root = string.Empty;
    private string _currentFile = string.Empty;
    private MotoAiKernel _kernel;
    private readonly AutoLinkEngine _autoLink = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Ouvrir un projet" };
        var path = await dlg.ShowAsync(this);

        if (string.IsNullOrWhiteSpace(path)) return;

        _root = path;
        _kernel = new MotoAiKernel(path);
        LoadTree();
        SetStatus($"Projet ouvert : {path}");
    }

    private void LoadTree()
    {
        Explorer.Items.Clear();

        foreach (var f in Directory.GetFiles(_root, "*.cs", SearchOption.AllDirectories))
        {
            Explorer.Items.Add(new TreeViewItem
            {
                Header = Path.GetFileName(f),
                Tag = f
            });
        }
    }

    private void OnFileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Explorer.SelectedItem is TreeViewItem item && item.Tag is string path)
        {
            _currentFile = path;
            Editor.Text = File.ReadAllText(path);
            SetStatus($"Ouvert : {Path.GetFileName(path)}");
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentFile)) return;

        File.WriteAllText(_currentFile, Editor.Text);
        SetStatus("Sauvegardé.");
    }

    private void OnSendAi(object sender, RoutedEventArgs e)
    {
        if (_kernel == null)
        {
            SetStatus("Ouvre d'abord un projet.");
            return;
        }

        var response = _kernel.Execute(new AiRequest
        {
            WorkspacePath = _root,
            UserText = AiInput.Text ?? string.Empty
        });

        AiLog.Text += $"\n> {AiInput.Text}\n{response.Title} — {response.Summary}\n";
        AiInput.Text = string.Empty;
    }

    private void OnAutoLink(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentFile)) return;

        var report = _autoLink.Analyze(_currentFile);

        AiLog.Text += $"\n[AutoLink] {report.Actions.Count} suggestion(s) :\n";

        foreach (var a in report.Actions)
        {
            AiLog.Text += $" - {a.Title}\n";
        }
    }

    private void SetStatus(string msg) => Status.Text = msg;
}
"
            });

            // Scripts + doc
            files.Add(new PlatformFileAction
            {
                RelativePath = "Tools/build-linux.sh",
                Reason = "Script de build Linux.",
                Content = "#!/bin/bash\nset -e\ndotnet build Moto.Linux/Moto.Linux.csproj -c Release\n"
            });

            files.Add(new PlatformFileAction
            {
                RelativePath = "Docs/LINUX.md",
                Reason = "Documentation du portage Linux.",
                Content = $"# Portage Linux\n\nÉditeur Avalonia complet dans Moto.Linux/ (explorateur, éditeur, IA via Moto.Core).\n"
            });

            return files;
        }
    }
}
