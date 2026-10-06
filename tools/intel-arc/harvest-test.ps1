# Intel's D3D12 driver (igd12um64xe2.dll, 32.0.101.9034) names an offline shader cache: OfflineShaderCacheHarvestModeEnabled,
# OfflineShaderCacheHarvestTargetDirectory (write one) and OfflineShaderCacheFile (load one), next to PrebuiltShaderBinaryDirPath,
# which sits in HKLM\SOFTWARE\Intel\Display\igfxcui\3D. This sets them, under that key and the adapter's class key, around a
# throwaway exe's pipelines, and checks:
#   1. harvest: does the driver write a file to the target directory (and what is it)?
#   2. load: with OfflineShaderCacheFile pointing at it and the exe's LocalLow cache file gone, is the baseline create a hit?
#   3. prebuilt: is a harvested file loaded from PrebuiltShaderBinaries\<every family>\ under its own name?
# Needs an administrator PowerShell. Every registry value it sets is removed (or put back) at the end, and every file it
# placed outside its results folder deleted. Run: powershell -ExecutionPolicy Bypass -File harvest-test.ps1
param([int]$Runs = 3)

$ErrorActionPreference = 'Continue'
$kit = Split-Path -Parent $MyInvocation.MyCommand.Path
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = Join-Path $kit "harvest-test-$stamp"
New-Item -ItemType Directory -Force $out | Out-Null
function Log([string]$s) { Write-Host $s; Add-Content -Path (Join-Path $out 'summary.txt') -Value $s }
Get-ChildItem $kit -File | Unblock-File -ErrorAction SilentlyContinue
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host 'Run this from an administrator PowerShell: it writes Intel driver settings under HKLM.'; exit 1
}
Log "harvest test, $stamp"
foreach ($v in Get-CimInstance Win32_VideoController) { Log ("GPU: {0} | driver {1}" -f $v.Name, $v.DriverVersion) }

$cache = "$($env:LOCALAPPDATA)Low\Intel\ShaderCache"
$root = 'C:\ProgramData\Intel\IGSDS\PrebuiltShaderBinaries'
$harvest = 'C:\scsk-harvest'
$keys = @('HKLM:\SOFTWARE\Intel\Display\igfxcui\3D')
$class = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}'
Get-ChildItem $class -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match '^\d{4}$' } | ForEach-Object {
    $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
    if ($p.ProviderName -match 'Intel' -and $p.UserModeDriverName) { $keys += $_.PSPath }
}
Log ("keys: {0}" -f ($keys -join ' | '))

# Registry values: remembered before the first change, restored at the end.
$saved = @{}
function SetValue([string]$name, $value, [string]$type) {
    foreach ($k in $keys) {
        if (-not (Test-Path $k)) { New-Item -Path $k -Force | Out-Null }
        $id = "$k|$name"
        if (-not $saved.ContainsKey($id)) {
            $old = Get-ItemProperty -Path $k -Name $name -ErrorAction SilentlyContinue
            $saved[$id] = if ($old) { @{ Had = $true; Value = $old.$name; Type = (Get-Item $k).GetValueKind($name) } } else { @{ Had = $false } }
        }
        New-ItemProperty -Path $k -Name $name -Value $value -PropertyType $type -Force | Out-Null
    }
}
function ClearValue([string]$name) { foreach ($k in $keys) { Remove-ItemProperty -Path $k -Name $name -ErrorAction SilentlyContinue } }
function Restore {
    foreach ($id in $saved.Keys) {
        $k, $name = $id -split '\|', 2
        $s = $saved[$id]
        if ($s.Had) { New-ItemProperty -Path $k -Name $name -Value $s.Value -PropertyType $s.Type -Force | Out-Null }
        else { Remove-ItemProperty -Path $k -Name $name -ErrorAction SilentlyContinue }
    }
}

$name = "scskhv$((Get-Random -Maximum 999999))"
$exe = Join-Path $kit "$name.exe"
Copy-Item (Join-Path $kit 'selftest.exe') $exe
$seed = 100000 + (Get-Random -Maximum 8000000)
function Baseline([string]$file) {
    $row = Get-Content $file -ErrorAction SilentlyContinue | Where-Object { $_ -like 'ctl: baseline (proc 1: cold)*' } | Select-Object -First 1
    if ($row) { [double]::Parse(($row -split "`t")[1], [Globalization.CultureInfo]::InvariantCulture) } else { -1 }
}
# Median baseline create of $Runs probe processes; the exe's LocalLow cache file is deleted before each unless -Keep.
function Probe([string]$what, [switch]$Keep) {
    $ms = foreach ($i in 1..$Runs) {
        if (-not $Keep -and $script:mine) { Remove-Item (Join-Path $cache $script:mine) -ErrorAction SilentlyContinue }
        & $exe fields2 $seed (Join-Path $out 'probe.txt') | Out-Null
        Baseline (Join-Path $out 'probe.txt')
    }
    $m = ($ms | Sort-Object)[[math]::Floor(@($ms).Count / 2)]
    Log ("{0,-58} {1,7:N2} ms  (runs: {2})" -f $what, $m, (($ms | ForEach-Object { '{0:N2}' -f $_ }) -join ', '))
    return $m
}
function Hex([string]$path) { $b = [IO.File]::ReadAllBytes($path); ($b[0..([math]::Min(47, $b.Length - 1))] | ForEach-Object { '{0:x2}' -f $_ }) -join ' ' }

try {
    # --- 1. harvest ---
    Remove-Item $harvest -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $harvest | Out-Null
    SetValue 'OfflineShaderCacheHarvestModeEnabled' 1 'DWord'
    SetValue 'OfflineShaderCacheHarvestTargetDirectory' $harvest 'String'
    $before = @{}; Get-ChildItem $cache -File -ErrorAction SilentlyContinue | ForEach-Object { $before[$_.Name] = 1 }
    & $exe fields1 $seed (Join-Path $out 'setup.txt') | Out-Null
    $script:mine = (Get-ChildItem $cache -File | Where-Object { -not $before.ContainsKey($_.Name) } | Sort-Object Length -Descending | Select-Object -First 1).Name
    Log ("LocalLow cache file under harvest mode: {0}" -f $(if ($mine) { $mine } else { 'none (harvest mode may replace the normal cache)' }))
    $harvested = @(Get-ChildItem $harvest -Recurse -File -ErrorAction SilentlyContinue)
    Log ("1. harvest: {0} file(s) in {1}" -f $harvested.Count, $harvest)
    foreach ($f in $harvested) { Log ("   {0} ({1:N0} bytes): {2}" -f $f.FullName, $f.Length, (Hex $f.FullName)); Copy-Item $f.FullName $out }
    $hit = Probe 'hit (harvest mode on, LocalLow file kept)' -Keep
    ClearValue 'OfflineShaderCacheHarvestModeEnabled'; ClearValue 'OfflineShaderCacheHarvestTargetDirectory'
    $cold = Probe 'cold (LocalLow file deleted before each run)'

    if ($harvested) {
        # --- 2. load through OfflineShaderCacheFile ---
        $file = ($harvested | Sort-Object Length -Descending | Select-Object -First 1).FullName
        SetValue 'OfflineShaderCacheFile' $file 'String'
        $load = Probe "2. OfflineShaderCacheFile = $(Split-Path $file -Leaf)"
        ClearValue 'OfflineShaderCacheFile'
        # --- 3. the harvested file(s) as prebuilts in every family folder ---
        $placed = @()
        foreach ($fam in Get-ChildItem $root -Directory -ErrorAction SilentlyContinue | ForEach-Object { Get-ChildItem $_.FullName -Directory }) {
            foreach ($f in $harvested) {
                $n = if ($f.Name -like '*.pso.bin') { $f.Name } else { "$($f.Name).pso.bin" }
                $p = Join-Path $fam.FullName $n
                if (-not (Test-Path $p)) { Copy-Item $f.FullName $p -ErrorAction SilentlyContinue; if (Test-Path $p) { $placed += $p } }
            }
        }
        $pre = Probe ("3. harvested file(s) placed as prebuilts ({0} copies)" -f $placed.Count)
        foreach ($p in $placed) { Remove-Item $p -ErrorAction SilentlyContinue }
        Log ''
        foreach ($t in @(@('2. OfflineShaderCacheFile', $load), @('3. prebuilt folder', $pre))) {
            $v = if ($t[1] -ge 0 -and $t[1] -lt $hit + ($cold - $hit) / 3) { 'LOADED (as fast as the hit)' } else { 'not loaded (as slow as cold)' }
            Log ("result: {0}: {1} (hit {2:N2} ms, cold {3:N2} ms)" -f $t[0], $v, $hit, $cold)
        }
    } else {
        Log ''
        Log 'result: harvest mode wrote nothing (the settings may live elsewhere, or need an app profile)'
    }
}
finally {
    Restore
    Log 'registry: every value this test set is removed (or put back)'
    if ($script:mine) { Remove-Item (Join-Path $cache $script:mine) -ErrorAction SilentlyContinue }
    Remove-Item $exe -ErrorAction SilentlyContinue
    Remove-Item $harvest -Recurse -Force -ErrorAction SilentlyContinue   # copies are in the results
}
$zip = Join-Path ([Environment]::GetFolderPath('Desktop')) "scskiller-intel-harvest-$stamp.zip"
Compress-Archive -Path "$out\*" -DestinationPath $zip -Force
Log "Done. Results: $zip"
