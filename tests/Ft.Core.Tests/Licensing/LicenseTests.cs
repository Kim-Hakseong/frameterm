using Ft.Core.Licensing;
using Ft.Core.Tests.TestUtil;
using Xunit;

namespace Ft.Core.Tests.Licensing;

public class LicenseTests
{
    private static readonly byte[] Seed =
        Convert.FromHexString("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60");
    private static byte[] PublicKey => Ed25519.GetPublicKey(Seed);
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void KeyRoundTrip_Verifies()
    {
        string key = LicenseVerifier.CreateKey(Seed, new LicensePayload { Email = "dev@example.com" });
        var verified = LicenseVerifier.Verify(PublicKey, key, Now);
        Assert.True(verified.IsOk);
        Assert.Equal("dev@example.com", verified.Value.Email);
    }

    [Fact]
    public void TamperedPayload_Fails()
    {
        string key = LicenseVerifier.CreateKey(Seed, new LicensePayload { Email = "a@b.c" });
        string[] parts = key.Split('.');
        // Different payload, original signature.
        string forged = LicenseVerifier.CreateKey(Seed, new LicensePayload { Email = "x@y.z" })
            .Split('.')[0] + "." + parts[1];
        Assert.False(LicenseVerifier.Verify(PublicKey, forged, Now).IsOk);
    }

    [Fact]
    public void ExpiredKey_Fails()
    {
        string key = LicenseVerifier.CreateKey(
            Seed, new LicensePayload { Email = "a@b.c", Expiry = "2026-01-01T00:00:00Z" });
        Assert.False(LicenseVerifier.Verify(PublicKey, key, Now).IsOk);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")]
    [InlineData("")]
    public void MalformedKeys_Fail(string key) =>
        Assert.False(LicenseVerifier.Verify(PublicKey, key, Now).IsOk);

    [Fact]
    public void TrialManager_FreshInstall_Starts14Days()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ft-trial-{Guid.NewGuid():N}", "trial.dat");
        try
        {
            var time = new FakeTimeSource();
            var manager = new TrialManager(path, PublicKey, time);
            var state = manager.Evaluate(null);
            Assert.Equal(LicenseStatus.Trial, state.Status);
            Assert.Equal(TrialManager.TrialDays, state.TrialDaysLeft);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void TrialManager_After15Days_Expired()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ft-trial-{Guid.NewGuid():N}", "trial.dat");
        try
        {
            var time = new FakeTimeSource();
            var manager = new TrialManager(path, PublicKey, time);
            manager.Evaluate(null); // creates the trial-start file
            time.Advance(15L * 24 * 60 * 60 * 1000);
            Assert.Equal(LicenseStatus.TrialExpired, manager.Evaluate(null).Status);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void TrialManager_ValidKey_OverridesTrial()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ft-trial-{Guid.NewGuid():N}", "trial.dat");
        try
        {
            var time = new FakeTimeSource();
            var manager = new TrialManager(path, PublicKey, time);
            manager.Evaluate(null);
            time.Advance(100L * 24 * 60 * 60 * 1000);
            string key = LicenseVerifier.CreateKey(Seed, new LicensePayload { Email = "dev@example.com" });
            var state = manager.Evaluate(key);
            Assert.Equal(LicenseStatus.Licensed, state.Status);
            Assert.Contains("dev@example.com", state.Detail);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
