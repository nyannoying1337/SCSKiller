# Does Intel's driver accept its per-exe cache file (LocalLow\Intel\ShaderCache) with the zero slack removed? On a B580 a
# cache file is ~60% zero slack between records, and the cap is 512 MB of file: compacted, the same cap would hold ~2.5x the
# pipelines. Under a throwaway exe name: compile (cold), again (hit), with the cache file gone (cold), then with each
# compacted variant put in its place before every run. A create as fast as the hit = the driver read the variant.
#   powershell -ExecutionPolicy Bypass -File compact-test.ps1
# Variants: A = the records back to back; B = A plus the fields Intel's prebuilt directml.pso.bin has (header flag 00,
# per-record size clen + 24, the extra field 0). Only the throwaway name's cache file is touched; it's deleted at the end.
param([int]$Runs = 3)

$ErrorActionPreference = 'Continue'
$kit = Split-Path -Parent $MyInvocation.MyCommand.Path
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = Join-Path $kit "compact-test-$stamp"
New-Item -ItemType Directory -Force $out | Out-Null
function Log([string]$s) { Write-Host $s; Add-Content -Path (Join-Path $out 'summary.txt') -Value $s }
Get-ChildItem $kit -File | Unblock-File -ErrorAction SilentlyContinue
$cache = "$($env:LOCALAPPDATA)Low\Intel\ShaderCache"

# INSC layout (ARCHITECTURE.md, Intel): a 0x1a-byte header ('INSC', u32, 3 flag bytes, a 15-character build stamp), then per
# record a 16-byte hash, 28 zero bytes, u64 X, a 44-byte entry header (u8 1 at +52; u64 clen at +88) and clen bytes of zlib
# ('78 01'). A cache file leaves zero slack after each record.
function Records([byte[]]$d) {
    $list = [Collections.Generic.List[long[]]]::new()
    $p = 0x1a
    while ($p + 98 -le $d.Length) {
        $clen = [BitConverter]::ToInt64($d, $p + 88)
        if ($d[$p + 96] -ne 0x78 -or $d[$p + 97] -ne 1 -or $d[$p + 52] -ne 1 -or $clen -le 0 -or $p + 96 + $clen -gt $d.Length) { throw "no record at 0x$('{0:x}' -f $p)" }
        $list.Add([long[]]@($p, $clen))
        $s = $p + 96 + $clen
        $next = -1
        while ($s + 98 -le $d.Length) {
            if ($d[$s + 96] -eq 0x78 -and $d[$s + 97] -eq 1 -and $d[$s + 52] -eq 1 -and [BitConverter]::ToInt64($d, $s + 88) -gt 0) { $next = $s; break }
            if ($d[$s] -ne 0) { throw "data in the slack at 0x$('{0:x}' -f $s)" }
            $s++
        }
        if ($next -lt 0) { break }
        $p = $next
    }
    return , $list   # the comma: a list of one record must not unroll into its two numbers
}
function Compact([byte[]]$d, $recs, [bool]$prebuiltFields) {
    $ms = [IO.MemoryStream]::new()
    $h = [byte[]]$d[0..0x19]
    if ($prebuiltFields) { $h[8] = 0; $h[9] = 0; $h[10] = 0 }
    $ms.Write($h, 0, $h.Length)
    foreach ($r in $recs) {
        $n = 96 + $r[1]
        $b = New-Object byte[] $n
        [Array]::Copy($d, $r[0], $b, 0, $n)
        if ($prebuiltFields) {
            [BitConverter]::GetBytes([long]($r[1] + 24)).CopyTo($b, 44)
            [BitConverter]::GetBytes([long]0).CopyTo($b, 80)
        }
        $ms.Write($b, 0, $n)
    }
    return $ms.ToArray()
}

$name = "scskct$((Get-Random -Maximum 999999))"
$exe = Join-Path $kit "$name.exe"
Copy-Item (Join-Path $kit 'selftest.exe') $exe
$seed = 100000 + (Get-Random -Maximum 8000000)
Log "compact test, $stamp"
foreach ($v in Get-CimInstance Win32_VideoController) { Log ("GPU: {0} | driver {1}" -f $v.Name, $v.DriverVersion) }

# The baseline create of one probe 6 process (proc 2), in ms; $place: bytes to put as the cache file first; after: the file is deleted.
function Probe([string]$what, [byte[]]$place, [switch]$Keep) {
    $sizes = @()
    $ms = foreach ($i in 1..$Runs) {
        if ($place) { [IO.File]::WriteAllBytes($script:path, $place) }
        $f = Join-Path $out 'probe.txt'
        & $exe fields2 $seed $f | Out-Null
        $sizes += if (Test-Path $script:path) { (Get-Item $script:path).Length } else { 0 }
        if (-not $Keep) { Remove-Item $script:path -ErrorAction SilentlyContinue }
        $row = Get-Content $f -ErrorAction SilentlyContinue | Where-Object { $_ -like 'ctl: baseline (proc 1: cold)*' } | Select-Object -First 1
        if ($row) { [double]::Parse(($row -split "`t")[1], [Globalization.CultureInfo]::InvariantCulture) }
    }
    $m = ($ms | Sort-Object)[[math]::Floor(@($ms).Count / 2)]
    Log ("{0,-44} {1,7:N2} ms  (runs: {2}; cache file after: {3} bytes)" -f $what, $m, (($ms | ForEach-Object { '{0:N2}' -f $_ }) -join ', '), ($sizes -join ', '))
    return $m
}

$before = @{}; Get-ChildItem $cache -File -ErrorAction SilentlyContinue | ForEach-Object { $before[$_.Name] = 1 }
& $exe fields1 $seed (Join-Path $out 'setup.txt') | Out-Null
$mine = Get-ChildItem $cache -File | Where-Object { -not $before.ContainsKey($_.Name) } | Sort-Object Length -Descending | Select-Object -First 1
if (-not $mine) { Log 'no new cache file: is the Intel GPU the default adapter?'; Remove-Item $exe; exit 1 }
$path = $mine.FullName
$orig = [IO.File]::ReadAllBytes($path)
$recs = Records $orig
$recBytes = ($recs | ForEach-Object { 96 + $_[1] } | Measure-Object -Sum).Sum
Log ("cache file: {0}: {1:N0} bytes, {2} records, {3:N0} bytes of records, {4:N0} bytes of slack" -f $mine.Name, $orig.Length, $recs.Count, $recBytes, ($orig.Length - 0x1a - $recBytes))
$a = Compact $orig $recs $false
$b = Compact $orig $recs $true
[IO.File]::WriteAllBytes((Join-Path $out 'variant-A.bin'), $a)
[IO.File]::WriteAllBytes((Join-Path $out 'variant-B.bin'), $b)
Log ("variant A: {0:N0} bytes ({1:N0}%), variant B: {2:N0} bytes" -f $a.Length, (100 * $a.Length / $orig.Length), $b.Length)

$hit = Probe 'original file (hit)' $orig
$cold = Probe 'no cache file (cold)' $null
$ta = Probe 'variant A (compacted)' $a
$tb = Probe 'variant B (compacted, prebuilt fields)' $b
Log ''
foreach ($t in @(@('A', $ta), @('B', $tb))) {
    $verdict = if ($t[1] -lt $hit + ($cold - $hit) / 3) { 'ACCEPTED (as fast as the hit)' } else { 'rejected (as slow as cold)' }
    Log ("result: variant {0}: {1} (hit {2:N2} ms, cold {3:N2} ms)" -f $t[0], $verdict, $hit, $cold)
}

Remove-Item $path -ErrorAction SilentlyContinue
Remove-Item $exe -ErrorAction SilentlyContinue
$zip = Join-Path ([Environment]::GetFolderPath('Desktop')) "scskiller-intel-compact-$stamp.zip"
Compress-Archive -Path "$out\*" -DestinationPath $zip -Force
Log "Done. Results: $zip"
