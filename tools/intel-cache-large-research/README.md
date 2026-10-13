# Arc Cache: larger-cap experimental build

Branch `intel-cache-large-experimental`. The standard `intel-cache-cap` version is published separately. This branch now includes the native WinUI app and safety worker source under the adjacent `intel-cache-cap-winui` / `intel-cache-cap-gui` folders. No unrelated CLI or proxy changes were carried over.

[Quick start](../intel-cache-cap-winui/docs/QUICKSTART.md) · [Private routing explained](../intel-cache-cap-winui/docs/PRIVATE-DRIVERS.md) · [Downloads](https://github.com/nyannoying1337/SCSKiller/releases)

## Build the app

Install .NET 10 SDK and the Windows SDK, then run from the repository root:

```
dotnet publish tools/intel-cache-cap-winui/App/ArcCache.WinUI.csproj -c Release -p:Platform=x64 -o out/ArcCache-Large-Experimental
```

The app compiles the x64 safety worker using the installed .NET Framework compiler. Windows App SDK and .NET runtime are published self-contained. The executable is `ArcCache.Experimental.exe`. No Intel or Windows driver DLL is redistributed: a matching installed package is copied locally only after explicit setup consent.

## Try the experimental build

1. Close the game and the older Arc Cache app. Open `ArcCache.Experimental.exe`.
2. Select a game with a working private setup. Choose 8 GiB first and apply after reading the separate experimental consent. If starting fresh, prepare/test the unchanged original copy before applying a limit.
3. Launch DX12 in single-player/offline mode. Click Show live overlay; use borderless/windowed display.
4. Check the loaded driver with the app. The overlay shows observed private/system path, qualified disk cap, private committed process memory, resident memory and shared Intel cache size/net growth. Module path/disk bytes do not prove the in-memory patch or shader capacity. Shared cache growth is not game-attributed.
5. Close the game before reverting. Select 4 GiB in THIS experimental app, or Use system driver again. The normal app does not recognize the new experimental images and will fail closed.

Stop the game if memory climbs rapidly or it hangs. Keep the app open or minimized while displaying the overlay; closing the app stops its owned overlay. Exclusive fullscreen/anti-cheat can prevent a desktop overlay from appearing. There is no game-process injection, keyboard hook, elevation, system driver replacement or enforcement bypass.

## What changed in the Intel copy

Only exact original Xe2 driver 32.0.101.9034 is accepted, SHA-256 `1A552E471AE986A52D045C4112E1980FB7171AABEBFF93C027A4BBA10A3C6229`.

8/16 GiB raise the constructor's cap AND separate aggregate append budget. They redirect only the full-cache reload call at RVA 0x17B620 to a new 87-byte assembly loop in an appended PE section. The loop calls the original Intel read helper for at most 256 MiB at a time; the original helper still owns asynchronous completion, offset updates and error behavior. MASM-generated unwind metadata is included; the original exception table is preserved verbatim in a copied table with one additional sorted entry. Windows loader mapping and function lookup were tested without executing the driver.

CRITICAL LIMITATION: this patch preserves Intel's contiguous allocation and parser ownership. Loading a file that reaches 8/16 GiB can request and zero roughly 8/16 GiB of RAM, in addition to game memory and other live buffers. Raising a preset does not allocate the whole limit immediately. The separate 1 MiB bounded-reader prototype is NOT substituted for Intel's parser. Both private copies passed an isolated B580 D3D12 device + trivial compute-pipeline creation probe. No real shader cache growth, large-file shader reload or gameplay above 4 GiB is verified.

Each large-cap change stores the previous/next DLL, original ACL and previous receipt under `%LOCALAPPDATA%/IntelArcCacheCap/large-cache-backups`. Output hashes, exact normalized source image, new code, unwind table, copied exception data, headers and checksum are validated. Changed images or unknown drivers are rejected. The app preserves Windows enforcement; a refusal to load is a failed experiment, not a reason to disable enforcement.

## Native and patch tests

`build-reader-tests.cmd`: real sparse-file bounded-read test (13 checks). Reader buffer 1 MiB, separate expected-data buffer 1 MiB, 8 GiB + 4096 bytes verified; fixture occupies 256 KiB and is deleted.

`build-large-tests.cmd [path-to-qualified-original-DLL]`: MASM wrapper tests, including real Windows exception unwinding. With the optional qualified original, also tests exact patch recognition, tamper rejection, byte-identical rollback, Windows PE mapping/function lookup and deterministic cap changes. Test images stay in ignored bin; they are not installed.

See `LargerCapsResearch.md` for the original tracing evidence. Fixed instruction addresses and hashes apply only to this qualified image.

## Driver updates and GPU families

The app checks active, started display devices through SetupAPI/Configuration Manager, then reads their actual driver registry keys. It does not pick a stale DriverStore folder by filename or modification date. While open, a 30-second metadata timer and window activation compare the driver version, registered package path and DLL/dependency file metadata. No driver bytes are hashed on those idle checks; a measured metadata request took about 109 ms on the test PC. There is no service or hidden installer.

When identity changes, the app retains the saved private setup but disables applying/launching it until review. Stock recovery remains enabled. Use Check new DLLs to perform a read-only current-package review: SHA-256, offline embedded-signature status, executable-section constructor candidates and exact dependency hashes. The last qualified review and explicit compatibility reports are stored under the app's LocalAppData directory. An installer changing the package during review causes rejection/retry. A matching signature on an unknown image never enables patching.

Unknown versions still need a reviewed patch definition. This is not automatic reverse engineering or automatic download of new patch offsets. If an updated driver is unsupported, Use system driver again removes the recorded private set and preserves shader caches. Switching an older private copy between limits does not make it compatible with a newer kernel driver.

The current package also contains igd12um64xe3.dll (SHA-256 029E091F5B67A7418B39900E2AC4EAC9EF64767595E0138FF6569CC42FEB4911). Its cache constructor is independently at raw 0x1900E0 / RVA 0x190CE0, not the Xe2 location. Isolated 2/4 GiB constructor tests pass and preserve the neighboring 4 GiB field and unrelated state; the external helper is stubbed exactly as in the earlier constructor probe. Xe3 game patching remains disabled because there is no matching Xe3 GPU on this machine and its complete private dependency set/runtime loading are not qualified. The B580 is Xe2. Dedicated vs integrated status alone does not choose a renderer: use the architecture and actually observed loaded driver. XeH is inspected and remains unqualified.

Xe3 test artifacts are research-only and are not included as driver files in the distributed app. See Xe3ConstructorSamples.cs / Xe3ConstructorProbe.cpp and DriverCatalogTests.cs for reproduction. Do not replace B580's Xe2 DLL with Xe3.

Run `build-driver-review-tests.cmd <qualified Xe2 original> <qualified Xe3 original>` after building the app to reproduce catalogue guards and the isolated Xe3 constructor tests. No driver or game installation is performed by that script.
