# Arc Cache standard preview

Download the matching Windows x64 ZIP from this repository's [Releases](https://github.com/nyannoying1337/SCSKiller/releases). Extract the **entire** folder and run `ArcCache.exe`. The unsigned self-contained app does not require Python, PowerShell commands, or a .NET SDK on the player's PC. The declared minimum is Windows 10 build 19041; testing was on Windows 11.

## Choose a version

| Version | Limits | Intended use |
| --- | --- | --- |
| [Standard preview](https://github.com/nyannoying1337/SCSKiller/tree/intel-cache-cap/tools/intel-cache-cap-winui) | Original 512 MiB, 1, 2, 4 GiB | Start here. Only the exact qualified B580 Xe2 driver package is accepted. |
| [Large-cache experimental preview](https://github.com/nyannoying1337/SCSKiller/tree/intel-cache-large-experimental/tools/intel-cache-large-research) | Standard presets plus 8 and 16 GiB | Separate build with a larger reload patch, driver-update review, and expanded overlay. 8/16 GiB gameplay and cache capacity remain unverified. |

Both are previews, not universal driver tools. Neither patches unknown driver versions. No Intel driver DLLs or Windows debug helper DLLs are included in downloads. Do not run both versions at once: they share profiles, receipts and settings. Use the experimental app to roll back its 8/16 GiB images; the standard app rejects those images.

## First game: five steps

1. In Settings, enable **Use private drivers** once, using a manually administrator-launched app, then restart Windows. [Why this is required](PRIVATE-DRIVERS.md).
2. Open the app normally. Select an installed Steam game, launch it in DX12 with the system driver, and leave it at the menu so the app can detect its rendering executable.
3. Close the game. Prepare its unchanged original 512 MiB private copy after reviewing the warning. This uses about 608 MiB per game.
4. Launch DX12 again; keep the app open until it observes the original private copy loading. Close the game after verification.
5. Select a preset and apply it after confirmation. Launch normally through Steam. The app can now be closed unless you want the overlay.

Read and acknowledge multiplayer/anti-cheat risk before applying. Detection is incomplete; a warning is not a guarantee that a game will accept modified driver files or that your account is safe. No anti-cheat is bypassed.

## Recovery

Close the game and choose **Use system driver again**. Do not delete receipts or private folders manually. If verification fails, keep the files and receipt, read Activity, and import only the matching original receipt. Shader caches are not removed by rollback. The WinUI workflow never replaces system driver files and does not require Safe Mode; the older global recovery utility is a separate advanced tool. Use Intel's official installer for a damaged system package.

After a driver update, the experimental app detects the changed active package while open and offers **Check new DLLs**. This is a read-only review, not automatic qualification. Unsupported packages stay blocked; return games to stock until a reviewed definition is available. The standard app has exact-version inspection at startup/refresh, without the experimental automatic update-review workflow. Xe3 is research-only; game patching remains disabled.

## SCSKiller and monitoring

The optional bridge uses a separately installed, matching SCSKiller CLI chosen in Settings. These app-only downloads do not include the CLI. Community access depends on your existing SCSKiller account/settings. Compilation uses the **stock** driver; private-cap compilation and simultaneous recorder compatibility are unresolved. Do not interpret shared Intel cache growth as proven per-game shader growth. The external overlay uses no injection; borderless/windowed display is recommended and exclusive fullscreen is unverified.

## What has actually been checked

The user confirmed Witcher 3 gameplay with a private copy and Ready or Not menu loading with repaired original, 1 GiB and 4 GiB copies. Loaded paths/disk hashes and unchanged system-driver hashes were observed. The Ready or Not repair handles a known 32-bit bundled debug helper with exact qualified 64-bit helpers copied from the player's system; changing a cap alone does not migrate older setups.

2/4 GiB constructors, patch hashes, tamper rejection, receipts/recovery, semantic ACL equality, consent gates, Steam fixtures, and quiet UI polling have automated checks. Actual capacity/growth effects remain unmeasured. 8/16 GiB passed isolated B580 D3D12 device/pipeline creation probes and reload-wrapper/unwind tests, **not** large-cache gameplay/reload. Very large files can require roughly their file size in contiguous RAM: the larger patch does not implement a low-memory Intel parser.

For source build/test instructions see [the app README](../README.md). Proprietary DLL test inputs are optional local files, not shipped fixtures.

## Troubleshooting logs

Settings → Troubleshooting offers opt-in detailed local logging and Open logs folder. `--debug` enables session-only diagnostics. See [troubleshooting](TROUBLESHOOTING.md) for privacy, rotation and report instructions. No automatic upload. Normal discovery/monitoring uses the selected game, not a Witcher-only restriction.
