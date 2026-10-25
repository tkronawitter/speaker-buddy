#!/usr/bin/env sh
# Fails if the firmware links Wi-Fi, Bluetooth, OTA or TinyUSB code (requirement A-04). This detects
# linked code by symbol names (nm) and by the archive members in the linker map; it cannot see code
# that hides under other names, so it detects, it does not prove.
# Usage: tools/check-image.sh [firmware/build/speaker_buddy.elf [map]]
# Uses $NM if set, else xtensa-esp32s3-elf-nm from PATH (present in the espressif/idf image).
# Without a map argument, the .map next to the ELF is used.
set -eu

elf="${1:-$(dirname "$0")/../firmware/build/speaker_buddy.elf}"
map="${2:-${elf%.elf}.map}"
nm="${NM:-xtensa-esp32s3-elf-nm}"
# An image with fewer symbols than this is not a real build (empty ELF, wrong file, nm failure)
minimum_symbols=1000

if [ ! -f "$elf" ]; then
    echo "ELF not found: $elf" >&2
    exit 2
fi
if [ ! -f "$map" ]; then
    echo "linker map not found: $map" >&2
    exit 2
fi

symbols="$("$nm" "$elf")"
count="$(printf '%s\n' "$symbols" | grep -c . || true)"
failed=0

if [ "$count" -lt "$minimum_symbols" ]; then
    echo "only $count symbols in $elf (expected at least $minimum_symbols): not a firmware build"
    failed=1
fi

if ! printf '%s\n' "$symbols" | awk '$NF == "app_main" && ($(NF-1) == "T" || $(NF-1) == "t") { found = 1 } END { exit !found }'; then
    echo "app_main is not defined in $elf: not a firmware build"
    failed=1
fi

# Same rules as check-image.ps1: name prefixes after an optional underscore, plus "tinyusb"
# anywhere. OTA is matched by its update functions only; the IDF core links
# esp_ota_get_running_partition itself. Type A symbols are absolute ROM addresses from the linker
# scripts, not code in the image: reported, not fatal.
matches="$(printf '%s\n' "$symbols" | awk '{ print $(NF-1) "\t" $NF "\t" $0 }' \
    | grep -E -i '^[^	]*	(_?(esp_wifi_|esp_bt_|esp_ble_|esp_bluedroid_|nimble_port_|btdm_|tud_|esp_ota_begin|esp_ota_write|esp_ota_end|esp_ota_set_boot_partition|esp_https_ota)|[^	]*tinyusb)' || true)"
hits="$(printf '%s\n' "$matches" | grep -v '^A	' | cut -f3- | grep . || true)"
rom="$(printf '%s\n' "$matches" | grep '^A	' | cut -f3- | grep . || true)"

if [ -n "$hits" ]; then
    echo "forbidden symbols in $elf:"
    printf '%s\n' "$hits" | sed 's/^/  /'
    failed=1
fi

# Radio and OTA blobs that must not be linked, as archive members in the map. libesp_phy.a is not
# one of them: the IDF core links its phy_override.c for the temperature sensor, without radio code.
members="$(grep -E -o '(^|[/\\])(libnet80211|libpp|libphy|libcoexist|libbt[a-z0-9_]*|libespnow)\.a\([^)]*\)' "$map" \
    | sed 's#^.*[/\\]##' | sort -u || true)"
if [ -n "$members" ]; then
    echo "forbidden archive members linked (from $map):"
    printf '%s\n' "$members" | sed 's/^/  /'
    failed=1
fi

if [ -n "$rom" ]; then
    echo "note: $(printf '%s\n' "$rom" | grep -c .) ROM symbols (type A) with forbidden prefixes are ignored, e.g.:"
    printf '%s\n' "$rom" | head -n 3 | sed 's/^/  /'
fi

if [ "$failed" -ne 0 ]; then
    exit 1
fi

echo "no forbidden symbols or archives ($count symbols in $elf, map $map)"
