using System.Text;
using System.Text.Json;

namespace Ft.Core.Licensing;

/// <summary>Signed license payload: who bought it and (optionally) when it expires.</summary>
public sealed class LicensePayload
{
    public string Email { get; set; } = string.Empty;
    public string Product { get; set; } = "FrameTerm";
    /// <summary>ISO-8601 UTC expiry; null/empty = perpetual.</summary>
    public string Expiry { get; set; } = string.Empty;
}

/// <summary>
/// Offline license key check: key = base64url(payloadJson) + "." +
/// base64url(ed25519Signature). Verification needs only the embedded public
/// key — no network.
/// </summary>
public static class LicenseVerifier
{
    public static string CreateKey(byte[] signingSeed, LicensePayload payload)
    {
        byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        byte[] signature = Ed25519.Sign(signingSeed, payloadBytes);
        return $"{Base64Url(payloadBytes)}.{Base64Url(signature)}";
    }

    public static Result<LicensePayload> Verify(byte[] publicKey, string key, DateTimeOffset nowUtc)
    {
        string[] parts = key.Trim().Split('.');
        if (parts.Length != 2) return Result<LicensePayload>.Fail("License key format is invalid.");

        byte[] payloadBytes;
        byte[] signature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            signature = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            return Result<LicensePayload>.Fail("License key encoding is invalid.");
        }

        if (!Ed25519.Verify(publicKey, payloadBytes, signature))
        {
            return Result<LicensePayload>.Fail("License signature does not verify.");
        }

        LicensePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<LicensePayload>(payloadBytes);
        }
        catch (JsonException)
        {
            return Result<LicensePayload>.Fail("License payload is malformed.");
        }
        if (payload is null) return Result<LicensePayload>.Fail("License payload is empty.");

        if (!string.IsNullOrEmpty(payload.Expiry))
        {
            if (!DateTimeOffset.TryParse(payload.Expiry, out var expiry))
            {
                return Result<LicensePayload>.Fail("License expiry is malformed.");
            }
            if (nowUtc > expiry) return Result<LicensePayload>.Fail("License has expired.");
        }

        return Result<LicensePayload>.Ok(payload);
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        string s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
