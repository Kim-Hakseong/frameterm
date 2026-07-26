using System.Numerics;
using System.Security.Cryptography;

namespace Ft.Core.Licensing;

/// <summary>
/// Independent RFC 8032 Ed25519 implementation (BCL has no Ed25519 and
/// external crypto packages are off-limits). Affine arithmetic over
/// BigInteger — slow but license checks run once, and correctness is pinned
/// by the RFC 8032 test vectors.
/// </summary>
public static class Ed25519
{
    private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
    private static readonly BigInteger L =
        BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");
    private static readonly BigInteger D =
        Mod(-121665 * Inverse(121666));
    private static readonly BigInteger I =
        BigInteger.ModPow(2, (P - 1) / 4, P); // sqrt(-1)

    private static readonly (BigInteger X, BigInteger Y) BasePoint = (
        RecoverX(Mod(4 * Inverse(5)), 0),
        Mod(4 * Inverse(5)));

    public static byte[] GetPublicKey(byte[] seed)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(seed.Length, 32, nameof(seed));
        var (a, _) = ExpandSeed(seed);
        return EncodePoint(ScalarMult(BasePoint, a));
    }

    public static byte[] Sign(byte[] seed, byte[] message)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(seed.Length, 32, nameof(seed));
        var (a, prefix) = ExpandSeed(seed);
        byte[] publicKey = EncodePoint(ScalarMult(BasePoint, a));

        BigInteger r = HashToScalar(prefix, message);
        byte[] rEncoded = EncodePoint(ScalarMult(BasePoint, r));
        BigInteger k = HashToScalar([.. rEncoded, .. publicKey], message);
        BigInteger s = Mod(r + k * a, L);
        return [.. rEncoded, .. EncodeScalar(s)];
    }

    public static bool Verify(byte[] publicKey, byte[] message, byte[] signature)
    {
        if (publicKey.Length != 32 || signature.Length != 64) return false;
        try
        {
            var aPoint = DecodePoint(publicKey);
            var rPoint = DecodePoint(signature[..32]);
            BigInteger s = DecodeScalar(signature[32..]);
            if (s >= L) return false;

            BigInteger k = HashToScalar([.. signature[..32], .. publicKey], message);
            var left = ScalarMult(BasePoint, s);
            var right = Add(rPoint, ScalarMult(aPoint, Mod(k, L)));
            return left == right;
        }
        catch (ArgumentException)
        {
            return false; // malformed point encoding
        }
    }

    private static (BigInteger A, byte[] Prefix) ExpandSeed(byte[] seed)
    {
        byte[] h = SHA512.HashData(seed);
        byte[] scalarBytes = h[..32];
        scalarBytes[0] &= 0xF8;
        scalarBytes[31] &= 0x7F;
        scalarBytes[31] |= 0x40;
        return (new BigInteger(scalarBytes, isUnsigned: true, isBigEndian: false), h[32..]);
    }

    private static BigInteger HashToScalar(byte[] prefix, byte[] message) =>
        Mod(new BigInteger(
            SHA512.HashData([.. prefix, .. message]), isUnsigned: true, isBigEndian: false), L);

    private static BigInteger Mod(BigInteger x) => Mod(x, P);

    private static BigInteger Mod(BigInteger x, BigInteger m)
    {
        BigInteger r = x % m;
        return r < 0 ? r + m : r;
    }

    private static BigInteger Inverse(BigInteger x) => BigInteger.ModPow(Mod(x), P - 2, P);

    private static (BigInteger X, BigInteger Y) Add(
        (BigInteger X, BigInteger Y) p, (BigInteger X, BigInteger Y) q)
    {
        // Twisted Edwards addition (complete for Ed25519's curve).
        BigInteger denomX = Inverse(1 + D * p.X * q.X * p.Y * q.Y);
        BigInteger denomY = Inverse(1 - D * p.X * q.X * p.Y * q.Y);
        BigInteger x = Mod((p.X * q.Y + q.X * p.Y) * denomX);
        BigInteger y = Mod((p.Y * q.Y + p.X * q.X) * denomY);
        return (x, y);
    }

    private static (BigInteger X, BigInteger Y) ScalarMult(
        (BigInteger X, BigInteger Y) point, BigInteger scalar)
    {
        var result = (X: BigInteger.Zero, Y: BigInteger.One); // identity
        var addend = point;
        while (scalar > 0)
        {
            if (!scalar.IsEven) result = Add(result, addend);
            addend = Add(addend, addend);
            scalar >>= 1;
        }
        return result;
    }

    private static BigInteger RecoverX(BigInteger y, int sign)
    {
        BigInteger y2 = Mod(y * y);
        BigInteger xx = Mod((y2 - 1) * Inverse(D * y2 + 1));
        BigInteger x = BigInteger.ModPow(xx, (P + 3) / 8, P);
        if (Mod(x * x) != xx)
        {
            x = Mod(x * I);
            if (Mod(x * x) != xx) throw new ArgumentException("Point is not on the curve.");
        }
        if ((int)(x & 1) != sign) x = P - x;
        return x;
    }

    private static byte[] EncodePoint((BigInteger X, BigInteger Y) point)
    {
        byte[] bytes = EncodeScalar(point.Y);
        if (!(point.X & 1).IsZero) bytes[31] |= 0x80;
        return bytes;
    }

    private static (BigInteger X, BigInteger Y) DecodePoint(byte[] encoded)
    {
        byte[] yBytes = (byte[])encoded.Clone();
        int sign = (yBytes[31] & 0x80) != 0 ? 1 : 0;
        yBytes[31] &= 0x7F;
        BigInteger y = new(yBytes, isUnsigned: true, isBigEndian: false);
        if (y >= P) throw new ArgumentException("Invalid point encoding.");
        return (RecoverX(y, sign), y);
    }

    private static byte[] EncodeScalar(BigInteger value)
    {
        byte[] bytes = new byte[32];
        value.TryWriteBytes(bytes, out _, isUnsigned: true, isBigEndian: false);
        return bytes;
    }

    private static BigInteger DecodeScalar(byte[] bytes) =>
        new(bytes, isUnsigned: true, isBigEndian: false);
}
