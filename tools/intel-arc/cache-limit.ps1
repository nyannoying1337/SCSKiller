# Finds Intel's D3D12 shader cache limit and the settings that may change it. Run from the intel-arc-kit folder:
#   powershell -ExecutionPolicy Bypass -File cache-limit.ps1                  measure the limit
#   powershell -ExecutionPolicy Bypass -File cache-limit.ps1 -ScanDriver      also list cache-related strings in the driver
#   ... -MaxGB 4                                                               stop after this much growth (default 3)
# cache-check.cmd runs it with -MaxGB 1: past the 512 MB cap, then whether the oldest PSOs still come from the cache.
# The fill runs `selftest dxcfill` under a throwaway exe name in batches of distinct compute PSOs and records, after each
# batch, that name's cache file and the whole folder. A per-file cap shows as the file stopping growing (or shrinking:
# entries evicted); a folder-wide cap as other files being deleted. Other games' cache files may then be evicted: they
# compile again once, at their next launch. The throwaway file is deleted at the end.
param([switch]$ScanDriver, [double]$MaxGB = 3, [int]$Batch = 2000, [int]$Unroll = 250)

$ErrorActionPreference = 'Continue'
$kit = Split-Path -Parent $MyInvocation.MyCommand.Path
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = Join-Path $kit "cache-limit-$stamp"
New-Item -ItemType Directory -Force $out | Out-Null
function Log([string]$s) { Write-Host $s; Add-Content -Path (Join-Path $out 'summary.txt') -Value $s }
Get-ChildItem $kit -File | Unblock-File -ErrorAction SilentlyContinue

$cache = "$($env:LOCALAPPDATA)Low\Intel\ShaderCache"
Log "Intel shader cache limit, $stamp"
foreach ($v in Get-CimInstance Win32_VideoController) { Log ("GPU: {0} | driver {1}" -f $v.Name, $v.DriverVersion) }

# --- Settings the driver might read --------------------------------------------------------------------------------------
foreach ($k in 'HKLM:\SOFTWARE\Intel', 'HKCU:\SOFTWARE\Intel', 'HKLM:\SOFTWARE\WOW6432Node\Intel') {
    if (Test-Path $k) {
        Get-ChildItem $k -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
            $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
            foreach ($n in $p.PSObject.Properties.Name) {
                if ($n -match 'cache|shader|disk' -and $n -notmatch '^PS') { Log "registry: $($_.Name) $n = $($p.$n)" }
            }
        }
    }
}
# --- Fill ----------------------------------------------------------------------------------------------------------------
$exe = Join-Path $kit "scskfill$((Get-Random -Maximum 999999)).exe"
Copy-Item (Join-Path $kit 'selftest.exe') $exe
# dxcfill's creates timed alone (no D3DCompile): the median, and the % under 1 ms (from the driver cache; a compile takes ms)
function Creates($lines) {
    foreach ($l in @($lines)) { if ("$l" -match 'create median ([\d.]+) ms, under 1 ms ([\d.]+)%') { return [pscustomobject]@{ Ms = [double]$Matches[1]; Hit = [double]$Matches[2] } } }
    [pscustomobject]@{ Ms = $null; Hit = $null }
}
function Folder { Get-ChildItem $cache -File -ErrorAction SilentlyContinue | Select-Object Name, Length, LastWriteTimeUtc }
$start = @(Folder)
$startNames = @{}; foreach ($f in $start) { $startNames[$f.Name] = $f.Length }
Log ("cache folder: {0}: {1} files, {2:N1} MB" -f $cache, $start.Count, (($start | Measure-Object Length -Sum).Sum / 1MB))
$mine = $null; $last = -1; $flat = 0; $maxBytes = $MaxGB * 1GB; $grown = 0
$rows = @()
Log ("filling in batches of {0:N0} compute PSOs (a line per batch; up to {1} GB of growth)..." -f $Batch, $MaxGB)
for ($b = 1; $b -le 10000; $b++) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $cr = Creates (& $exe dxcfill $Batch $Unroll $b 0)
    $now = @(Folder)
    if (-not $mine) {   # this name's file: the one that's new since the start
        $mine = ($now | Where-Object { -not $startNames.ContainsKey($_.Name) } | Sort-Object Length -Descending | Select-Object -First 1).Name
        if (-not $mine) { Log 'no new cache file after the first batch: is the Intel GPU the default adapter?'; break }
        Log "this exe's cache file: $mine"
    }
    $size = ($now | Where-Object Name -eq $mine).Length
    $gone = @($start | Where-Object { $n = $_.Name; -not ($now | Where-Object Name -eq $n) }).Count
    $total = ($now | Measure-Object Length -Sum).Sum
    $rows += [pscustomobject]@{ Batch = $b; PSOs = $b * $Batch; FileMB = [math]::Round($size / 1MB, 1); FolderMB = [math]::Round($total / 1MB, 1)
        OtherFilesGone = $gone; Seconds = [math]::Round($sw.Elapsed.TotalSeconds, 1); CreateMs = $cr.Ms; HitPct = $cr.Hit }
    Log ("batch {0}: {1:N0} PSOs, file {2:N1} MB, folder {3:N1} MB, other files gone {4}, {5:N0} s, create median {6:N2} ms" -f $b, ($b * $Batch), ($size / 1MB), ($total / 1MB), $gone, $sw.Elapsed.TotalSeconds, $cr.Ms)
    if ($null -eq $size) { Log 'this exe''s cache file was deleted: a folder-wide eviction took it'; break }
    if ($size -le $last + 1MB) { $flat++ } else { $flat = 0 }   # at the cap the file still changes by a few bytes (B580: 512.0 MB)
    if ($flat -ge 3) { Log ("the file stopped growing at {0:N1} MB: a per-file cap" -f ($size / 1MB)); break }
    $grown = $size; $last = $size
    if ($grown -ge $maxBytes) { Log ("stopped at {0:N1} MB of growth (-MaxGB {1}): no cap below that" -f ($grown / 1MB), $MaxGB); break }
}
$rows | Export-Csv (Join-Path $out 'fill.csv') -NoTypeInformation

# Are the first batch's PSOs still cached, or were they evicted? Their creates again, beside the newest batch's again (surely
# cached) and their own first run (compiled): the share of creates under 1 ms, i.e. served from the driver cache.
if ($rows.Count -ge 2) {
    $newest = $rows[-1].Batch
    $hit = Creates (& $exe dxcfill $Batch $Unroll $newest 0)
    $old = Creates (& $exe dxcfill $Batch $Unroll 1 0)
    Log ("from the cache (creates under 1 ms): batch 1 first run {0:N0}%, batch {1} again {2:N0}%, batch 1 again {3:N0}%" -f $rows[0].HitPct, $newest, $hit.Hit, $old.Hit)
    Log ("create median: batch 1 first run {0:N2} ms, batch {1} again {2:N2} ms, batch 1 again {3:N2} ms" -f $rows[0].CreateMs, $newest, $hit.Ms, $old.Ms)
    if ($null -ne $old.Hit -and $hit.Hit -ge 50) {
        $kept = [math]::Min(100, 100 * $old.Hit / $hit.Hit)
        $verdict = if ($kept -ge 90) { 'KEPT: the oldest PSOs still come from the cache' } elseif ($kept -le 10) { 'EVICTED: the oldest PSOs compile again' }
                   else { 'PARTLY: some of the oldest PSOs were evicted' }
        Log ("result after {0:N1} MB: {1} (about {2:N0}% of batch 1 kept)" -f $rows[-1].FileMB, $verdict, $kept)
    } else { Log 'no result: the newest batch did not come from the cache either, so the timing cannot tell' }
}

Remove-Item $exe -ErrorAction SilentlyContinue
if ($mine) { Remove-Item (Join-Path $cache $mine) -ErrorAction SilentlyContinue; Log "deleted the throwaway cache file $mine" }

# --- Driver strings (last: the fill matters more) ------------------------------------------------------------------------
if ($ScanDriver) {
    # The user-mode driver files: the class key's DriverStore folder of the Intel adapter. Printable ASCII and UTF-16 runs
    # are pulled out first (one linear pass per file), then those mentioning a cache kept: driver-strings.txt.
    $class = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}'
    $dirs = Get-ChildItem $class -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match '^\d{4}$' } | ForEach-Object {
        $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
        if ($p.ProviderName -match 'Intel' -and $p.UserModeDriverName) {
            foreach ($f in @($p.UserModeDriverName)) { if ($f -and (Test-Path $f)) { Split-Path $f -Parent } }
        }
    } | Sort-Object -Unique
    $ascii = [regex]'[\x20-\x7E]{6,200}'
    $wide = [regex]'(?:[\x20-\x7E]\x00){6,200}'
    $found = [Collections.Generic.HashSet[string]]::new()
    foreach ($d in $dirs) {
        $files = @(Get-ChildItem $d -Filter *.dll -File | Sort-Object Length)
        Log ("driver folder: {0} ({1} DLLs, {2:N0} MB)" -f $d, $files.Count, (($files | Measure-Object Length -Sum).Sum / 1MB))
        $i = 0
        foreach ($f in $files) {
            $i++
            Write-Host ("  [{0}/{1}] {2} ({3:N0} MB)" -f $i, $files.Count, $f.Name, ($f.Length / 1MB))
            $text = [Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes($f.FullName))
            foreach ($m in $ascii.Matches($text)) { if ($m.Value -match 'cache') { [void]$found.Add("$($f.Name)`t$($m.Value)") } }
            foreach ($m in $wide.Matches($text)) { $v = $m.Value -replace "`0", ''; if ($v -match 'cache') { [void]$found.Add("$($f.Name)`t(utf16) $v") } }
        }
    }
    $found | Sort-Object | Set-Content (Join-Path $out 'driver-strings.txt')
    Log ("driver strings mentioning a cache: {0} (driver-strings.txt)" -f $found.Count)
}

$zip = Join-Path ([Environment]::GetFolderPath('Desktop')) "scskiller-intel-cache-limit-$stamp.zip"
Compress-Archive -Path "$out\*" -DestinationPath $zip -Force
Log "Done. Results: $zip"
