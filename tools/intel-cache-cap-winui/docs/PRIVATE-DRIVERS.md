# Private drivers: the one-time Windows setting

**Enable Use private drivers once, then restart Windows.** This is required by this app's per-game routing workflow. Run Arc Cache as administrator yourself for this change only; normal play and normal app use do not need administrator rights. The app never opens an elevation prompt or enables this silently.

## What it does

Arc Cache prepares a matched copy of the supported Intel user-mode driver package beside the game's rendering executable, in `<game.exe>.local`. Only that private copy receives the cache patch. Windows' `DevOverrideEnable` setting lets DLL redirection work even when an executable has an application manifest. The original system driver and kernel driver are not replaced.

The switch is **machine-wide**: it can also affect unrelated applications that already have a `.local` redirection setup. Games without a private setup continue to use the installed driver. This is not a per-game registry switch, and it does not disable anti-cheat, driver signing, Secure Boot or Windows integrity enforcement. A modified DLL can still be refused by Windows or a game.

## On, off and restart required

| App status | What to do |
| --- | --- |
| On | Private routing is enabled. Check the running game's loaded driver to confirm its private copy actually loaded. |
| Off | Enable the setting and restart before preparing/using private profiles in this app. Existing patched files are not proof they will load. |
| Restart required | Restart Windows. The registry value has changed, but its effect is not yet confirmed for this boot. |

Turning the setting off and restarting removes the manifest override; it does not delete private folders or shader caches. Some applications without manifests can still honor ordinary `.local` redirection. **To reliably return a particular game to stock, remove its owned private setup through the app**, rather than relying on the machine-wide toggle.

## Undo one game

Close the game and choose **Use system driver again** on its page. Arc Cache checks the saved receipt and file hashes, then removes only its owned private files. Leave the Windows setting alone if other games still use private copies. Shader caches are preserved. Missing receipts, unexpected files or changed hashes stop removal; use recovery options rather than deleting blindly.

## Undo the Windows setting

Remove your private game setups first. Manually run Arc Cache as administrator, open Settings, choose **Restore previous Windows setting**, confirm, and restart Windows. The saved previous value is restored; if it was already enabled, restoring does not switch it off. Keep `%LOCALAPPDATA%/IntelArcCacheCap/developer-override.json` for recovery. The worker blocks restore while known private game sets remain.

## Set it and close the app

Once a game is prepared and the desired limit is applied, the app can be closed. Windows loads the private files on later launches; no background installer, service or running app is required. The external overlay requires the app to stay open/minimized. Recheck compatibility after driver/game updates; old private user-mode files are not automatically compatible with a new kernel driver.

Technical reference: [Microsoft DLL redirection documentation](https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-redirection). The registry value is a DWORD at `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\DevOverrideEnable`.
