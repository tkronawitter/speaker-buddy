# speaker-timer

[![ci](https://github.com/tkronawitter/speaker-timer/actions/workflows/ci.yml/badge.svg)](https://github.com/tkronawitter/speaker-timer/actions/workflows/ci.yml)

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

## Hardware

- [Waveshare ESP32-S3-Touch-AMOLED-1.8](https://www.waveshare.com/esp32-s3-touch-amoled-1.8.htm),
  368 x 448 AMOLED with touch, used in landscape
- A USB-C cable to the presenting laptop

## Repository layout

| Path | Content |
| --- | --- |
| `laptop/` | Laptop program, C# / .NET 8. `SpeakerTimer.Core` holds the platform-neutral logic and its tests |
| `firmware/` | ESP-IDF firmware for the timer (planned) |
| `docs/protocol.md` | The serial protocol between timer and laptop |
| `.github/workflows/` | CI |

## Build and test the laptop logic

Requires the .NET 8 SDK.

```sh
dotnet test laptop/SpeakerTimer.sln
```

## License

[MIT](LICENSE)
