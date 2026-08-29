using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace Moto.Shared;

/// <summary>Ed25519 minimaliste (sign+verify), 100 % maison, sans lib externe.</summary>
public static class Ed25519
{
    static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
    static readonly BigInteger L = BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819949");
    static readonly BigInteger D = Mod(-121665 * Inv(121666), P);
    static readonly BigInteger I = BigInteger.ModPow(2, (P - 1) / 4, P);
    static readonly BigInteger By = Mod(4 * Inv(5), P);
    static readonly BigInteger Bx = RecoverX(By);

    static BigInteger Mod(BigInteger a, BigInteger m) => ((a % m) + m) % m;
    static BigInteger Inv(BigInteger a) => BigInteger.ModPow(a, P - 2, P);

    static BigInteger RecoverX(BigInteger y)
    {
        var y2 = y * y;
        var xx = (y2 - 1) * Inv(D * y2 + 1) % P;
        var x = BigInteger.ModPow(xx, (P + 3) / 8, P);
        if (Mod(x * x - xx, P) != 0) x = Mod(x * I, P);
        if (x.IsEven) x = P - x;
        return x;
    }

    // Point (x,y) ops sur la courbe
    static (BigInteger, BigInteger) Add((BigInteger, BigInteger) p, (BigInteger, BigInteger) q)
    {
        var (x1, y1) = p; var (x2, y2) = q;
        var dxy = D * x1 * x2 % P * y1 % P * y2 % P;
        var x3 = (x1 * y2 + x2 * y1) % P * Inv(1 + dxy) % P;
        var y3 = (y1 * y2 + x1 * x2) % P * Inv(1 - dxy) % P;
        return (Mod(x3, P), Mod(y3, P));
    }

    static (BigInteger, BigInteger) ScalarMult(BigInteger e, (BigInteger, BigInteger) p)
    {
        var r = (0, 1);
        while (e > 0)
        {
            if (e.IsOdd) r = Add(r, p);
            p = Add(p, p);
            e >>= 1;
        }
        return r;
    }

    static byte[] EncodeLE(BigInteger v, int len = 32)
    {
        var b = v.ToByteArray();
        var r = new byte[len];
        for (int i = 0; i < Math.Min(len, b.Length); i++) r[i] = b[i];
        return r;
    }

    static BigInteger DecodeLE(byte[] b)
    {
        var r = new byte[b.Length + 1];
        Array.Copy(b, r, b.Length);
        return new BigInteger(r, isUnsigned: false);
    }

    static byte[] Hash(byte[] m)
    {
        using var sha = SHA512.Create();
        return sha.ComputeHash(m);
    }

    static byte[] EncodePoint((BigInteger, BigInteger) p)
    {
        var (x, y) = p;
        var r = EncodeLE(y);
        if (x.IsOdd) r[31] |= 0x80;
        return r;
    }

    static (BigInteger, BigInteger) DecodePoint(byte[] s)
    {
        var b = (byte[])s.Clone();
        bool xSign = (b[31] & 0x80) != 0;
        b[31] &= 0x7F;
        var y = DecodeLE(b);
        var x = RecoverX(y);
        if (x.IsOdd != xSign) x = P - x;
        return (x, y);
    }

    public static (byte[] pub, byte[] priv) GenerateKey()
    {
        var priv = new byte[32];
        RandomNumberGenerator.Fill(priv);
        var h = Hash(priv);
        var a = Clamp(h);
        var pub = EncodePoint(ScalarMult(a, (Bx, By)));
        return (pub, priv);
    }

    static BigInteger Clamp(byte[] h)
    {
        var a = DecodeLE(h.Take(32).ToArray());
        a &= BigInteger.Pow(2, 254) + (BigInteger.Pow(2, 252) * 0); // clear bit255
        a |= BigInteger.Pow(2, 254);
        a &= ~BigInteger.One & a | a; // garde
        return a;
    }

    public static byte[] Sign(byte[] message, byte[] priv)
    {
        var h = Hash(priv);
        var a = Clamp(h);
        var pub = EncodePoint(ScalarMult(a, (Bx, By)));
        var r = Mod(DecodeLE(Hash(h.Skip(32).Take(32).ToArray().Concat(message).ToArray())), L);
        var R = EncodePoint(ScalarMult(r, (Bx, By)));
        var k = Mod(DecodeLE(Hash(R.Concat(pub).Concat(message).ToArray())), L);
        var S = Mod(r + k * a, L);
        return R.Concat(EncodeLE(S)).ToArray();
    }

    public static bool Verify(byte[] message, byte[] signature, byte[] pub)
    {
        if (signature.Length != 64 || pub.Length != 32) return false;
        var R = DecodePoint(signature.Take(32).ToArray());
        var S = DecodeLE(signature.Skip(32).ToArray());
        var A = DecodePoint(pub);
        var k = Mod(DecodeLE(Hash(signature.Take(32).Concat(pub).Concat(message).ToArray())), L);
        var left = ScalarMult(S, (Bx, By));
        var right = Add(R, ScalarMult(k, A));
        return left == right;
    }
}
