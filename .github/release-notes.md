Speaker Buddy @TAG@, built by the release workflow from this tag.

## Files

| File | What it is |
| --- | --- |
| `speaker_buddy-merged.bin` | Complete timer firmware, flashed at address 0x0 |
| `speaker_buddy.bin`, `bootloader.bin`, `partition-table.bin`, `flash_args` | The same firmware in parts, with the offsets `idf.py` uses |
| `speaker-buddy.exe` | Laptop program for Windows x64, one self-contained file, no .NET install needed |
| `SHA256SUMS` | SHA-256 of every file above |

Flash the merged image with esptool 5 or later:

```sh
esptool --chip esp32s3 write-flash 0x0 speaker_buddy-merged.bin
```

## Not signed

`speaker-buddy.exe` in this release is **not code-signed**: the signing key never leaves the
machine it was created on, so the release workflow cannot sign. Windows SmartScreen may warn
when the file comes from the internet. Signed copies are made locally with `tools/sign.ps1`.

Check a download against `SHA256SUMS` before use:

```powershell
Get-FileHash speaker-buddy.exe -Algorithm SHA256
```
