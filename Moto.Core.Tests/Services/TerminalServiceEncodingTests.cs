// Moto.Core.Tests/Services/TerminalServiceEncodingTests.cs
using System.Collections.Concurrent;
using System.Text;
using Moto.Editor.Services;
using Xunit;

namespace Moto.Core.Tests.Services;

/// <summary>
/// ★ AJOUT (26/09) : accents du Terminal (« Tous droits r,serv,s. » sur la capture de Tom).
/// Les octets « OEM » sont ceux qu'écrit cmd.exe sur un Windows français (page 850).
/// </summary>
public class TerminalServiceEncodingTests
{
    private static readonly Encoding Oem850 = CodePagesEncodingProvider.Instance.GetEncoding(850)!;

    [Fact]
    public void Push_CmdBannerInOem850_KeepsAccents()
    {
        var bytes = Oem850.GetBytes("(c) Microsoft Corporation. Tous droits réservés.\r\n");
        Assert.Contains((byte)0x82, bytes); // « é » en page 850 — lu « ‚ » (virgule basse, page 1252) avant le correctif

        var lines = new TerminalOutputDecoder(Oem850).Push(bytes, bytes.Length);

        Assert.Equal(new[] { "(c) Microsoft Corporation. Tous droits réservés." }, lines);
    }

    [Fact]
    public void Push_Utf8Line_IsDecodedAsUtf8()
    {
        const string text = "MSBUILD : error MSB1003: Spécifiez un fichier projet ou solution.";
        var bytes = Encoding.UTF8.GetBytes(text + "\n");

        var lines = new TerminalOutputDecoder(Oem850).Push(bytes, bytes.Length);

        Assert.Equal(new[] { text }, lines);
    }

    [Fact]
    public void Push_Utf8CharacterSplitBetweenTwoReads_IsReassembled()
    {
        var bytes = Encoding.UTF8.GetBytes("déjà\n");
        var decoder = new TerminalOutputDecoder(Oem850);

        var first = decoder.Push(bytes[..2], 2); // « d » + 0xC3, premier octet de « é »
        var second = decoder.Push(bytes[2..], bytes.Length - 2);

        Assert.Empty(first);
        Assert.Equal(new[] { "déjà" }, second);
    }

    [Fact]
    public void Push_LineEndings_FollowReadLineRules()
    {
        var decoder = new TerminalOutputDecoder(Oem850);
        var chunk1 = Encoding.ASCII.GetBytes("a\r"); // \r\n coupé entre deux lectures
        var chunk2 = Encoding.ASCII.GetBytes("\nb\rc\n\nd\n");

        var first = decoder.Push(chunk1, chunk1.Length);
        var second = decoder.Push(chunk2, chunk2.Length);

        Assert.Equal(new[] { "a" }, first);
        Assert.Equal(new[] { "b", "c", "", "d" }, second);
    }

    [Fact]
    public void Flush_ReturnsLastLineWithoutEnding_Once()
    {
        var decoder = new TerminalOutputDecoder(Oem850);
        var prompt = Oem850.GetBytes(@"C:\Users\Téo>");

        Assert.Empty(decoder.Push(prompt, prompt.Length));
        Assert.Equal(@"C:\Users\Téo>", decoder.Flush());
        Assert.Null(decoder.Flush());
    }

    [Fact]
    public void DecodeAll_Utf8Output_IsExactlyAsBefore()
    {
        // Sortie typique de git (porcelain, CRLF, accents) : GitService l'analyse, elle doit rester
        // identique à l'ancien décodage UTF-8 imposé.
        var bytes = Encoding.UTF8.GetBytes("?? Chaîne.cs\r\n M déjà vu/été.txt\r\n");

        Assert.Equal(Encoding.UTF8.GetString(bytes), TerminalOutputDecoder.DecodeAll(bytes, Oem850));
    }

    [Fact]
    public void DecodeAll_MixedCmdAndUtf8Lines_KeepsEachLineAndItsEnding()
    {
        var bytes = Oem850.GetBytes("ou externe, un programme exécutable\r\n")
            .Concat(Encoding.UTF8.GetBytes("Spécifiez déjà\n"))
            .Concat(Encoding.ASCII.GetBytes("fin"))
            .ToArray();

        Assert.Equal("ou externe, un programme exécutable\r\nSpécifiez déjà\nfin",
            TerminalOutputDecoder.DecodeAll(bytes, Oem850));
    }

    [Fact]
    public void DecodeAll_OutputWithByteOrderMark_IsReadAsBefore()
    {
        // « wmic » écrit en UTF-16 précédé d'un BOM : l'ancien StreamReader le reconnaissait.
        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("Name\r\nIntel é\r\n")).ToArray();
        var utf8 = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("été")).ToArray();

        Assert.Equal("Name\r\nIntel é\r\n", TerminalOutputDecoder.DecodeAll(utf16, Oem850));
        Assert.Equal("été", TerminalOutputDecoder.DecodeAll(utf8, Oem850));
        Assert.Equal(string.Empty, TerminalOutputDecoder.DecodeAll(Array.Empty<byte>(), Oem850));
    }

    /// <summary>
    /// Bout en bout, commande « en coulisses » (panneau Git, agents) : le message de cmd (page OEM)
    /// et le fichier UTF-8 doivent ressortir lisibles, fins de ligne comprises. Celui-ci échoue bien
    /// avec l'ancien code, quel que soit l'exécuteur de tests : l'UTF-8 y était imposé.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RealCmd_OemAndUtf8LinesStayReadable()
    {
        if (!OperatingSystem.IsWindows()) return;

        var dir = Directory.CreateTempSubdirectory("moto-oneshot-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "utf8.txt"), "UTF8: Spécifiez déjà\n", new UTF8Encoding(false));

            var result = await new TerminalService().ExecuteAsync("echo OEM: réservés àçù&& type utf8.txt", dir);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("OEM: réservés àçù\r\nUTF8: Spécifiez déjà\n", result.Output);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* sans conséquence */ }
        }
    }

    /// <summary>
    /// Un symbole absent de la page OEM (« → », « ♪ »…) tapé dans le terminal doit partir en « ? ».
    /// Le remplacement « approchant » de .NET en faisait des caractères de contrôle — « → » = Ctrl+Z,
    /// que cmd lit comme une fin de saisie, « ♪ » = Entrée, « ◙ » = saut de ligne : la ligne était
    /// coupée et un morceau pouvait partir comme une commande à part (vu le 26/09).
    /// </summary>
    [Fact]
    public async Task Start_TypedSymbolOutsideOemPage_BecomesQuestionMark_WithoutSplittingTheLine()
    {
        if (!OperatingSystem.IsWindows()) return;

        var lines = new ConcurrentQueue<string>();
        var errors = new ConcurrentQueue<string>();
        var terminal = new TerminalService();
        terminal.OutputReceived += (line, isError) => (isError ? errors : lines).Enqueue(line);
        try
        {
            terminal.Start(Path.GetTempPath());
            terminal.SendInput("echo SYMBOLES: a→b♪c◙d♥e");
            terminal.SendInput("echo TOUJOURS LA");

            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline && !lines.Contains("TOUJOURS LA"))
                await Task.Delay(50);
        }
        finally
        {
            terminal.Stop();
        }

        Assert.Contains("SYMBOLES: a?b?c?d?e", lines);
        Assert.Contains("TOUJOURS LA", lines);
        Assert.Empty(errors); // avant : « 'd' n'est pas reconnu… », un morceau de la ligne lancé comme commande
    }

    /// <summary>Réglage Python déjà présent chez l'utilisateur : volontairement laissé tel quel, rien à vérifier.</summary>
    private static bool UserSetPython() =>
        Environment.GetEnvironmentVariable("PYTHONIOENCODING") != null
        || Environment.GetEnvironmentVariable("PYTHONUTF8") != null;

    /// <summary>Option B : un programme Python lancé en coulisses écrit en UTF-8 (voir TerminalService).</summary>
    [Fact]
    public async Task ExecuteAsync_TellsPythonToWriteUtf8()
    {
        if (!OperatingSystem.IsWindows() || UserSetPython()) return;

        var result = await new TerminalService().ExecuteAsync("echo %PYTHONIOENCODING%");

        Assert.Equal("utf-8:replace", result.Output.TrimEnd());
    }

    /// <summary>Option B : idem dans le terminal du bas.</summary>
    [Fact]
    public async Task Start_TellsPythonToWriteUtf8()
    {
        if (!OperatingSystem.IsWindows() || UserSetPython()) return;

        var lines = new ConcurrentQueue<string>();
        var terminal = new TerminalService();
        terminal.OutputReceived += (line, _) => lines.Enqueue(line);
        try
        {
            terminal.Start(Path.GetTempPath());
            terminal.SendInput("echo PY=%PYTHONIOENCODING%");

            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline && !lines.Contains("PY=utf-8:replace"))
                await Task.Delay(50);
        }
        finally
        {
            terminal.Stop();
        }

        Assert.Contains("PY=utf-8:replace", lines);
    }

    /// <summary>
    /// Bout en bout avec le vrai cmd.exe : un accent tapé doit arriver intact à cmd et en
    /// revenir intact (page OEM), et un fichier UTF-8 affiché par « type » rester lisible.
    /// Limite connue : l'exécuteur de tests possède une console, donc l'ancien code y décodait
    /// déjà en OEM et la bannière s'y affichait bien — le « r,serv,s » n'apparaît que dans une
    /// appli SANS console (MOTO). La bannière est couverte par Push_CmdBannerInOem850_KeepsAccents ;
    /// ici, c'est la ligne UTF-8 qui fait échouer l'ancien code (vérifié le 26/09).
    /// </summary>
    [Fact]
    public async Task Start_RealCmd_AccentsSurviveBothDirections()
    {
        if (!OperatingSystem.IsWindows()) return;

        var dir = Directory.CreateTempSubdirectory("moto-terminal-").FullName;
        File.WriteAllText(Path.Combine(dir, "utf8.txt"), "UTF8: Spécifiez déjà\n", new UTF8Encoding(false));
        var lines = new ConcurrentQueue<string>();
        var terminal = new TerminalService();
        terminal.OutputReceived += (line, _) => lines.Enqueue(line);
        try
        {
            terminal.Start(dir);
            terminal.SendInput("echo OEM: réservés àçù");
            terminal.SendInput("type utf8.txt");

            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline
                   && !(lines.Contains("OEM: réservés àçù") && lines.Contains("UTF8: Spécifiez déjà")))
                await Task.Delay(50);
        }
        finally
        {
            terminal.Stop();
            try { Directory.Delete(dir, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* cmd pas encore tout à fait fermé */ }
        }

        Assert.Contains("OEM: réservés àçù", lines);
        Assert.Contains("UTF8: Spécifiez déjà", lines);
        // Ni « ‚ » (0x82 lu en page 1252 : le bug de la bannière) ni caractère de remplacement.
        Assert.DoesNotContain(lines, l => l.Contains('\u201A') || l.Contains('\uFFFD'));
    }
}
