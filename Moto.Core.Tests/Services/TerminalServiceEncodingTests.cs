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
