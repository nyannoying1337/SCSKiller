# Lists every string in the installed Intel driver's DLLs that mentions a cache, a prebuilt or IGSDS: the candidates for
# a hidden shader cache setting and for how prebuilt .pso.bin files are named (the per-exe cache is capped at 512 MB on a
# B580, driver 32.0.101.9034). Read-only, a few minutes. Run: powershell -ExecutionPolicy Bypass -File driver-strings.ps1
$ErrorActionPreference = 'Continue'
$kit = Split-Path -Parent $MyInvocation.MyCommand.Path
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = Join-Path $kit "driver-strings-$stamp"
New-Item -ItemType Directory -Force $out | Out-Null
function Log([string]$s) { Write-Host $s; Add-Content -Path (Join-Path $out 'summary.txt') -Value $s }
foreach ($v in Get-CimInstance Win32_VideoController) { Log ("GPU: {0} | driver {1}" -f $v.Name, $v.DriverVersion) }
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
        foreach ($m in $ascii.Matches($text)) { if ($m.Value -match 'cache|prebuilt|pso\.bin|igsds') { [void]$found.Add("$($f.Name)`t$($m.Value)") } }
        foreach ($m in $wide.Matches($text)) { $v = $m.Value -replace "`0", ''; if ($v -match 'cache|prebuilt|pso\.bin|igsds') { [void]$found.Add("$($f.Name)`t(utf16) $v") } }
    }
}
$found | Sort-Object | Set-Content (Join-Path $out 'driver-strings.txt')
Log ("driver strings mentioning a cache: {0} (driver-strings.txt)" -f $found.Count)

$zip = Join-Path ([Environment]::GetFolderPath('Desktop')) "scskiller-intel-driver-strings-$stamp.zip"
Compress-Archive -Path "$out\*" -DestinationPath $zip -Force
Log "Done. Results: $zip"
