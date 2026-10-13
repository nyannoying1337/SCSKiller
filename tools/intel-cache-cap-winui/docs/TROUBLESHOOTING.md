# Reporting a problem

1. Open Settings → Troubleshooting and turn on **Detailed diagnostic logging**. Alternatively launch the EXE with `--debug` to enable it for that session without saving the preference.
2. Reproduce the problem once. Open Activity for the ordinary action result; diagnostics add worker requests/results, timings, observed driver facts and full exception details.
3. Choose **Open logs folder**. Review `diagnostics.jsonl` and `diagnostics.jsonl.previous` before sharing. They can contain your Windows username, game/driver/receipt paths and file hashes. Normal action logs and overlay CSVs are in the same folder. Never share account tokens or unrelated files.
4. Turn detailed logging off afterward. Existing logs are retained for manual review/removal. Disabling the toggle does not remove previous logs. No automatic upload or telemetry is implemented.

Detailed logs are opt-in and rotate at about 2 MiB into one previous generation (about 4 MiB total). Individual diagnostic payloads are bounded to 16 KiB. Normal action/recovery logs continue when detailed logging is off. The toggle changes logging only: no game/system mutation, no elevated process, no changed driver qualification.

Include the app version/standard-or-experimental variant, Windows version, GPU model, driver version, game name/DX12 mode, selected limit and exact failure message. Loaded paths/disk hashes are observations, not proof of shader capacity. The source retains legacy Witcher profile support for recovery, but normal detection and the monitor use the selected game rather than a Witcher-only whitelist.
