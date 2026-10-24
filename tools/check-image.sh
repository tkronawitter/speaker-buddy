#!/usr/bin/env sh
# Fails if the firmware ELF links Wi-Fi, Bluetooth, OTA or TinyUSB code (requirement A-04).
# Usage: tools/check-image.sh [firmware/build/speaker_buddy.elf]
# Uses $NM if set, else xtensa-esp32s3-elf-nm from PATH (present in the espressif/idf image).
set -eu

elf="${1:-$(dirname "$0")/../firmware/build/speaker_buddy.elf}"
nm="${NM:-xtensa-esp32s3-elf-nm}"

if [ ! -f "$elf" ]; then
    echo "ELF not found: $elf" >&2
    exit 2
fi

symbols="$("$nm" "$elf")"
count="$(printf '%s\n' "$symbols" | wc -l | tr -d ' ')"

# Same rules as check-image.ps1: name prefixes after an optional underscore, plus "tinyusb"
# anywhere. Type A symbols are absolute ROM addresses, not code in the image. OTA is matched by
# its update functions only; the IDF core links esp_ota_get_running_partition itself.
hits="$(printf '%s\n' "$symbols" | awk '$(NF-1) != "A" { print $NF "\t" $0 }' \
    | grep -E -i '^_?(esp_wifi_|esp_bt_|esp_ble_|esp_bluedroid_|nimble_port_|btdm_|tud_|esp_ota_begin|esp_ota_write|esp_ota_end|esp_ota_set_boot_partition|esp_https_ota)|^[^	]*tinyusb' \
    | cut -f2- || true)"

if [ -n "$hits" ]; then
    echo "forbidden symbols in $elf:"
    printf '%s\n' "$hits" | sed 's/^/  /'
    exit 1
fi

echo "no forbidden symbols ($count symbols checked in $elf)"
