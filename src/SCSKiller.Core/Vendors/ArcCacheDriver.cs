using System.Security.Cryptography;
using Microsoft.Win32;

namespace SCSKiller.Core.Vendors;

/// <summary>A private Intel driver copy handed over by Arc Cache for one compile (Intel only). Arc Cache puts a patched copy
/// of the driver package in the game's <c>&lt;exe&gt;.local</c> folder, so the game runs with a larger per-game shader cache
/// than the stock 512 MiB (<see cref="IntelBackend.PerGameCap"/>). A warm that ran on the stock driver would fill the same
/// cache file (keyed on the exe name) under the stock cap and evict everything past 512 MiB, so the staged warm exe must
/// load the very same copy: scskiller_warm links <c>&lt;stage&gt;\&lt;exe&gt;.local</c> to the game's folder
/// (<c>--driver-local</c>) and its child refuses to compile unless the driver it loaded is that copy, by path and SHA-256.
///
/// Set only by Arc Cache, for the child process it starts (never persistent): <see cref="DirVariable"/> (the game's
/// .local folder), <see cref="ExeVariable"/> (the game's exe), <see cref="MiBVariable"/>, <see cref="NameVariable"/>
/// (igd12um64xe2/xe3/xeh.dll) and <see cref="HashVariable"/>. All or none; a partial or invalid set stops the compile
/// instead of falling back to the stock driver.</summary>
public sealed record ArcCacheDriver(string GameExe, string LocalDir, int CacheMiB, string DriverName, string DriverSha256)
{
    public const string DirVariable = "SCSKILLER_ARCCACHE_PRIVATE_DRIVER_DIR";
    public const string ExeVariable = "SCSKILLER_ARCCACHE_GAME_EXE";
    public const string MiBVariable = "SCSKILLER_ARCCACHE_CACHE_MIB";
    public const string NameVariable = "SCSKILLER_ARCCACHE_DRIVER_NAME";
    public const string HashVariable = "SCSKILLER_ARCCACHE_DRIVER_SHA256";
    static readonly string[] Variables = [DirVariable, ExeVariable, MiBVariable, NameVariable, HashVariable];
    static readonly string[] DriverNames = ["igd12um64xe2.dll", "igd12um64xe3.dll", "igd12um64xeh.dll"];
    static readonly int[] Sizes = [1024, 2048, 4096, 8192, 16384];

    /// <summary>The handoff in this process's environment; null when none of its variables is set.</summary>
    /// <exception cref="InvalidOperationException">Some are set but the set is incomplete or invalid.</exception>
    public static ArcCacheDriver? FromEnvironment(Func<string, string?>? env = null)
    {
        env ??= Environment.GetEnvironmentVariable;
        var v = Variables.Select(n => env(n)).ToArray();
        if (v.All(string.IsNullOrEmpty)) return null;
        if (v.Any(string.IsNullOrEmpty))
            throw new InvalidOperationException("Arc Cache's private driver handoff is incomplete; nothing was compiled.");
        if (!int.TryParse(v[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var mib) || !Sizes.Contains(mib))
            throw new InvalidOperationException($"Arc Cache handed over an unknown cache size ({v[2]} MiB); nothing was compiled.");
        if (!DriverNames.Contains(v[3], StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Arc Cache handed over an unknown driver file ({v[3]}); nothing was compiled.");
        if (v[4]!.Length != 64 || !v[4]!.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Arc Cache handed over an invalid driver hash; nothing was compiled.");
        string exe = Path.GetFullPath(v[1]!), dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(v[0]!));
        if (!dir.Equals(exe + ".local", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Arc Cache's private driver folder isn't the game's .local folder; nothing was compiled.");
        return new ArcCacheDriver(exe, dir, mib, v[3]!.ToLowerInvariant(), v[4]!.ToUpperInvariant());
    }

    /// <summary>The per-game cap a complete handoff asks for, without checking the files (for notes); null without one.</summary>
    public static long? RequestedCap(Func<string, string?>? env = null)
    {
        env ??= Environment.GetEnvironmentVariable;
        if (Variables.Any(n => string.IsNullOrEmpty(env(n)))) return null;
        return int.TryParse(env(MiBVariable), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var mib)
               && Sizes.Contains(mib) ? (long)mib << 20 : null;
    }

    /// <summary>Checks, right before a warm of <paramref name="game"/> starts, that this handoff is for that game and that
    /// Windows will use the copy: the exe is the game's, the folder is a real folder (not a link) holding the driver with
    /// the expected hash, and DLL redirection (DevOverrideEnable) is on. Throws with the reason otherwise.</summary>
    public void Check(Game game, bool? devOverride = null)
    {
        if (!Path.GetFullPath(game.ExePath).Equals(GameExe, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Arc Cache handed over the private driver of {Path.GetFileName(GameExe)}, not of {game.Name}; nothing was compiled.");
        var info = new DirectoryInfo(LocalDir);
        if (!info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException($"{game.Name}'s private driver folder is missing or is a link; nothing was compiled.");
        var dll = Path.Combine(LocalDir, DriverName);
        if (!File.Exists(dll) || File.GetAttributes(dll).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException($"{game.Name}'s private driver copy has no {DriverName}; nothing was compiled.");
        if (!Sha256(dll).Equals(DriverSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{game.Name}'s private {DriverName} changed since Arc Cache checked it; nothing was compiled.");
        if (!(devOverride ?? DevOverrideEnabled()))
            throw new InvalidOperationException("Windows' private-driver setting (DevOverrideEnable) is off or needs a restart, so the compile would use the normal 512 MB driver; nothing was compiled.");
    }

    public static string Sha256(string path)
    {
        using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(s));
    }

    /// <summary>HKLM\...\Image File Execution Options DevOverrideEnable = 1: Windows honours an exe's .local folder.
    /// The setting takes effect after a restart; the child's loaded-driver check catches a pending one.</summary>
    static bool DevOverrideEnabled()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options");
            return k?.GetValue("DevOverrideEnable") is int i && i == 1;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }

    /// <summary>scskiller_warm's arguments for this handoff.</summary>
    public string[] WarmArgs() =>
        ["--driver-local", LocalDir, "--driver-name", DriverName, "--driver-sha256", DriverSha256, "--driver-mib", CacheMiB.ToString(System.Globalization.CultureInfo.InvariantCulture)];
}
