using System.IO.Compression;
using System.Reflection;

namespace Moto.Installer;

public enum TargetOs { Windows, Linux, MacOS, Unknown }

public static class OsDetector
{
    public static TargetOs Detect()
    {
        if (OperatingSystem.IsWindows()) return TargetOs.Windows;
        if (OperatingSystem.IsMacOS())   return TargetOs.MacOS;
        if (OperatingSystem.IsLinux())   return TargetOs.Linux;
        return TargetOs.Unknown;
    }
}

public static class InstallLocation
{
    public static string For(TargetOs os) => os switch
    {
        TargetOs.Windows => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "MotoEditor"),
        TargetOs.Linux => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "moto-editor"),
        TargetOs.MacOS => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Applications", "Moto Editor"),
        _ => Path.Combine(AppContext.BaseDirectory, "MotoEditor")
    };
}

public static class ProcessGuard
{
    public static bool IsRunning() =>
        Process.GetProcessesByName("Moto.Editor").Length > 0 ||
        Process.GetProcessesByName("MotoEditor").Length > 0;
}

public static class PayloadExtractor
{
    public static void ExtractTo(string destination, Action<int> onProgress)
    {
        Directory.CreateDirectory(destination);
        string fullDest = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;

        using var zip = OpenPayload();
        var entries = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
        int done = 0, lastPct = -1;

        foreach (var entry in entries)
        {
            string target = Path.GetFullPath(Path.Combine(destination, entry.FullName));

            // Sécurité : empêche l'attaque "Zip Slip"
            if (!target.StartsWith(fullDest, StringComparison.Ordinal))
                throw new InvalidOperationException($"Entrée invalide : {entry.FullName}");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);

            int pct = ++done * 100 / entries.Count;
            if (pct != lastPct) { lastPct = pct; onProgress(pct); }
        }
    }

    private static ZipArchive OpenPayload()
    {
        // 1. Payload embarqué dans l'EXE
        var res = Assembly.GetExecutingAssembly()
                          .GetManifestResourceStream("Moto.Installer.payload.zip");
        if (res != null) return new ZipArchive(res, ZipArchiveMode.Read);

        // 2. Fallback : payload.zip à côté de l'installateur
        string side = Path.Combine(AppContext.BaseDirectory, "payload.zip");
        if (File.Exists(side)) return ZipFile.OpenRead(side);

        throw new FileNotFoundException("Payload introuvable (payload.zip manquant).");
    }
}

public static class ShortcutService
{
    public static void Create(TargetOs os, string installDir)
    {
        switch (os)
        {
            case TargetOs.Windows: CreateWindows(installDir); break;
            case TargetOs.Linux:   CreateLinux(installDir);   break;
            case TargetOs.MacOS:   CreateMacOs(installDir);   break;
        }
    }

    // ── Windows : .lnk via WScript.Shell (réflexion, sans interop externe) ──
    private static void CreateWindows(string installDir)
    {
        string exe = Path.Combine(installDir, "Moto.Editor.exe");
        string startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs");
        Directory.CreateDirectory(startMenu);

        CreateLnk(Path.Combine(startMenu, "MOTO Editor.lnk"), exe, installDir);
        CreateLnk(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "MOTO Editor.lnk"), exe, installDir);
    }

    private static void CreateLnk(string shortcutPath, string target, string workDir)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return;
            var shell = Activator.CreateInstance(shellType)!;
            var lnk = shellType.InvokeMember("CreateShortcut",
                BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath })!;
            var t = lnk.GetType();
            t.InvokeMember("TargetPath",       BindingFlags.SetProperty, null, lnk, new object[] { target });
            t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { workDir });
            t.InvokeMember("IconLocation",     BindingFlags.SetProperty, null, lnk, new object[] { target + ",0" });
            t.InvokeMember("Save",             BindingFlags.InvokeMethod, null, lnk, null);
        }
        catch { /* raccourci optionnel */ }
    }

    // ── Linux : .desktop + symlink ──
    private static void CreateLinux(string installDir)
    {
        string exe = Path.Combine(installDir, "Moto.Editor");
        TryChmod(exe);

        string appsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "applications");
        Directory.CreateDirectory(appsDir);
        File.WriteAllText(Path.Combine(appsDir, "moto-editor.desktop"),
            $"[Desktop Entry]\nName=MOTO Editor\nExec={exe}\nType=Application\nCategories=Development;IDE;\n");

        string binDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
        Directory.CreateDirectory(binDir);
        string link = Path.Combine(binDir, "moto-editor");
        if (File.Exists(link)) File.Delete(link);
        File.CreateSymbolicLink(link, exe);
    }

    // ── macOS : rend l'exécutable lançable ──
    private static void CreateMacOs(string installDir)
    {
        TryChmod(Path.Combine(installDir, "Moto.Editor"));
    }

    private static void TryChmod(string path)
    {
        try
        {
            if (File.Exists(path))
                System.Diagnostics.Process.Start("chmod", $"+x \"{path}\"")?.WaitForExit(2000);
        }
        catch { /* optionnel */ }
    }
}

public static class UninstallRegistrar
{
    public static void Register(TargetOs os, string installDir)
    {
        if (os != TargetOs.Windows) return;
        try
        {
            string uninstallCmd = Path.Combine(installDir, "uninstall.cmd");
            File.WriteAllText(uninstallCmd, BuildUninstallScript(installDir));

            // Entrée "Ajout/Suppression de programmes" (HKCU → per-user, sans admin)
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MotoEditor");
            key?.SetValue("DisplayName", "MOTO Editor");
            key?.SetValue("Publisher", "MOTO");
            key?.SetValue("InstallLocation", installDir);
            key?.SetValue("UninstallString", $"\"{uninstallCmd}\"");
            key?.SetValue("NoModify", 1, Microsoft.Win32.RegistryValueKind.DWord);
            key?.SetValue("NoRepair", 1, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch { /* optionnel */ }
    }

    private static string BuildUninstallScript(string installDir) => $@"@echo off
echo Désinstallation de MOTO Editor…
del /q ""%APPDATA%\Microsoft\Windows\Start Menu\Programs\MOTO Editor.lnk"" 2>nul
del /q ""%USERPROFILE%\Desktop\MOTO Editor.lnk"" 2>nul
reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\MotoEditor"" /f 2>nul
start """" cmd /c ""timeout /t 2 /nobreak >nul & rd /s /q ""{installDir}""""
";
}
