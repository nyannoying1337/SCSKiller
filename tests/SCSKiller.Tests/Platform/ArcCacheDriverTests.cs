using SCSKiller.Core;
using SCSKiller.Core.Vendors;
using Xunit;

namespace SCSKiller.Tests.Platform;

public class ArcCacheDriverTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "scs-arc-" + Guid.NewGuid().ToString("N"));
    readonly string _exe, _local, _dll, _hash;

    public ArcCacheDriverTests()
    {
        Directory.CreateDirectory(_dir);
        _exe = Path.Combine(_dir, "Game-Win64-Shipping.exe");
        File.WriteAllText(_exe, "exe");
        _local = _exe + ".local";
        Directory.CreateDirectory(_local);
        _dll = Path.Combine(_local, "igd12um64xe2.dll");
        File.WriteAllText(_dll, "patched driver");
        _hash = ArcCacheDriver.Sha256(_dll);
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    Dictionary<string, string?> Env(int mib = 16384) => new()
    {
        [ArcCacheDriver.DirVariable] = _local, [ArcCacheDriver.ExeVariable] = _exe, [ArcCacheDriver.MiBVariable] = mib.ToString(),
        [ArcCacheDriver.NameVariable] = "igd12um64xe2.dll", [ArcCacheDriver.HashVariable] = _hash.ToLowerInvariant(),
    };

    static Func<string, string?> From(Dictionary<string, string?> d) => k => d.TryGetValue(k, out var v) ? v : null;

    Game TheGame() => new("steam:1", "Game", Store.Steam, _dir, _exe);

    [Fact]
    public void NoVariablesIsNoHandoff() => Assert.Null(ArcCacheDriver.FromEnvironment(_ => null));

    [Fact]
    public void ValidHandoffBecomesWarmArguments()
    {
        var h = ArcCacheDriver.FromEnvironment(From(Env()))!;
        Assert.Equal(_hash, h.DriverSha256);
        h.Check(TheGame(), devOverride: true);
        Assert.Equal(["--driver-local", _local, "--driver-name", "igd12um64xe2.dll", "--driver-sha256", _hash, "--driver-mib", "16384"], h.WarmArgs());
        Assert.Equal(16384L << 20, ArcCacheDriver.RequestedCap(From(Env())));
    }

    [Fact]
    public void PartialOrInvalidHandoffStopsInsteadOfFallingBack()
    {
        var partial = Env();
        partial[ArcCacheDriver.HashVariable] = null;
        Assert.Throws<InvalidOperationException>(() => ArcCacheDriver.FromEnvironment(From(partial)));
        Assert.Throws<InvalidOperationException>(() => ArcCacheDriver.FromEnvironment(From(Env(512))));
        var otherDir = Env();
        otherDir[ArcCacheDriver.DirVariable] = _dir;
        Assert.Throws<InvalidOperationException>(() => ArcCacheDriver.FromEnvironment(From(otherDir)));
        var otherDll = Env();
        otherDll[ArcCacheDriver.NameVariable] = "d3d12.dll";
        Assert.Throws<InvalidOperationException>(() => ArcCacheDriver.FromEnvironment(From(otherDll)));
    }

    [Fact]
    public void CheckRejectsAnotherGameAChangedCopyAndRedirectionOff()
    {
        var h = ArcCacheDriver.FromEnvironment(From(Env()))!;
        Assert.Throws<InvalidOperationException>(() => h.Check(new Game("steam:2", "Other", Store.Steam, _dir, Path.Combine(_dir, "Other.exe")), devOverride: true));
        Assert.Throws<InvalidOperationException>(() => h.Check(TheGame(), devOverride: false));
        File.WriteAllText(_dll, "changed");
        Assert.Throws<InvalidOperationException>(() => h.Check(TheGame(), devOverride: true));
    }
}
