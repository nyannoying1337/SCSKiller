# Arc Cache — standard preview

**Start with [the quick start](docs/QUICKSTART.md).** Read [Use private drivers explained](docs/PRIVATE-DRIVERS.md) before enabling the Windows setting. Download both variants from [GitHub Releases](https://github.com/nyannoying1337/SCSKiller/releases).

This branch builds `ArcCache.exe`. It provides original 512 MiB and reviewed 1/2/4 GiB presets; larger presets live on the separate intel-cache-large-experimental branch. These are unsigned previews. Capacity effects are unmeasured; Ready or Not menu loading was confirmed with the repaired original, 1 GiB and 4 GiB setups. Unknown drivers stay blocked. App-only releases use a separately installed SCSKiller CLI.

The detailed notes below include earlier investigation results. The quick start and version-specific research notes describe the current supported workflow.


A separate, unpackaged x64 WinUI 3 application for verified Intel Arc private-driver profiles. The existing SCSKiller app and unrelated functionality are not changed. Installed Steam titles are discovered locally across Steam libraries; no API key, sign-in or network image download is required. Artwork uses Steam's local cache.

## Implemented workflow

1. **Your games:** search installed Steam titles, select a game, inspect its private setup and launch it through Steam. Games are identified automatically from their running DX12 process and original Intel driver. New titles need no compiled app entry; all new private setups are experimental. No generic multiplayer/anti-cheat guarantee is made.
2. **Set up original private copy:** after explicit confirmation, copy the 27 hash-pinned original driver files (about 608 MiB). The game must be closed, the system B580 driver must be original, the automatically observed game executable hash must match, and existing local D3D12 proxies must be disabled by the user. Unknown driver packages are refused; changed game builds require automatic rediscovery.
3. Launch the original private control in DX12; its loaded driver path and disk hash are checked automatically. **Apply selected limit** offers 1 GiB (gameplay tested), 2 GiB and 4 GiB (experimental constructor patches). 512 MiB restores original private bytes. Every change requires confirmation; leaving the original control requires its automatically observed loading result. No gameplay-test checkbox is required. Wide patches are checked by isolated native execution and full-image hashes. Ready or Not menu loading was confirmed at 4 GiB; the cache-growth effect remains unmeasured.
4. **Check loaded driver:** native, read-only module enumeration reports paths and disk hashes. **Return game to stock:** validate the entire receipt and remove only the owned private files. Altered or extra contents stop removal. Import accepts a schema-3 matched-set receipt for that exact game install, and never overwrites an existing record.

**Driver** verifies the currently registered B580 package. The WinUI tool does not replace global driver files. The earlier WinForms recovery utility remains separate. **Activity** records actions/results in `%LOCALAPPDATA%\IntelArcCacheCap\logs`. Per-game recovery records are in `%LOCALAPPDATA%\IntelArcCacheCap\games`.

**Settings** exposes the machine-wide Windows developer-routing override. Changes require manually running the app as administrator, explicit confirmation, a saved previous-value receipt and a restart. No automatic elevation, injection, boot-setting changes, integrity-enforcement bypass, Python or PowerShell subprocesses are used. The safety engine is a compiled C# .NET Framework 4.8 executable, built from the already-tested native engine sources. Game/system mutations require an explicit confirmed request. Local discovery profiles, loading-observation status and logs are written automatically. The WinUI app runs asInvoker and the engine inherits that privilege level.

## Automatic game discovery



Selecting a Steam game checks a locally saved profile or searches running processes under that exact install directory. A unique AMD64 PE32+ executable must have both D3D12 and the original, currently registered hash-pinned Intel Xe2 module loaded. Existing private folders or local D3D12 proxies are not guessed around. The original 27-file driver cohort is checked before recording a new observation. The app stores the executable path/hash, app ID, install root, timestamp and original-driver hash under `%LOCALAPPDATA%\IntelArcCacheCap\profiles`. Unknown or ambiguous processes remain pending; the selected game is retried every four seconds. Discovery never changes game files, installs a proxy or patches a driver.



The existing Witcher record is grandfathered for recovery. New titles and updated game builds need no app source change. If a game updates while a private set exists, recovery stays available: return it to stock and launch DX12 to rediscover. New receipts embed discovery metadata, allowing recovery/import even if the profile cache is lost. Moved installations, inaccessible processes, multiple rendering executables, DX11 and non-Xe2 driver routes are not silently guessed.



After explicit preparation, the app observes the original 512 MiB private control automatically. It requires the exact rendering process to load the expected private Xe2 path and original disk hash, validates the full cohort, and records `original-control-loading-verified`. Prepare/Change remain explicit and closed-game actions; the worker rejects a change from original to patched before this observation. Loaded-path/disk-hash observations do not prove gameplay, in-memory bytes or capacity. No user must manually check a hash or rebuild the app per title.



Generic discovery was exercised against the running Ready or Not DX12 process on the test PC. Its shipping executable and original Xe2 path/hash were recorded automatically, and Steam declared multiplayer/co-op. That initial discovery test did not install a set. Later user tests confirmed repaired private-copy loading at original 512 MiB, 1 GiB and 4 GiB. Automatic discovery/loading checks still do not prove shader capacity.



## Tested and unverified

The matched private set was observed loading in Witcher 3 on the test PC with Intel B580 driver 32.0.101.9034. The known patched private Xe2 path/hash and unchanged original global Xe2 hash were verified, and the user confirmed gameplay. Native module observation simultaneously saw another process loading the original global DLL. The cache-growth effect was not measured; application-specific settings can override the default.

The WinUI app's library scan, package status, existing receipt validation, compiled worker confirmation gates and read-only visual preview are tested. The fresh WinUI prepare/change/remove buttons use the same engine, but have not been clicked against the real game during this UI migration. These are experimental controls, not a claim that every lifecycle or every Steam game is supported. Interrupted preparation can retain staging; interrupted replacement/removal can require manual recovery inspection. Keep receipts and original backups. Proprietary Intel DLLs are not shipped in either app or source archives.

## Multiplayer and anti-cheat warnings

The WinUI app scans entries under the selected install and retrieves current English Steam store categories over HTTPS. Anti-cheat indicators, Steam multiplayer/co-op or VAC declarations, and unavailable/incomplete checks produce a prominent HIGH RISK warning. Applying remains possible after a separate explicit acknowledgment of possible launch failures and account penalties. DX12 rendering executables are discovered at runtime and locally hash-pinned. This risk override does not enable unknown driver versions or skip executable/update checks. Store single-player labels and filename indicators are incomplete evidence, not guarantees.

The scan uses an 88-name snapshot of SCSKiller's detector in `src/SCSKiller.Core/Games/GameFiles.cs`. Hidden entries count. Links, unreadable entries, missing roots, two-million-entry limits and a ten-second budget produce an incomplete verdict requiring risk acknowledgment. A marker stops scanning and requires acknowledgment. Tests assert the marker snapshot matches SCSKiller's rules. No anti-cheat is disabled or bypassed.

The worker repeats the assessment before Prepare/Change and requires `RiskConfirmed` when it cannot recommend applying, independently of ordinary `Confirmed`. The shared engine retains exact executable/package hashes, proxy-conflict detection, closed-game checks and receipt validation. Risk consent never overrides these technical checks. Removing a verified set stays available offline. Launching a configured private set with a warning also asks for explicit risk acknowledgment. External launchers are not controlled. The older WinForms private-set interface has no Steam warning dialog; prefer this WinUI companion.

## Experimental 2 / 4 GiB encodings

The original `mov qword [rcx+0x10], imm32` sign-extends its immediate, so 0x80000000 is negative. The wide patches replace only the existing 24 bytes at raw offset 0x18BC5C / RVA 0x18C85C. They load 0x100000000 into RAX, preserve its store at `[rcx+8]`, then store either RAX or RAX shifted right by one into `[rcx+0x10]`. They preserve the zero store, prologue, calls, unwind layout and return path. RAX is not a helper argument and is overwritten after the helper call; the following ADD replaces shift flags before that call. No code caves, trampolines, size changes or integrity-enforcement bypass are used.

Only the pinned original 32.0.101.9034 image is normalized/accepted. Full-image SHA-256 pins are `596DEEA6EB0467C0E1FE7C92F8CB83049CE9BF38E82758C84FE6B2C2A31DED12` (2 GiB) and `9B43A3F28C4A2ED28BB70BC16845C03D8EC919811B955854D56DB1884C92F85C` (4 GiB). Engine tests cover recognition, tampering and conversion back to the exact original/1 GiB hashes. `NativeTests/CapConstructorProbe.cpp` executes copies of the reviewed constructor with the external helper stubbed identically in every sample: cap values, neighboring 4 GiB field, return value and all other buffer bytes match expectations. This verifies initialization, not downstream arithmetic, eviction behavior, actual capacity or game loading. The meaning of the separate 4 GiB field is not established; larger values are not offered. Keep the current 1 GiB setup until choosing an experimental test in the app.

## SCSKiller compiler / community connection

The game page offers status and compile actions through a selected SCSKiller console CLI. The companion package includes a matching CLI and native warmer under `SCSKiller/`; it is selected automatically on first run. Settings can choose another matching CLI build. The desktop GUI executable is rejected by its PE subsystem. No arbitrary commands, recorder installation, shell, elevation, affirmative stdin responses or settings changes are exposed. Child output streams to Activity; Stop terminates only the owned CLI/process tree. Cancellation does not restore cache entries already changed.

The focused CLI `--community` option attaches the existing Account/Community services only when requested by status/compile. Existing DPAPI-protected sign-in and `UseCommunityDb` setting are used. It never signs in or attaches Sharing/recorder-management services. Without sign-in/access, local compilation remains available; no community recording is promised. Downloads are hash-only pipeline recordings matched to the game build and rehydrated from installed game data, not portable precompiled Intel driver cache blobs. Normal CLI commands without the new option retain their previous behavior. The connection enables experimental Intel support only in the child environment.

Compilation currently loads the stock system driver and its 512 MiB default, regardless of the game's private limit. It may evict existing cache entries. The UI warns before invoking it and refuses compilation while the selected detected game process is running; SCSKiller also retains its own game/anti-cheat/compatibility checks. Cap-aware replay and simultaneous private-driver recording remain unresolved, as described below. Bridge command/lifecycle fixtures, CLI build/help, native warmer build and real Witcher status were tested. The real status recognized 865 recorded and 69,450 generated pipelines and an existing warmed plan; no new real compilation or authenticated community download was verified in this session.

## SCSKiller compatibility investigation

The current private-driver `.local` setup is not compatible with the existing `d3d12.dll` recorder workflow. An isolated loader test on the test PC returned the app-local proxy for both an absolute System32 `d3d12.dll` load and a `LOAD_LIBRARY_SEARCH_SYSTEM32` load. This confirms a self-forwarding route in `proxy.cpp`. A focused guard now refuses `g_real == self` instead of allowing recursive calls. It was compiled and tested: normal proxy loading succeeded; redirected loading failed cleanly with Win32 error 1114. The existing `selftest sysdxgi` check also passed. The guard is not a compatibility fix and has not been installed in Witcher.

A second isolated experiment copied the unmodified Microsoft-signed D3D12 runtime under a distinct basename. It loaded, but B580 device creation returned `D3D12_ERROR_INVALID_REDIST` (`0x887E0003`). A copied experimental proxy using that alias had the same failure. This route is not shipped or recommended, and no Windows validation was bypassed. The live Witcher proxy and private driver files were untouched.

Cap-aware warm/replay staging still needs work: SCSKiller's Intel backend reports 512 MiB and its staged warmer uses the game executable name with the stock driver. The compiler/community connection above is implemented, but does not resolve private-driver staging or recorder compatibility. No automatic recorder installation or cap override was added to SCSKiller.

## Read-only cache overlay

With Witcher DX12 running, **Show cache monitor** opens an external click-through C# Windows Forms HUD from the compiled safety engine. The main app remains WinUI 3. It samples file lengths every two seconds, displays only while the exact selected game process is foreground, and stops on game exit or when the app closes. Use **Stop cache monitor** to stop manually. Use windowed/borderless presentation; exclusive fullscreen display is not verified. No game injection or graphics hooks are used.

The HUD measures the **shared Intel cache folder** under `%USERPROFILE%\AppData\LocalLow\Intel\ShaderCache`, not a proven per-game subset. It explicitly labels attribution as unverified. Growth is signed net disk size change since the monitor started; evictions can make it negative. Changed files counts added, removed or size-changed files relative to that baseline. It is neither a shader compilation counter nor proof of the cap's effect. Missing/inaccessible files show read failures. Subdirectories and reparse targets are not scanned. CSV samples go to the existing app log folder.

On the test PC, an Intel cache candidate grew by 5,215 bytes to approximately 13.1 MiB, but no further growth occurred in the subsequent 30-second/area-change snapshots. Read-only file-user and mapped-file observations identified Witcher's DirectX cache session; they did not attribute the Intel file conclusively. The overlay does not represent this candidate as verified Witcher data. Six isolated measurement tests cover idle state, growth, new files, eviction, fixed baselines and a missing folder. Run `MonitorTests\build.cmd` on Windows. The normal import workflow was also observed recognizing the existing 1 GiB set after import.

## Build instructions

Requires Windows, .NET 10 SDK, Windows SDK 10.0.26100 tooling (NuGet restores other WinUI dependencies), and Windows' .NET Framework 4.8 compiler. Uses repository-matched WinUI 2.3.9 / Windows App Runtime 2.5.1 component packages. InteractiveExperiences is explicitly pinned to available 2.1.9 to resolve the WinUI package's missing 2.1.8 dependency.

```
dotnet publish App/ArcCache.WinUI.csproj -c Release -p:Platform=x64 -o publish
dotnet run --project Tests/ArcCache.Tests.csproj -c Release -- publish/ArcCache.SafetyEngine.exe <optional-matched-receipt>
```

Ship the complete published folder, not its EXE alone: it includes .NET and native WinUI runtime files. Windows 10 build 19041 or later is the declared minimum. Development binaries are unsigned. The build compiles the sibling `intel-cache-cap-gui` engine sources without modifying or invoking unrelated SCSKiller code.

`build-companion.ps1 -Output <fresh-directory>` additionally builds and packages the matching CLI, native warmer and pinned codecs. It requires x64 MSVC/CMake and the .NET SDK, accepts `-Dotnet` / `-Cmake` paths, and refuses to replace existing output. This is a developer packaging script; the product never launches PowerShell. Preserve the complete folder hierarchy. An App-only publish requires choosing an existing CLI build with `--community` support in Settings.

`ArcCache.exe --preview <absolute-output.png>` renders the real library window off-screen with read-only status/receipt checks, saves a PNG, then exits. It performs no setup or driver mutation.

Microsoft references: [unpackaged WinUI apps](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app), [DLL redirection](https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-redirection), [UMD loading](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/loading-a-user-mode-display-driver).

## Installation and loading states

The header reports the verified installed limit. Running-game observations have their own panel; unmeasured cache capacity never means installation failed. The three steps are detection, original-copy loading, and applying a limit. Desired limits survive original preparation during the session. Reapplying an installed limit is disabled. Technical paths and receipt imports live in recovery options. Consent is requested in the action dialog.

Stock-driver compilation is disabled for patched private profiles until custom-driver warm staging is implemented. This change does not implement cap-aware compilation. Refresh setup validates the current disk receipt without changing game files.

## Qualified debug-helper routing fix

Ready or Not's AMD64 executable ships an I386 dbghelp.dll (6.9.0003.113). Recent crash contexts report delayed-load exception 0xC06D007E. The executable's stub at return RVA 0x62FD335 passes delay-import descriptor RVA 0x88C00C4 for dbghelp.dll. This identifies the failed helper load, but does not establish the cause of all RAM growth.

An isolated x64 probe with the same bundled helper and exe.local directory reproduced Win32 193 for both absolute System32 loading and LOAD_LIBRARY_SEARCH_SYSTEM32. Adding the original AMD64 Windows 10.0.26100.9549 dbghelp.dll and dbgcore.dll into exe.local made both loads succeed from that folder. Source Authenticode signatures were verified as valid Microsoft Windows signatures during qualification.

New preparation detects this helper by architecture and exact SHA-256, independently of game title. The two Windows helper sources are accepted only at their pinned full hashes, copied from local System32, and recorded alongside the 27 Intel files in schema 4. Schema 3 remains supported. Game-folder helpers are preserved; rollback validates and removes only the 29 recorded private files. No debug DLLs are redistributed in the companion package. Different Windows helper versions or unknown x86 helpers are refused. Existing setups must be removed and freshly prepared with this build to obtain the fix. The user subsequently confirmed menu launch with fresh schema-4 original 512 MiB, 1 GiB and 4 GiB setups. Capacity and eviction behavior remain unmeasured.

### Runtime follow-up (2026-10-13)

The first retest used a legacy schema-3 setup changed from 2 to 4 GiB, with no debug helpers, and RAM reached about 28 GiB. The fresh schema-4 512 MiB control subsequently reached the menu; private committed memory stabilized near 4.8 GiB in the sampled window, compared with approximately 4.8 GiB on stock. The user confirmed menu launch at 1 GiB and 4 GiB next. The 4 GiB private Xe2 disk hash was 9B43A3F28C4A2ED28BB70BC16845C03D8EC919811B955854D56DB1884C92F85C, while the system retained its original hash. The game had closed before the final patched loaded-module query, so that query does not prove patched runtime module loading. No cache growth beyond 512 MiB was measured.

The app now detects legacy setups missing the qualified helpers and labels them as requiring a compatibility update while retaining their installed-limit information. Cap changes and control verification refuse such setups; launch is disabled until the user removes and freshly prepares them. Removal remains available. Three read-only native guard checks verified known helper detection, schema-3 rejection, and qualified schema-4/source acceptance. Twenty app checks pass, including preserving verified installation state while blocking an incompatible setup.
