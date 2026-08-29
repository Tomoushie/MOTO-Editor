// Moto.Installer/Program.cs
using System.Diagnostics;
using Moto.Shared;

namespace Moto.Installer;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Ui.Banner();

        bool silent    = Has(args, "--silent") || Has(args, "/S");
        bool isUpdate  = Has(args, "--update");
        string? payload  = Get(args, "--payload");
        string? target   = Get(args, "--target");
        string? waitFor  = Get(args, "--wait-for");
        string? relaunch = Get(args, "--relaunch");

        try
        {
            var os = OsDetector.Detect();
            Ui.Info($"Système détecté : {os}");

            string installDir = target ?? InstallLocation.For(os);
            Ui.Info((isUpdate ? "Mise à jour vers : " : "Répertoire : ") + installDir);

            WaitForEditor(waitFor, silent);

            if (isUpdate)
                return ApplyUpdate(installDir, payload, os, relaunch);

            PayloadExtractor.ExtractTo(installDir, Ui.Progress, payload);
            FinalizeInstall(os, installDir, relaunch);
            Ui.Success("✅ Installation terminée !");
            return 0;
        }
        catch (Exception ex)
        {
            Ui.Error(ex.Message);
            return 1;
        }
    }

    // ★ Mise à jour : vérification crypto → extraction temp → swap atomique + rollback
    private static int ApplyUpdate(string installDir, string? payload, TargetOs os, string? relaunch)
    {
        string manifestPath = payload != null ? Path.ChangeExtension(payload, ".json") : "";

        // Vérification SHA256 + Ed25519 avant toute extraction
        if (File.Exists(manifestPath) &&
            !PayloadVerifier.VerifyPayload(payload!, manifestPath, BuildKeys.UpdatePublicKeyHex))
        {
            Ui.Error("Signature invalide : mise à jour refusée.");
            return 2;
        }

        // Extraction dans un dossier temporaire (jamais directement la cible)
        string tempExtract = Path.Combine(Path.GetTempPath(), "moto-update", "extract");
        if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
        PayloadExtractor.ExtractTo(tempExtract, Ui.Progress, payload);

        // Swap atomique + rollback automatique
        AtomicUpdater.Apply(installDir, tempExtract);

        FinalizeInstall(os, installDir, relaunch);
        Ui.Success("✅ Mise à jour terminée !");
        return 0;
    }

    private static void WaitForEditor(string? waitFor, bool silent)
    {
        if (waitFor != null && int.TryParse(waitFor, out int pid))
        {
            Ui.Info("Attente de la fermeture de MOTO Editor…");
            try { Process.GetProcessById(pid)?.WaitForExit(15000); } catch { }
        }
        else if (ProcessGuard.IsRunning())
        {
            Ui.Warn("MOTO Editor est en cours d'exécution. Fermez-le pour continuer.");
            if (!silent) Console.ReadKey();
            if (ProcessGuard.IsRunning()) throw new InvalidOperationException("Installation annulée.");
        }
    }

    private static void FinalizeInstall(TargetOs os, string installDir, string? relaunch)
    {
        ShortcutService.Create(os, installDir);
        UninstallRegistrar.Register(os, installDir);
        if (relaunch != null && File.Exists(relaunch))
        {
            Ui.Info("Redémarrage de MOTO Editor…");
            Process.Start(new ProcessStartInfo(relaunch) { UseShellExecute = true });
        }
    }

    private static bool Has(string[] args, string name) => args.Contains(name);
    private static string? Get(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
