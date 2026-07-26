using Ft.Core.Time;

namespace Ft.Core.Licensing;

public enum LicenseStatus
{
    Trial,
    TrialExpired,
    Licensed,
}

public sealed record LicenseState(LicenseStatus Status, int TrialDaysLeft, string Detail);

/// <summary>
/// 14-day trial bookkeeping. The trial start date persists in a small file
/// under the app data dir; a valid license key short-circuits everything.
/// </summary>
public sealed class TrialManager(string stateFilePath, byte[] licensePublicKey, ITimeSource time)
{
    public const int TrialDays = 14;

    public LicenseState Evaluate(string? licenseKey)
    {
        var now = time.Now.ToUniversalTime();

        if (!string.IsNullOrWhiteSpace(licenseKey))
        {
            var verified = LicenseVerifier.Verify(licensePublicKey, licenseKey, now);
            if (verified.IsOk)
            {
                return new LicenseState(LicenseStatus.Licensed, 0, $"Licensed to {verified.Value.Email}");
            }
        }

        DateTimeOffset trialStart = ReadOrCreateTrialStart(now);
        int daysUsed = (int)Math.Floor((now - trialStart).TotalDays);
        int daysLeft = TrialDays - daysUsed;
        return daysLeft > 0
            ? new LicenseState(LicenseStatus.Trial, daysLeft, $"Trial · {daysLeft} days left")
            : new LicenseState(LicenseStatus.TrialExpired, 0, "Trial expired");
    }

    private DateTimeOffset ReadOrCreateTrialStart(DateTimeOffset now)
    {
        try
        {
            if (File.Exists(stateFilePath) &&
                DateTimeOffset.TryParse(File.ReadAllText(stateFilePath).Trim(), out var stored))
            {
                // A stored date in the future means the clock was rolled back.
                return stored <= now ? stored : now;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
            File.WriteAllText(stateFilePath, now.ToString("O"));
            return now;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unwritable state dir: fall back to an in-memory trial start.
            return now;
        }
    }
}
