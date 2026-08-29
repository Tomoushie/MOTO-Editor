using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Moto.Shared;

public static class Sha256Helper
{
    public static string ComputeFile(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return ToHex(sha.ComputeHash(fs));
    }

    public static string ComputeBytes(byte[] data)
    {
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(data));
    }

    public static string ToHex(byte[] b)
    {
        var sb = new StringBuilder(b.Length * 2);
        foreach (var x in b) sb.Append(x.ToString("x2"));
        return sb.ToString();
    }

    public static byte[] FromHex(string hex)
    {
        var r = new byte[hex.Length / 2];
        for (int i = 0; i < r.Length; i++)
            r[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return r;
    }
}
