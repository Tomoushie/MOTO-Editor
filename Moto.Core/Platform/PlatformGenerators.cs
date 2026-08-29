// Moto.Core/Platform/PlatformGenerators.cs
using System.Collections.Generic;
using System.Linq;

namespace Moto.Core.Platform
{
    /// <summary>
    /// Générateurs de fichiers par plateforme :
    /// fichiers natifs, dossiers, configurations, scripts, pipelines CI.
    /// </summary>
    public static class PlatformGenerators
    {
        public static PlatformProposal BuildProposal(
            TargetPlatform platform, PlatformReport report, PlatformDetection detection)
        {
            var ns = string.IsNullOrWhiteSpace(report.RootNamespace) ? "MotoApp" : report.RootNamespace;

            var proposal = new PlatformProposal
            {
                Platform = platform,
                Confidence = detection.Confidence,
                Title = $"Générer les fichiers pour {platform}",
                Description = platform switch
                {
                    TargetPlatform.Android => "MainActivity, MainApplication, AndroidManifest + TFM net8.0-android.",
                    TargetPlatform.iOS => "AppDelegate, Program, Info.plist + TFM net8.0-ios.",
                    TargetPlatform.MacOS => "AppDelegate MacCatalyst, Program, Info.plist + TFM net8.0-maccatalyst.",
                    TargetPlatform.Linux => "Scripts de build/exécution + doc (MAUI ne cible pas Linux nativement).",
                    _ => "Configuration Windows + TFM net8.0-windows."
                }
            };

            switch (platform)
            {
                case TargetPlatform.Android:
                    AddAndroid(proposal, ns);
                    proposal.NewTargetFrameworks = AppendTfm(report, "net8.0-android");
                    break;
                case TargetPlatform.iOS:
                    AddIos(proposal, ns);
                    proposal.NewTargetFrameworks = AppendTfm(report, "net8.0-ios");
                    break;
                case TargetPlatform.MacOS:
                    AddMac(proposal, ns);
                    proposal.NewTargetFrameworks = AppendTfm(report, "net8.0-maccatalyst");
                    break;
                case TargetPlatform.Linux:
                    AddLinux(proposal, ns);
                    break;
                case TargetPlatform.Windows:
                    AddWindows(proposal, ns);
                    proposal.NewTargetFrameworks = AppendTfm(report, "net8.0-windows10.0.19041.0");
                    break;
            }

            return proposal;
        }

        private static string AppendTfm(PlatformReport report, string tfm)
        {
            var current = report.CurrentTargetFrameworks;
            if (string.IsNullOrWhiteSpace(current)) return $"net8.0;{tfm}";
            if (current.Contains(tfm)) return current;
            return $"{current};{tfm}";
        }

        // ------------------------------------------------------------------
        // ANDROID
        // ------------------------------------------------------------------
        private static void AddAndroid(PlatformProposal p, string ns)
        {
            p.Files.Add(new PlatformFileAction
            {
                RelativePath = "Platforms/Android/MainActivity.cs",
                Reason = "Activité principale Android.",
                Content = $@"using Android.App;
using Android.Content.PM;
using Android.OS;

namespace {ns};

[Activity(Theme = ""@style/Maui.SplashTheme"", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                         ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                         ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{{
}}
"
            });

            p.Files.Add(new PlatformFileAction
            {
                RelativePath = "Platforms/Android/MainApplication.cs",
                Reason = "Application Android (démarrage).",
                Content = $@"using Android.App;
using Android.Runtime;

namespace {ns};

[Application]
public class MainApplication : MauiApplication
{{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {{
    }}

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}}
"
            });

            p.Files.Add(new PlatformFileAction
            {
                RelativePath = "Platforms/Android/AndroidManifest.xml",
                Reason = "Manifeste Android (permissions de base).",
                Content = @"<?xml version='1.0' encoding='utf-8'?>
<manifest xmlns:android='http://schemas.android.com/apk/res/android'>
    <application android:allowBackup='true' android:supportsRtl='true'></application>
    <uses-permission android:name='android.permission.ACCESS_NETWORK_STATE' />
    <uses-permission android:name='android.permission.INTERNET' />
</manifest>
"
            });
        }

        // ------------------------------------------------------------------
        // iOS
        // ------------------------------------------------------------------
        private static void AddIos(PlatformProposal p, string ns)
        {
            p.Files.Add(new PlatformFileAction
            {
                RelativePath = "Platforms/iOS/AppDelegate.cs",
                Reason = "Delegate iOS.",
                Content = $@"using Foundation;

namespace {ns};

[Register(""AppDelegate"")]
public class AppDelegate : MauiUIApplicationDelegate
{{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}}
"
            });

            p.Files.Add(new PlatformFileAction
            {
                RelativePath = "Platforms/iOS/Program.cs",
                Reason = "Point d'entrée iOS.",
                Content = $@"using ObjCRuntime;
using UIKit;

namespace {ns};

public class Program
{{
    static void Main(string[] args)
    {{
        UIApplication.Main(args, null, typeof(AppDelegate));
    }}
}}
"
            });

            p.Files.Add(new PlatformFileAction
            {
                RelativePath = "Platforms/iOS/Info.plist",
                Reason = "Configuration iOS.",
                Content = @"<?xml version='1.0' encoding='UTF-8'?>
<!DOCTYPE plist PUBLIC '-//Apple//DTD PLIST 1.0//EN' 'http://www.apple.com/DTDs/PropertyList-1.0.dtd'>
<plist version='1.0'>
<dict>
    <key>LSRequiresIPhoneOS</key><true/>
    <key>UIDeviceFamily</key><array><integer>1</integer><integer>2</integer></array>
    <key>UIRequiredDeviceCapabilities</key><array><string>arm64</string></array>
    <key>UISupportedInterfaceOrientations</key>
    <array><string>UIInterfaceOrientationPortrait</string><string>UIInterfaceOrientationLandscapeLeft</string><string>UIInterfaceOrientationLandscapeRight</string></array>
</dict>
</plist>
"
            });
        }

        // ------------------------------------------------------------------
        // MacOS (MacCatalyst)
        // ------------------------------------------------------------------
        private static void AddMac(PlatformProposal p, string ns)
        {
            p.Files.Add(new PlatformFileAction
            {
                RelativePath = "Platforms/MacCatalyst/AppDelegate.cs",
                Reason = "Delegate MacCatalyst.",
                Content = $@"using Foundation;

namespace {ns};

[Register(""AppDelegate"")]
public class AppDelegate : MauiUIApplicationDelegate
{{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}}
"
            });

            p.Files.Add(new PlatformFileAction
            {
                RelativePath = "Platforms/MacCatalyst/Program.cs",
                Reason = "Point d'entrée MacCatalyst.",
                Content = $@"using ObjCRuntime;
using UIKit;

namespace {ns};

public class Program
{{
    static void Main(string[] args)
    {{
        UIApplication.Main(args, null, typeof(AppDelegate));
    }}
}}
"
            });

            p.Files.Add(new PlatformFileAction
            {
                RelativePath = "Platforms/MacCatalyst/Info.plist",
                Reason = "Configuration MacOS.",
                Content = @"<?xml version='1.0' encoding='UTF-8'?>
<!DOCTYPE plist PUBLIC '-//Apple//DTD PLIST 1.0//EN' 'http://www.apple.com/DTDs/PropertyList-1.0.dtd'>
<plist version='1.0'>
<dict>
    <key>LSMinimumSystemVersion</key><string>11.0</string>
    <key>UIDeviceFamily</key><array><integer>2</integer></array>
</dict>
</plist>
"
            });
        }

        // ------------------------------------------------------------------
        // LINUX (scripts + doc, MAUI ne cible pas Linux nativement)
        // ------------------------------------------------------------------
        // Moto.Core/Platform/PlatformGenerators.cs — AddLinux() ENRICHI
        // (remplace l'ancien AddLinux : ajoute le head Avalonia)
        private static void AddLinux(PlatformProposal p, string ns)
        {
            // Scripts + doc (comme avant)
            p.Files.Add(new PlatformFileAction
            {
                RelativePath = "Tools/build-linux.sh",
                Reason = "Script de build Linux.",
                Content = "#!/bin/bash\nset -e\ndotnet publish -c Release -r linux-x64 --self-contained false\n"
            });

            p.Files.Add(new PlatformFileAction
            {
                RelativePath = "Docs/LINUX.md",
                Reason = "Documentation du portage Linux.",
                Content = $"# Portage Linux de {ns}\n\nHead Avalonia généré dans Moto.Linux/.\n"
            });

            // 2. Head Avalonia complet
            p.Files.AddRange(AvaloniaLinuxGenerator.Generate(ns));

        // ------------------------------------------------------------------
        // WINDOWS
        // ------------------------------------------------------------------
        private static void AddWindows(PlatformProposal p, string ns)
        {
            p.Files.Add(new PlatformFileAction
            {
                RelativePath = "Docs/WINDOWS.md",
                Reason = "Documentation du portage Windows.",
                Content = $@"# Portage Windows de {ns}

Cible : net8.0-windows10.0.19041.0 (WinUI / MAUI).
Voir Platforms/Windows pour les fichiers natifs.
"
            });
        }
    }
}
