# Fails if the firmware links Wi-Fi, Bluetooth, OTA or TinyUSB code (requirement A-04). This detects
# linked code by symbol names (nm) and by the archive members in the linker map; it cannot see code
# that hides under other names, so it detects, it does not prove.
# Usage: pwsh tools/check-image.ps1 [firmware/build/speaker_buddy.elf] [-Nm <path to nm>] [-Map <map>]
# Without -Nm, xtensa-esp32s3-elf-nm is taken from PATH or from the ESP-IDF tools directory.
# Without -Map, the .map next to the ELF is used.
param(
    [string] $Elf = (Join-Path $PSScriptRoot '..\firmware\build\speaker_buddy.elf'),
    [string] $Nm,
    [string] $Map
)

$ErrorActionPreference = 'Stop'

# Symbol name prefixes (after an optional leading underscore) that must not appear in the image.
# OTA is matched by its update functions only: the IDF core itself links
# esp_ota_get_running_partition (esp_partition/partition_target.c) to find the running app.
$forbiddenPrefixes = @(
    'esp_wifi_', 'esp_bt_', 'esp_ble_', 'esp_bluedroid_', 'nimble_port_', 'btdm_', 'tud_',
    'esp_ota_begin', 'esp_ota_write', 'esp_ota_end', 'esp_ota_set_boot_partition', 'esp_https_ota'
)
# Substrings that must not appear anywhere in a symbol name
$forbiddenSubstrings = @('tinyusb')
# Radio and OTA blobs that must not be linked, as archive members in the map. libesp_phy.a is not
# one of them: the IDF core links its phy_override.c for the temperature sensor, without radio code.
$forbiddenArchives = '(^|[/\\])(libnet80211|libpp|libphy|libcoexist|libbt[a-z0-9_]*|libespnow)\.a\(([^)]*)\)'
# An image with fewer symbols than this is not a real build (empty ELF, wrong file, nm failure)
$minimumSymbols = 1000

if (-not (Test-Path -LiteralPath $Elf)) {
    Write-Error "ELF not found: $Elf"
}
if (-not $Map) {
    $Map = [IO.Path]::ChangeExtension($Elf, '.map')
}
if (-not (Test-Path -LiteralPath $Map)) {
    Write-Error "linker map not found: $Map"
}

if (-not $Nm) {
    $onPath = Get-Command xtensa-esp32s3-elf-nm -ErrorAction SilentlyContinue
    if ($onPath) {
        $Nm = $onPath.Source
    }
    else {
        $toolsRoot = if ($env:IDF_TOOLS_PATH) { $env:IDF_TOOLS_PATH } else { Join-Path $HOME '.espressif' }
        $found = Get-ChildItem -Path (Join-Path $toolsRoot 'tools\xtensa-esp-elf') -Recurse `
            -Filter 'xtensa-esp32s3-elf-nm*' -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $found) {
            Write-Error 'xtensa-esp32s3-elf-nm not found; pass -Nm or set IDF_TOOLS_PATH'
        }
        $Nm = $found.FullName
    }
}

$global:LASTEXITCODE = 0
$symbols = @(& $Nm $Elf)
if ($LASTEXITCODE -ne 0) {
    Write-Error "nm failed with exit code $LASTEXITCODE"
}

$failed = $false
if ($symbols.Count -lt $minimumSymbols) {
    Write-Output "only $($symbols.Count) symbols in $Elf (expected at least $minimumSymbols): not a firmware build"
    $failed = $true
}

$prefixPattern = '^_?(' + (($forbiddenPrefixes | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')'
$hits = @()
$romHits = @()
$hasAppMain = $false
foreach ($line in $symbols) {
    # nm lines: "<address> <type> <name>" or "         U <name>" for undefined symbols
    $fields = $line.Trim() -split '\s+'
    $name = $fields[-1]
    $type = if ($fields.Count -ge 2) { $fields[-2] } else { '' }
    if ($name -ceq 'app_main' -and $type -cmatch '^[Tt]$') {
        $hasAppMain = $true
    }
    $forbidden = $name -match $prefixPattern
    if (-not $forbidden) {
        foreach ($s in $forbiddenSubstrings) {
            if ($name.ToLowerInvariant().Contains($s)) {
                $forbidden = $true
                break
            }
        }
    }
    if (-not $forbidden) {
        continue
    }
    # Type A are absolute ROM addresses from the linker scripts, not code in the image: reported, not fatal
    if ($type -ceq 'A') {
        $romHits += $line
    }
    else {
        $hits += $line
    }
}

if (-not $hasAppMain) {
    Write-Output "app_main is not defined in $($Elf): not a firmware build"
    $failed = $true
}
if ($hits) {
    Write-Output "forbidden symbols in $($Elf):"
    $hits | ForEach-Object { Write-Output "  $_" }
    $failed = $true
}

$members = @(Select-String -LiteralPath $Map -Pattern $forbiddenArchives -AllMatches |
    ForEach-Object { $_.Matches } | ForEach-Object { "$($_.Groups[2].Value).a($($_.Groups[3].Value))" } |
    Sort-Object -Unique)
if ($members) {
    Write-Output "forbidden archive members linked (from $($Map)):"
    $members | ForEach-Object { Write-Output "  $_" }
    $failed = $true
}

if ($romHits) {
    Write-Output "note: $($romHits.Count) ROM symbols (type A) with forbidden prefixes are ignored, e.g.:"
    $romHits | Select-Object -First 3 | ForEach-Object { Write-Output "  $_" }
}

if ($failed) {
    exit 1
}

Write-Output "no forbidden symbols or archives ($($symbols.Count) symbols in $Elf, map $Map)"
exit 0
