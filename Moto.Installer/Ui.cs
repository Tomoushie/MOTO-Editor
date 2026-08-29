namespace Moto.Installer;

public static class Ui
{
    public static void Banner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║        MOTO Editor — Setup           ║");
        Console.WriteLine("║  Léger. Rapide. Sans dépendance.     ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.ResetColor();
    }

    public static void Info(string m)    => WriteLine($"  {m}", ConsoleColor.Gray);
    public static void Warn(string m)    => WriteLine($"⚠ {m}", ConsoleColor.Yellow);
    public static void Error(string m)   => WriteLine($"❌ {m}", ConsoleColor.Red);
    public static void Success(string m) => WriteLine(m, ConsoleColor.Green);

    public static void Progress(int pct)
    {
        Console.Write($"\r  [{pct,3}%] Extraction…");
        if (pct >= 100) Console.WriteLine();
    }

    private static void WriteLine(string m, ConsoleColor c)
    {
        Console.ForegroundColor = c;
        Console.WriteLine(m);
        Console.ResetColor();
    }
}
