> Experimental branch: see [larger-cache build and test instructions](../intel-cache-large-research/README.md). The 8/16 GiB variants are unverified in gameplay and preserve whole-file RAM allocation. Use the WinUI experimental executable for larger-cap installation and rollback.

# Intel Arc Cache Cap — C# Windows application

## Matched private-driver experiment: gameplay confirmed

On 2026-10-12, Witcher 3 DX12 loaded the matched private set and the user confirmed gameplay with the known 1 GiB Xe2 patch. Native module observation simultaneously saw Witcher loading the private patched Xe2 path and Edge loading the original system Xe2 path. These are loaded-path observations plus disk hashes, not in-memory hashing. Actual shader-cache growth and the cap's effect have not been measured. Multiplayer and anti-cheat compatibility are not verified.

The earlier ONE-DLL redirection failed: with a working original system driver it produced a DirectX 12 requirements error. Do not use that method. The original-copy control succeeded after including the registered Intel loader and package support files. The matched set currently contains 27 exact hash-pinned files (about 608 MiB) from driver 32.0.101.9034. The set was selected from observed modules, PE imports, package DLL-name strings and compiler helpers; it is not claimed to be the minimal or universally complete dependency closure.

1. Keep the installed system Xe2 DLL original. Use **Prepare matched original set** with the exact original package folder inside DriverStore; save its schema-3 recovery receipt outside the game folder. The supported Witcher executable and every package file must match pinned hashes. Windows developer override must already have been explicitly enabled, followed by a reboot.
2. Test the original private set in gameplay. Close Witcher and use **Change private set cap** with the receipt and cap 1024. Only the private Xe2 file changes. 512 restores the original private bytes; other limits are blocked for this experiment.
3. Use **Observe game modules** while playing. It uses native Toolhelp enumeration and reports inaccessible processes. It must show the private path and known patched disk hash before claiming loading worked.
4. Close Witcher and use **Remove matched set** with its schema-3 receipt to return to stock routing. Every owned file must match; extra or changed files stop removal. Restore the developer setting separately and reboot when ending redirection testing.

SCSKiller's game-local d3d12.dll proxy must be disabled for this workflow. It conflicted with .local routing in the earlier test. The app does not automatically remove mods. The Windows developer-override setting is machine-wide even though these private files are specific to Witcher. The app never disables integrity enforcement, modifies boot settings, injects code, or automatically elevates.

Receipts are durable before file changes. Failed preparation may retain a .pending staging directory for inspection. Interrupted private replacement/removal can require manual inspection of known files; recovery is not guaranteed after filesystem or power failures. Proprietary DLLs are copied locally and are not included in the source or app distribution.


This is a compiled x64 WinForms app using Windows' .NET Framework. It has **no Python dependency, bundled interpreter, PowerShell subprocesses or third-party runtime packages**. The standalone developer executable is unsigned. Windows 10/11 with .NET Framework 4.8 is the target. `asInvoker` prevents automatic elevation; manually use Run as administrator only for an explicitly confirmed Safe Mode operation.

## Workflow

1. Scan packages, select a DLL, and Inspect. Package candidates can be inactive. Observe game modules while the game runs to identify an actual module path.
2. Choose the desired default per-cache-file cap and create a fresh bundle. The bundle includes `before.dll`, `patched.dll`, and `manifest.json` with hashes, owner/group/DACL and the recovery journal.
3. Keep the bundle and an official Intel installer on a local drive. Read the Safe Mode guide. In Safe Mode, manually launch as administrator, open the bundle, check the consent checkbox and confirm the exact target and before/after hashes.
4. Reboot normally and observe modules while the intended game runs. A module path and disk hash do not prove in-memory bytes or effective cache behavior.

Rollback restores the bundle's before image, which may itself be an older cap-only patch. It does not automatically restore the stock cap. Unknown target bytes or changed ACLs block replacement. If access is denied, the app does not take ownership; use the official Intel driver installer for recovery. Offline Windows installation is not implemented.

## Per-game isolation preflight

The **Check Witcher 3 isolation** button locates Steam's Witcher 3 DX12 executable and reads its application manifest using resource-only Windows APIs. It reports the existing `DevOverrideEnable` registry value without modifying it. A manifested executable with the override disabled blocks simple `.local` isolation. An unmanifested executable or an already enabled override is only a candidate: it does not prove Direct3D will use the private Intel driver. No redirection files or game DLL copies are created by the check button. The globally installed original remains a separate prerequisite for keeping normal games on the original driver.

Historical preflight: the manifest required the explicitly enabled developer override. The single-DLL test failed and is superseded by the matched-set experiment above.

### Explicit developer override controls

**Witcher test result:** the test PC already had SCSKiller's local `d3d12.dll` proxy. With the `.local` test active, Witcher crashed in that proxy with `0xC00000FD` (stack overflow). The crash report contained the local proxy but no system D3D12 or Intel UMD entry. The proxy source loads the system `d3d12.dll` by absolute path; Windows documents that `.local` can redirect even absolute paths. Recursive forwarding is therefore the leading explanation, not proven by a captured stack trace. The owned test copy was removed; the existing proxy and game executable were preserved. Current preflight and private-copy setup block **any existing game-local d3d12.dll** until compatibility is established. Do not remove the user's proxy automatically or claim this isolation method works with it.

After user authorization, the native app provides **Enable developer override** and **Restore developer setting**. Manually launch as administrator; the app never elevates itself. Enable records the original value (including absence) at `%LOCALAPPDATA%\IntelArcCacheCap\developer-override.json`, flushes the receipt before writing DWORD `DevOverrideEnable=1`, and checks read-back. Restore uses that receipt and refuses unexpected registry drift. Both actions require an explicit confirmation and a manual reboot. They do not change driver files, boot settings or integrity enforcement.

**Prepare Witcher copy** uses only `witcher3.exe.local\igd12um64xe2.dll`. Choose the supported original DLL and a fresh recovery receipt outside the game folder, then confirm the exact target. Its receipt records the inspected game executable SHA-256, supported DLL hash and cap. Setup refuses unknown game builds, existing redirection paths, existing root Intel DLLs or running Witcher processes. **Remove Witcher test copy** uses the selected receipt, verifies exact supported contents and removes only its one owned DLL and then the empty folder. Unexpected extra files or modified bytes block removal.

The test remains unverified until Witcher's process is observed loading the exact private path. No anti-cheat compatibility guarantee is made. To restore the test entirely, close Witcher, remove the private copy with its receipt, restore the developer setting, and reboot. The original installed Intel driver is a separate explicit Safe Mode operation.

## Why 2047 MiB?

The verified instruction is `mov qword ptr [rcx+0x10], imm32`. Its 32-bit immediate is sign-extended. The largest positive value is 2,147,483,647 bytes, or 2047 MiB plus 1,048,575 bytes. The UI accepts whole MiB, so its maximum is **2047 MiB**. 2048 MiB (`0x80000000`) becomes `0xFFFFFFFF80000000`, not positive 2 GiB.

This is a limit of the original immediate-only edit and this legacy WinForms selector, not proof of a 2 GiB cache engine cap. The shared engine now also recognizes pinned experimental 2/4 GiB constructor encodings, exposed by the sibling WinUI companion. Those encodings passed isolated native constructor tests; game loading, downstream limits and eviction behavior are unverified. See `../intel-cache-cap-winui/README.md`. Intel's instruction reference documents sign extension: https://cdrdv2-public.intel.com/835752/253667-sdm-vol-2b.pdf

## Strict support and scope

Only `igd12um64xe2.dll`, driver 32.0.101.9034, 42,440,832 bytes:

Original SHA-256: `1A552E471AE986A52D045C4112E1980FB7171AABEBFF93C027A4BBA10A3C6229`.

Verified 1 GiB SHA-256: `74CC626FA3F16E4365152B2FD241A11641E3713B5A2F822662F3DD0E1825FCE3`.

The engine checks AMD64 PE32+, maps instruction RVA `0x18C85C` to executable file offset `0x18BC5C`, validates surrounding bytes, and edits only the four-byte immediate at `0x18BC60`. Cap-only variants are accepted only if normalizing that immediate reproduces the full original SHA-256. The 1 GiB output must match its known hash. No other driver versions or unrelated byte modifications are accepted.

The existing static analysis traced config+0x10 to the cache object's size-check field. This is a **default per-cache-file cap**, not a guaranteed limit for each game or the total cache directory. Application profiles can override it. No shaders are deleted.

## Native Windows APIs and recovery

`FileSecurity` and `RawSecurityDescriptor` capture and restore owner, group and DACL. Access comparisons retain ordered ACE bytes, inherited ACE flags, DACL presence, owner/group and protection. Only the `AI` bookkeeping difference is ignored. Audit/SACL settings are not captured or deliberately modified; restoration uses owner/group/access sections only.

`WinVerifyTrust` checks embedded Authenticode without online retrieval. The UI explicitly says **catalog trust is not checked**. This is narrower than Windows' complete driver package verification. A modified copy invalidates original signature/catalog correspondence irrespective of the displayed embedded result. See Microsoft: https://learn.microsoft.com/en-us/windows-hardware/drivers/install/release-signing-a-driver-package-s-catalog-file

Installation checks administrator status, Safe Mode, this Windows installation's exact DriverStore target, and reparse points. It refuses observed loaded Xe2 modules. The process scan reports inaccessible processes and cannot prove absence of every loaded image. After explicit confirmation it rechecks the bundle, target bytes and ACL, stages and flushes a validated copy, restores its ACL, and uses `File.Replace`. The target is rechecked immediately before replacement. A durable journal records start and results; no delayed reboot replacement or background system-file writes.

Before-replacement failure leaves target bytes unchanged. Post-replacement validation failure records `recovery-required`; return to Safe Mode and roll back. Changed bytes/ACL or access denial stop the operation. Power loss and filesystem faults are not guaranteed recoverable by a file journal. Keep the official Intel installer available.

The app never changes boot configuration, test signing, Secure Boot, App Control, driver enforcement or ownership. Safe Mode does not restore trust or guarantee the modified driver can load.

Logs: `%LOCALAPPDATA%\IntelArcCacheCap\logs`. They may contain local paths and process details.

## Build and test

Open `IntelArcCacheCap.csproj` in Visual Studio with .NET Framework 4.8 targeting tools, or run `build.cmd` using the Windows Framework compiler. No package restore is needed. `build.cmd` builds the GUI and a separate test runner.

Run `bin\ArcCap.Tests.exe <path-to-supported-original.dll> <optional-report.txt>`. Tests operate on temporary files, including real Windows ACL restoration, replacement and rollback. Proprietary Intel DLLs are not distributed. No tests install to DriverStore.

Implemented: compiled GUI, strict patch engine, progress/results, native owner/group/DACL workflow, package discovery, embedded signature check, consent-gated Safe Mode replacement and rollback, journal/logs and best-effort module observation.

Unverified: live installed-driver replacement/rollback, cache-limit behavior and compatibility outside the single tested Witcher build. Private 1 GiB loading and user-confirmed gameplay are verified as described above. Experimental 2/4 GiB constructor encodings are implemented in the shared engine and WinUI companion; runtime effects remain unverified. Not implemented: offline Windows installation, new version discovery, per-game AIL profile editing, in-memory hashing, driver installer integration. Native schema-2 bundles are distinct from the earlier Python prototype's schema-1 bundles; create a fresh bundle.

GPL-3.0, matching the parent repository. This folder is independent of SCSKiller's normal functionality.
