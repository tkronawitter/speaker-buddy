# Speaker Buddy

[![ci](https://github.com/tkronawitter/speaker-buddy/actions/workflows/ci.yml/badge.svg)](https://github.com/tkronawitter/speaker-buddy/actions/workflows/ci.yml)

A small display on the lectern that shows the elapsed talk time and colours its background by how
the talk is going against time marks in the PowerPoint speaker notes: green on plan, purple when
behind (talk faster), red when ahead (talk slower). Past the planned length it blinks orange.

**Status:** work in progress, stage 1. Nothing here is ready to use yet.

## How it works

You put a line such as `## 10:00` into the speaker notes of a slide: that slide should be reached
ten minutes into the talk. During the slide show, a small program on the laptop reads PowerPoint
(read-only), compares the time at which each marked slide is reached with its mark, and sends
the resulting colour to the timer over USB once per second.

The timer owns the clock. Unplug the cable and it keeps counting on its own, so the talk never
depends on the laptop program.

## How to set marks

A mark says "this is where I want to be when this slide comes up". It is not a schedule for
every slide. Put marks on the few slides where you can talk a little more or a little less: a
demo you can shorten, an example you can skip, a story you can stretch. Those are the places
where you can win time back or spend time you have, so those are the places where you want to
know how you are doing.

Each mark stands on its own. When you reach a marked slide, the timer compares the elapsed time
with that mark only; what earlier marks said does not matter any more. If you were behind at
10:00 and are on time at 20:00, the slide at 20:00 is green.

- One mark every few minutes is enough. Put the last mark close to the end of the talk
- Write the mark on its own line in the speaker notes: `## 10:00`, two hashes, a space, then
  minutes and seconds
- Green means within the tolerance of the mark: a 45-minute talk allows 1:00 either way, shorter
  talks at least 0:45
- Without `--length`, the highest mark is taken as the talk length if it is at least 5:00
- Marks on hidden slides are ignored

Check a deck before the talk:

```sh
speaker-buddy --check deck.pptx
```

It lists the marks per slide and reports likely typos, marks on hidden slides and marks that go
backwards.

## Hardware

- [Waveshare ESP32-S3-Touch-AMOLED-1.8](https://www.waveshare.com/esp32-s3-touch-amoled-1.8.htm),
  368 x 448 AMOLED with touch, used in landscape
- A USB-C cable to the presenting laptop

## Repository layout

| Path | Content |
| --- | --- |
| `laptop/` | Laptop program, C# / .NET 8. `SpeakerBuddy.Core` holds the platform-neutral logic and its tests, `SpeakerBuddy.App` is the Windows program (`speaker-buddy.exe`) |
| `firmware/` | ESP-IDF firmware for the timer. `components/timer_core` holds the clock, protocol and display logic in plain C, with host tests in `test/host` |
| `docs/protocol.md` | The serial protocol between timer and laptop |
| `tools/` | `check-image.ps1` / `check-image.sh`: fail if the firmware image links Wi-Fi, Bluetooth, OTA updates or TinyUSB |
| `.github/workflows/` | CI |

## Build and test the laptop logic

Requires the .NET 8 SDK.

```sh
dotnet test laptop/SpeakerBuddy.sln
```

## Build and flash the firmware

Requires ESP-IDF v5.5.5. The component versions are pinned in `firmware/dependencies.lock`. Test
the plain C logic on the host first; this needs CMake and a C compiler (Linux or WSL):

```sh
cmake -S firmware/test/host -B build-host
cmake --build build-host
ctest --test-dir build-host
```

Build the image from an ESP-IDF shell, check it, then flash it (replace `COM4` with the board's
port):

```sh
cd firmware
idf.py build
pwsh ../tools/check-image.ps1 build/speaker_buddy.elf
idf.py -p COM4 flash
```

`check-image` must exit 0 before every flash.

## Laptop program

`speaker-buddy.exe` runs on Windows with PowerPoint. Started without options it sits in the
notification area, follows the slide show and drives the timer; "Stop and release port" in its
menu ends it. It logs to `%LOCALAPPDATA%\speaker-buddy\logs`.

| Command | Purpose |
| --- | --- |
| `speaker-buddy --check deck.pptx` | Lists the time marks of a deck and reports likely typos, marks on hidden slides and decreasing marks, without PowerPoint |
| `speaker-buddy --demo` | Sends a colour test pattern to the timer |
| `speaker-buddy --length 45` | Sets the talk length instead of taking the highest mark |

From PowerShell, append `| Out-Host` to `--check` and `--demo` so the prompt waits for the output.

### Publish and sign

```sh
pwsh tools/publish-laptop.ps1
pwsh tools/sign.ps1
```

`publish-laptop.ps1` writes one self-contained `laptop/publish/speaker-buddy.exe` (about 70 MB),
so the presenting laptop needs no .NET runtime. `sign.ps1` signs it with a code signing
certificate from `Cert:\CurrentUser\My` (found by subject, or pass `-Thumbprint`) and an RFC 3161
timestamp, then checks the signature. The certificate is self-signed for now: Windows shows the
signature as valid only on machines that trust it, and it is not expected to satisfy Smart App
Control.
Copy the exe without a browser download, or run `Unblock-File` on it after copying.

## Releases

Pushing a tag such as `v0.1.0` runs `.github/workflows/release.yml`: it builds the firmware,
runs `check-image` on it, runs the laptop tests, publishes the single-file exe and creates a
GitHub release with `SHA256SUMS`. A tag with a hyphen, such as `v0.1.0-rc1`, becomes a
pre-release. Firmware and laptop program report their version from `git describe`. The exe in a
release is not code-signed, because the signing key stays on the machine it was created on.

## License

[MIT](LICENSE)
