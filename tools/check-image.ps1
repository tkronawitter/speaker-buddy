# Fails if the firmware ELF links Wi-Fi, Bluetooth, OTA or TinyUSB code (requirement A-04).
# Usage: pwsh tools/check-image.ps1 [firmware/build/speaker_timer.elf] [-Nm <path to nm>]
# Without -Nm, xtensa-esp32s3-elf-nm is taken from PATH or from the ESP-IDF tools directory.
param(
    [string] $Elf = (Join-Path $PSScriptRoot '..\firmware\build\speaker_timer.elf'),
    [string] $Nm
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

if (-not (Test-Path -LiteralPath $Elf)) {
    Write-Error "ELF not found: $Elf"
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
$symbols = & $Nm $Elf
if ($LASTEXITCODE -ne 0) {
    Write-Error "nm failed with exit code $LASTEXITCODE"
}

$prefixPattern = '^_?(' + (($forbiddenPrefixes | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')'
$hits = foreach ($line in $symbols) {
    # nm lines: "<address> <type> <name>" or "         U <name>" for undefined symbols
    $fields = $line.Trim() -split '\s+'
    $name = $fields[-1]
    # Type A are absolute ROM addresses from the linker scripts, not code in the image
    if ($fields.Count -ge 2 -and $fields[-2] -ceq 'A') {
        continue
    }
    if ($name -match $prefixPattern) {
        $line
        continue
    }
    foreach ($s in $forbiddenSubstrings) {
        if ($name.ToLowerInvariant().Contains($s)) {
            $line
            break
        }
    }
}

if ($hits) {
    Write-Output "forbidden symbols in $($Elf):"
    $hits | ForEach-Object { Write-Output "  $_" }
    exit 1
}

Write-Output "no forbidden symbols ($($symbols.Count) symbols checked in $Elf)"
exit 0
