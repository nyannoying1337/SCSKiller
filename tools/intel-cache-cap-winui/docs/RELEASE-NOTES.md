## Download

Download `ArcCache-Experimental-2026.10.13-Windows-x64.zip`, extract the complete folder, and run `ArcCache.Experimental.exe`. This is an unsigned Windows x64 preview; all app/.NET/WinUI files are included. No Python, terminal commands or .NET SDK are needed to run it. The SCSKiller CLI is optional and is **not included** in this app-only ZIP.

## This version

Standard presets plus experimental 8/16 GiB, active-driver update detection/read-only review, and a broader external overlay. Only the exact qualified Intel Arc B580 Xe2 32.0.101.9034 package is supported. Unknown versions remain blocked even if a byte pattern matches. Xe3 is inspected/research-tested, but game patching remains disabled.

The routing setting now says **Use private drivers** and explains when it is required. On/off/restart-required statuses are distinct. A saved limit stays visible when routing is inactive; private launch is blocked until ready. One-game rollback is separate from restoring the machine-wide setting.

## Start here

1. Read [Quick start](https://github.com/nyannoying1337/SCSKiller/blob/intel-cache-large-experimental/tools/intel-cache-cap-winui/docs/QUICKSTART.md).
2. Read [Private drivers explained](https://github.com/nyannoying1337/SCSKiller/blob/intel-cache-large-experimental/tools/intel-cache-cap-winui/docs/PRIVATE-DRIVERS.md). Enable once with explicit manual administrator launch, confirm, and restart Windows. Other apps with `.local` setups can also be affected. The installed system driver and integrity enforcement remain unchanged.
3. Select a Steam game, detect its DX12 process, prepare an unchanged private copy, verify its loading, then apply a preset with the game closed. Risk warnings require acknowledgment; anti-cheat is never disabled or bypassed.

## Evidence and limitations

User tests confirmed Witcher 3 gameplay and repaired Ready or Not menu loading at original 512 MiB, 1 GiB and 4 GiB. Paths/disk hashes were observed; cache-capacity/growth effects remain **unmeasured**. 8/16 GiB passed isolated B580 device/pipeline probes and wrapper/unwind checks, not large-cache gameplay/reload. A large cache can consume roughly its file size in contiguous RAM; the patch does not implement a low-memory Intel parser.

Current source builds pass 27 core/worker/bridge checks, 27 larger-patch/native-wrapper checks, 10 catalogue checks and 5 isolated Xe3 constructor checks. Both actual routing screens were rendered and inspected. Read-only checks did not install new game or system patches.

The optional SCSKiller bridge remains **stock-driver-only**; private-cap compilation and simultaneous recorder compatibility are unresolved. Community access needs the existing account/settings and a CLI with `--community` support (the standard branch includes its source). The overlay is external, without injection, and reports shared cache size rather than proven game-specific growth; exclusive fullscreen is unverified. The app can be closed after setup unless using the overlay.

## Roll back

Close the game and choose **Use system driver again**. Files/receipt must verify; extra or altered files stop removal. Keep receipts/backups. Shader caches are preserved. Use the experimental app to roll back 8/16 GiB; standard rejects those images. Do not run both apps together because they share recovery data. No proprietary Intel or Windows debug-helper DLLs are distributed.

[Source and build instructions](https://github.com/nyannoying1337/SCSKiller/tree/intel-cache-large-experimental/tools/intel-cache-cap-winui) · [Experimental research/build notes](https://github.com/nyannoying1337/SCSKiller/tree/intel-cache-large-experimental/tools/intel-cache-large-research).

## Troubleshooting logs

Settings → Troubleshooting offers opt-in detailed local logging and Open logs folder. `--debug` enables session-only diagnostics. See [troubleshooting](TROUBLESHOOTING.md) for privacy, rotation and report instructions. No automatic upload. Normal discovery/monitoring uses the selected game, not a Witcher-only restriction.
