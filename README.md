# <img src="assets/logo/speaker-buddy.svg" alt="Speaker Buddy logo" width="48" align="absmiddle"> Speaker Buddy

[![ci](https://github.com/tkronawitter/speaker-buddy/actions/workflows/ci.yml/badge.svg)](https://github.com/tkronawitter/speaker-buddy/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/tkronawitter/speaker-buddy/graph/badge.svg)](https://codecov.io/gh/tkronawitter/speaker-buddy)

A small timer on the lectern that shows how long you have been talking and colours its screen by
how the talk is going against time marks in your PowerPoint speaker notes: green on plan, red when
you are ahead (talk slower), purple when you are behind (talk faster). Past the planned length it
blinks orange.

Needs: a Windows laptop with PowerPoint, and a
[Waveshare ESP32-S3-Touch-AMOLED-1.8](https://www.waveshare.com/esp32-s3-touch-amoled-1.8.htm)
board with a USB-C cable.

**Status:** pre-1.0 (v0.x). Usable; behaviour and the serial protocol may still change.

## How it works

You put a line such as `## 10:00` into the speaker notes of a slide: you want to reach that slide
ten minutes into the talk. During the slide show, the laptop program reads PowerPoint (it only
reads, it never changes the deck), compares the time at which each marked slide comes up with its
mark, and sends the colour to the timer over USB once per second.

The timer owns the clock. If the laptop program stops, the timer keeps counting on its own, with a
"disconnected" symbol and without colours, so the talk never depends on the laptop program. The
USB cable is also the timer's only power: unplugged, it switches off, and after plugging it back in
it starts again at 00:00, stopped.

## Hardware

- [Waveshare ESP32-S3-Touch-AMOLED-1.8](https://www.waveshare.com/esp32-s3-touch-amoled-1.8.htm),
  368 x 448 AMOLED with touch, used in landscape
- A USB-C cable to the presenting laptop

## Install on the presenting laptop

1. Download `speaker-buddy.exe` and `SHA256SUMS` from the
   [latest release](https://github.com/tkronawitter/speaker-buddy/releases). It is one file and
   needs no .NET installation.
2. Check the download in PowerShell; the hash must match the line for `speaker-buddy.exe` in
   `SHA256SUMS`:

   ```powershell
   Get-FileHash .\speaker-buddy.exe -Algorithm SHA256
   ```

3. Unblock it, so Windows does not treat it as a file from the internet:

   ```powershell
   Unblock-File .\speaker-buddy.exe
   ```

4. Start it once by hand. The release exe is not code-signed: if SmartScreen warns, choose
   "More info", then "Run anyway". If Smart App Control is switched on, expect it to block the
   exe; then turn Smart App Control off.
5. Plug in the timer and run the colour test once:

   ```powershell
   .\speaker-buddy.exe --demo | Out-Host
   ```

   Close the tray program first ("Stop and release port"): only one program can use the timer's
   port at a time. The test also sets a talk length of one minute, so a running clock turns orange
   after 1:00. Stop the test with Ctrl+C.

To put a release firmware on the board, flash `speaker_buddy-merged.bin` with esptool 5 or
later (older versions spell the command `write_flash`):

```sh
esptool --chip esp32s3 write-flash 0x0 speaker_buddy-merged.bin
```

## How to set marks

A mark says "this is where I want to be when this slide comes up". It is not a schedule for every
slide. Put marks on the few slides where you can talk a little more or a little less: a demo you
can shorten, an example you can skip, a story you can stretch. Those are the places where you can
win time back or spend time you have, so those are the places where you want to know how you are
doing.

Each mark stands on its own. When you reach a marked slide, the timer compares the elapsed time
with that mark only; what earlier marks said does not matter any more. If you were behind at
10:00 and are on time at 20:00, the slide at 20:00 is green.

- Write the mark on its own line in the speaker notes: `## 10:00`, two hashes, a space, then
  minutes (0 to 99) and seconds
- One mark per slide counts, the first one in its notes. One mark every few minutes is enough; put
  the last one close to the end of the talk
- Marks on hidden slides are ignored
- Tell the timer how long the talk is with a line `## session length 45:00` in the notes of any
  visible slide; if there are several, the first counts. Without it, the highest mark counts as the length if it is at least 5:00. `--length`
  when starting the program overrides both
- Green means within the tolerance of the mark: the talk length divided by 45, at least 0:45. A
  45-minute talk allows 1:00 either way, a 60-minute talk 1:20

Check a deck before the talk:

```powershell
.\speaker-buddy.exe --check deck.pptx | Out-Host
```

It lists the marks per slide, the talk length and the tolerance, and reports lines that look like
marks but are not (such as `## 10:75` or `##10:00`), marks on hidden slides, marks that go
backwards, and session length lines that look wrong (such as `## session lenght 60:00`), sit on
hidden slides or appear more than once. It needs no PowerPoint. Exit code 0 means no problems, 1 means problems were reported,
2 means the deck could not be read or the options were wrong.

## During the talk

Start the laptop program before the talk; it sits in the notification area and waits for the
slide show. Nothing starts the clock by itself:

- Tap the screen to start the clock
- Hold for 1 second to pause it; tap to go on
- Hold for 1 second while paused to reset it to 00:00

| Screen | Meaning |
| --- | --- |
| Logo, "Speaker Buddy" and the version | Startup screen, about 2 seconds or until you tap |
| Black | No slide show, or no mark reached yet. When a show that ran past its length ends, the orange stops and the clock keeps counting; with `--length` the timer stays orange past that length |
| Green, no deviation | On plan |
| Red, with a deviation such as `-0:45` | Ahead: talk slower |
| Purple, with a deviation such as `+1:30` | Behind: talk faster |
| Orange, blinking | Over the planned length |
| USB symbol with an X | No data from the laptop for 3 seconds; the clock keeps counting |

Without the laptop the timer switches to blinking orange at 60:00.

## If something goes wrong

- **USB symbol with an X:** the laptop program has stopped or the cable is loose. The clock keeps
  counting; start the program again, it finds the timer on its own
- **Colours do not match your notes after editing them:** the program reads the notes when the
  slide show starts. End the show and start it again
- **The program misbehaves:** right-click its tray icon and choose "Stop and release port". The
  timer keeps counting; PowerPoint is not affected
- **Log:** "Open log" in the tray menu, or `%LOCALAPPDATA%\speaker-buddy\logs`
- Keep a second timer running as a backup

## Laptop program

`speaker-buddy.exe` runs on Windows with PowerPoint. Started without options it sits in the
notification area, follows the slide show and drives the timer. Its icon is grey while it waits
for the timer and in colour when the timer is connected. The tray menu has "Open log" and "Stop
and release port".

With more than one slide show running, the program follows one and stays with it: at its start
the first show it finds, later the first show that starts. When the followed show ends, every
show still running is ignored until it ends; a show started after that is followed, the same deck
again or another one. A second deck shown during the talk, for a demo for example, never changes
the marks or the length on the timer. The log names each ignored show.

| Command (PowerShell) | Purpose |
| --- | --- |
| `.\speaker-buddy.exe` | Starts in the notification area |
| `.\speaker-buddy.exe --length 45` | Starts in the notification area with a fixed talk length, in minutes or `m:ss` |
| `.\speaker-buddy.exe --port COM4` | Uses this serial port instead of finding the timer by its USB ID; also works with `--demo` |
| `.\speaker-buddy.exe --rehearsal` | Starts in the notification area and writes a rehearsal log, see below |
| `.\speaker-buddy.exe --check deck.pptx` | Checks the marks of a deck, see "How to set marks" |
| `.\speaker-buddy.exe --demo` | Sends a colour test pattern to the timer |
| `.\speaker-buddy.exe --help` | Lists the options |

The program is a Windows application, so PowerShell does not wait for it. Append `| Out-Host` to
`--check`, `--demo` and `--help` to see their output before the prompt returns.

### Rehearsal log

With `--rehearsal` (it combines with `--length` and `--port`) the program writes one CSV file per
slide show into the log folder, `%LOCALAPPDATA%\speaker-buddy\logs`, named after the time of its
first row and the deck, for example `rehearsal-2026-10-25-143005-mytalk.csv`. The log names the file when
the first row is written. Each slide change adds a row, going back a slide too, once the timer has
reported its time:

```text
time,slide,mark_s,t_s,deviation_s,colour
14:30:05,1,,3,,neutral
14:32:05,2,60,123,63,purple
```

| Column | Meaning |
| --- | --- |
| `time` | Clock time of the slide change |
| `slide` | Slide number in the deck |
| `mark_s` | The slide's mark in seconds, empty if it has none |
| `t_s` | Elapsed time on the timer in seconds |
| `deviation_s` | The deviation line of the timer: seconds behind plan (positive, purple) or ahead (negative, red), empty when green or neutral |
| `colour` | `neutral`, `green`, `red` or `purple`, as the timer showed it |

Times are in seconds so a spreadsheet can sort and subtract them. The file is standard CSV with a
comma as separator; Excel with German settings puts it into one column on a double-click, so import
it there with Data > From Text/CSV. A new show, or a new start of the program,
always starts a new file. The 30-day cleanup of the log folder leaves rehearsal files alone.

## For developers

### Repository layout

| Path | Content |
| --- | --- |
| `laptop/` | Laptop program, C# / .NET 8. `SpeakerBuddy.Core` holds the platform-neutral logic and its tests, `SpeakerBuddy.App` is the Windows program (`speaker-buddy.exe`) |
| `firmware/` | ESP-IDF firmware for the timer. `components/timer_core` holds the clock, protocol and display logic in plain C, with host tests in `test/host` |
| `docs/protocol.md` | The serial protocol between timer and laptop program |
| `tools/` | `check-image.ps1` / `check-image.sh`, `publish-laptop.ps1`, `sign.ps1`, `make-icons.ps1` (builds the `.ico` files from the logo) |
| `assets/logo/` | Logo as SVG masters and the app and tray icons |
| `.github/workflows/` | CI and the release workflow |

### Build and test the laptop logic

Requires the .NET 8 SDK.

```sh
dotnet test laptop/SpeakerBuddy.sln
```

### Build and flash the firmware

Requires ESP-IDF v6.1. The component versions are pinned in `firmware/dependencies.lock`. Test
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

`check-image` must exit 0 before every flash. The firmware must contain no radio code: it fails
if the image or its linker map contains Wi-Fi, Bluetooth, over-the-air update or TinyUSB code.

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
Control. Copy the exe without a browser download, or run `Unblock-File` on it after copying.

### Releases

Pushing a tag such as `v0.1.0` runs `.github/workflows/release.yml`: it runs the firmware host
tests and the laptop tests, builds the firmware, runs `check-image` on it, publishes the
single-file exe and creates a GitHub release with `SHA256SUMS`. A tag with a hyphen, such as
`v0.1.0-rc1`, becomes a pre-release. Firmware and laptop program report their version from
`git describe`. The exe in a release is not code-signed, because the signing key stays on the
machine it was created on.

### Dependency updates

Dependabot (`.github/dependabot.yml`) opens a pull request each week for the GitHub Actions and
one for the NuGet packages of the laptop program. It does not cover the firmware: the ESP-IDF
component manager is not a Dependabot ecosystem, so check `firmware/dependencies.lock` by hand
once a month and before a release.

Re-resolve the components within the ranges in `firmware/main/idf_component.yml` (for example
`lvgl/lvgl` `>=9.6.0,<10`), from an ESP-IDF v6.1 shell:

```sh
cd firmware
idf.py update-dependencies
git diff -- dependencies.lock
```

No diff means nothing to do. With a diff, build, run the host tests, check the image, test on the
board, then commit the new `dependencies.lock` and name the new versions in the commit message.
Commit the lock together with the update: CI and the release workflow fail when a build changes
`dependencies.lock` (step "Dependencies unchanged").

`idf.py update-dependencies` does not move the exact pins: ESP-IDF `==6.1.0` and the board
package `waveshare/esp32_s3_touch_amoled_1_8` `==2.0.3`. Look for newer versions on
[components.espressif.com](https://components.espressif.com) and in the
[ESP-IDF releases](https://github.com/espressif/esp-idf/releases). Raising a pin is a change to
`idf_component.yml` with its own board test. A new ESP-IDF version also moves the
`espressif/idf` image tag and digest in `.github/workflows/ci.yml` and `release.yml`, and the
version named in "Build and flash the firmware".

### Branch rules

A ruleset protects `main`: no force push, no deletion, and the CI checks `laptop`, `laptop-app`,
`firmware-host` and `firmware-build` must pass before a pull request can be merged. Repository
admins can bypass it. CI runs only for changes under `laptop/`, `firmware/`, `tools/` or to
`ci.yml` itself, so a pull request that touches nothing else, for example a Dependabot update of
the actions in `release.yml` or a change to the docs, never gets these checks and needs an admin
to merge it.

### Protocol

The timer and the laptop program talk in JSON lines over USB serial; see
[docs/protocol.md](docs/protocol.md).

## License

[MIT](LICENSE)
