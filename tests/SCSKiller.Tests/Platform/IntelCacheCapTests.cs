using SCSKiller.Core;
using SCSKiller.Core.App;
using SCSKiller.Core.Vendors;

namespace SCSKiller.Tests.Platform;

/// <summary>The Intel per-game 512 MiB cache cap (measured on an Arc B580, ARCHITECTURE.md): the app reports that a game
/// over it keeps only part of its compile, instead of a plain "Warmed".</summary>
public class IntelCacheCapTests
{
    const long Cap = 512L << 20;
    static readonly GpuInfo IntelGpu = new(GpuVendor.Intel, "Intel(R) Arc(TM) B580 Graphics", "32.0.101.9034", 1, 12UL << 30);

    static GameState Warmed(long? estimate, long? onDisk = null) =>
        new GameState(new Game("steam:1", "Palworld", Store.Steam, @"X:\g", @"X:\g\g.exe"), null, AntiCheat.None, GameStatus.Warmed, "",
            null, null, estimate, null, "32.0.101.9034", DateTimeOffset.UtcNow, null, true, null) with { CacheOnDisk = onDisk };

    [Fact]
    public void Only_the_intel_backend_reports_a_per_game_cap()
    {
        Assert.Equal(Cap, new IntelBackend(IntelGpu).PerGameCacheCap);
        Assert.Equal(512L << 20, IntelBackend.PerGameCap);
        Assert.Null(((IGpuVendorBackend)new UnsupportedVendor(IntelGpu)).PerGameCacheCap);
    }

    [Fact]
    public void A_game_over_the_cap_reports_the_share_that_fits()
    {
        var big = Warmed(984L << 20);        // Palworld's estimate: ~984 MB
        Assert.True(ScsKiller.OverPerGameCap(big, Cap));
        Assert.Equal(52, ScsKiller.PerGameCapFitPercent(big, Cap));   // 512 / 984
        var note = ScsKiller.PerGameCapNote(big, Cap);
        Assert.NotNull(note);
        Assert.Contains("512 MB", note);
        Assert.Contains("52%", note);

        // a measured size wins over the estimate
        Assert.True(ScsKiller.OverPerGameCap(Warmed(300L << 20, onDisk: 700L << 20), Cap));
    }

    [Fact]
    public void A_game_that_fits_gets_no_note()
    {
        var small = Warmed(300L << 20);
        Assert.False(ScsKiller.OverPerGameCap(small, Cap));
        Assert.Equal(100, ScsKiller.PerGameCapFitPercent(small, Cap));
        Assert.Null(ScsKiller.PerGameCapNote(small, Cap));
        Assert.Null(ScsKiller.PerGameCapNote(small, null));            // no cap vendor
        Assert.Null(ScsKiller.PerGameCapNote(Warmed(984L << 20), null));
    }

    [Fact]
    public void The_library_row_flags_a_warmed_game_over_the_cap()
    {
        var big = Warmed(984L << 20);
        Assert.Contains("over the 512 MB limit", Format.ShortNote(big, Cap));
        // without a per-game cap the note is unchanged (every other vendor)
        Assert.DoesNotContain("limit", Format.ShortNote(big) ?? "");
        // a fitting game keeps its normal note on Intel
        Assert.DoesNotContain("limit", Format.ShortNote(Warmed(300L << 20), Cap) ?? "");
    }
}
