# Does Intel's driver load a prebuilt shader file (C:\ProgramData\Intel\IGSDS\PrebuiltShaderBinaries\<dGPU|iGPU>\<family>\
# <name>.pso.bin, as Intel ships directml.pso.bin) for any exe, and does it accept a copy of that exe's own cache file
# (LocalLow\Intel\ShaderCache, the same INSC container, unpacked)? Needs an administrator PowerShell (ProgramData\Intel).
#   powershell -ExecutionPolicy Bypass -File prebuilt-test.ps1
# Timed with selftest's probe 6 child (fields), which times each PSO create alone: its baseline pipeline is ~4 ms cold and
# ~0.1 ms from the driver cache (Arc B580). Under a throwaway exe name: compile (cold), again (hit), with the cache file
# moved away (cold: the baseline), then with a copy of the cache file placed as a prebuilt in every family folder under
# each candidate name and the cache file moved away again. A create as fast as the hit = the driver loaded the prebuilt.
# Only files it creates are touched: every copy is removed and the cache file put back at the end.
param([int]$Runs = 3)

$ErrorActionPreference = 'Continue'
$kit = Split-Path -Parent $MyInvocation.MyCommand.Path
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = Join-Path $kit "prebuilt-test-$stamp"
New-Item -ItemType Directory -Force $out | Out-Null
function Log([string]$s) { Write-Host $s; Add-Content -Path (Join-Path $out 'summary.txt') -Value $s }
Get-ChildItem $kit -File | Unblock-File -ErrorAction SilentlyContinue

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host 'Run this from an administrator PowerShell: it writes to C:\ProgramData\Intel.'; exit 1
}
$cache = "$($env:LOCALAPPDATA)Low\Intel\ShaderCache"
$root = 'C:\ProgramData\Intel\IGSDS\PrebuiltShaderBinaries'
$families = @(Get-ChildItem $root -Directory -ErrorAction SilentlyContinue | ForEach-Object { Get-ChildItem $_.FullName -Directory })
Log "prebuilt test, $stamp"
foreach ($v in Get-CimInstance Win32_VideoController) { Log ("GPU: {0} | driver {1} | {2}" -f $v.Name, $v.DriverVersion, $v.PNPDeviceID) }
Log ("family folders: {0}" -f (($families | ForEach-Object { $_.FullName.Substring($root.Length + 1) }) -join ', '))
if (-not $families) { Log "no family folders under $root"; exit 1 }

$name = "scskpb$((Get-Random -Maximum 999999))"
$exe = Join-Path $kit "$name.exe"
Copy-Item (Join-Path $kit 'selftest.exe') $exe
$seed = 100000 + (Get-Random -Maximum 8000000)
# One probe process (proc 2: no setup rows); the median over $Runs of its baseline create, in ms.
function Probe([string]$what) {
    $ms = foreach ($i in 1..$Runs) {
        $f = Join-Path $out "probe.txt"
        & $exe fields2 $seed $f | Out-Null
        $row = Get-Content $f -ErrorAction SilentlyContinue | Where-Object { $_ -like 'ctl: baseline (proc 1: cold)*' } | Select-Object -First 1
        if ($row) { [double]::Parse(($row -split "`t")[1], [Globalization.CultureInfo]::InvariantCulture) }
    }
    $m = ($ms | Sort-Object)[[math]::Floor(@($ms).Count / 2)]
    Log ("{0,-56} {1,7:N2} ms  (runs: {2})" -f $what, $m, (($ms | ForEach-Object { '{0:N2}' -f $_ }) -join ', '))
    return $m
}
$before = @{}; Get-ChildItem $cache -File -ErrorAction SilentlyContinue | ForEach-Object { $before[$_.Name] = 1 }
& $exe fields1 $seed (Join-Path $out 'setup.txt') | Out-Null   # compiles the pipelines once: cold, into this name's cache file
$mine = Get-ChildItem $cache -File | Where-Object { -not $before.ContainsKey($_.Name) } | Sort-Object Length -Descending | Select-Object -First 1
if (-not $mine) { Log 'no new cache file: is the Intel GPU the default adapter?'; Remove-Item $exe; exit 1 }
Log ("cache file: {0} ({1:N0} bytes)" -f $mine.Name, $mine.Length)
$hit = Probe 'from the driver cache (hit)'
$saved = Join-Path $out "cache-$($mine.Name)"
Move-Item $mine.FullName $saved
# Cold: each run would cache it again, so move the fresh file away after each.
function ColdProbe([string]$what) {
    $ms = foreach ($i in 1..$Runs) {
        $f = Join-Path $out "probe.txt"
        & $exe fields2 $seed $f | Out-Null
        Remove-Item (Join-Path $cache $mine.Name) -ErrorAction SilentlyContinue
        $row = Get-Content $f -ErrorAction SilentlyContinue | Where-Object { $_ -like 'ctl: baseline (proc 1: cold)*' } | Select-Object -First 1
        if ($row) { [double]::Parse(($row -split "`t")[1], [Globalization.CultureInfo]::InvariantCulture) }
    }
    $m = ($ms | Sort-Object)[[math]::Floor(@($ms).Count / 2)]
    Log ("{0,-56} {1,7:N2} ms  (runs: {2})" -f $what, $m, (($ms | ForEach-Object { '{0:N2}' -f $_ }) -join ', '))
    return $m
}
$cold = ColdProbe 'cache file moved away (cold)'

# Candidate names: the exe's name in the forms a driver might use, and the cache file's own name. Each is placed in every
# family folder at once (the driver reads its GPU's), then removed before the next.
$result = 'no candidate loaded'
foreach ($n in @("$name.exe", $name, ($name.ToUpperInvariant() + '.EXE'), $mine.Name)) {
    $placed = @()
    foreach ($fam in $families) {
        $p = Join-Path $fam.FullName "$n.pso.bin"
        try { Copy-Item $saved $p -ErrorAction Stop; $placed += $p }
        catch { Log "couldn't write $p : $($_.Exception.Message)" }
    }
    if (-not $placed) { continue }
    $t = ColdProbe "prebuilt as '$n.pso.bin' (all families)"
    foreach ($p in $placed) { Remove-Item $p -ErrorAction SilentlyContinue }
    if ($t -lt ($hit + ($cold - $hit) / 3)) { $result = "LOADED as '$n.pso.bin'"; break }
}
Log ''
Log ("result: {0} (hit {1:N2} ms, cold {2:N2} ms)" -f $result, $hit, $cold)

# Clean up: no copy left in any family folder, the cache file back where it was, no throwaway exe.
foreach ($fam in $families) { Get-ChildItem $fam.FullName -Filter "$name*" -File -ErrorAction SilentlyContinue | Remove-Item -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $fam.FullName "$($mine.Name).pso.bin") -ErrorAction SilentlyContinue }
Move-Item $saved (Join-Path $cache $mine.Name) -Force -ErrorAction SilentlyContinue
Remove-Item $exe -ErrorAction SilentlyContinue
$zip = Join-Path ([Environment]::GetFolderPath('Desktop')) "scskiller-intel-prebuilt-$stamp.zip"
Compress-Archive -Path "$out\*" -DestinationPath $zip -Force
Log "Done. Results: $zip"
