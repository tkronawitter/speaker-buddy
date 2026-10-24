# Plan: Speaker Timer, stage 1

**Status:** in-progress (approved by the owner 2026-10-24 18:45; the plan may change along the way)
**Last updated:** 2026-10-24
**Target:** github.com/tkronawitter/speaker-timer (`firmware/`, `laptop/`, `docs/`, `.github/`), Waveshare ESP32-S3-Touch-AMOLED-1.8 (V2) on the development PC `COM4`, the owner' Windows 11 laptop

## Goal

A small AMOLED timer on the lectern shows the elapsed talk time and colours its background by
how the speaker is doing against `## m:ss` marks in the PowerPoint speaker notes: green on plan,
red behind, purple ahead. It has to be ready for the the first talk on the talk date.

Approach: the timer owns the time `t` and runs alone without a laptop. A .NET 8 tray program on
the laptop reads PowerPoint read-only over COM, evaluates the marks and sends one JSON line per
second over USB serial. All evaluation logic lives in a platform-neutral library that is tested
on Ubuntu; the firmware stays a display plus clock, built with ESP-IDF, the Waveshare BSP and
LVGL 9.

The confirmed requirements (IDs L-, D-, S-, B-, R-, O-, A-) are in the private notes page
`IT-Infrastructure/IoT Devices/Speaker-Timer-Requirements` (German). This plan refers to them by
ID and does not restate them.

## Constraints

- Hard dates (A-01): go/no-go with a full dress rehearsal on the stage laptop with a second
  screen by **Thu the rehearsal date**; freeze from **Sun the freeze date**, nothing is flashed after; talk on
  **Tue the talk date**
- Scope tiers are fixed by the user (2026-10-24): core before the talk date, stretch only if time
  allows, everything else after the talk date Stretch work never delays a core milestone
- Firmware: ESP-IDF v5.5.5 (`%USERPROFILE%\esp\esp-idf-v5.5.5`), BSP
  `waveshare/esp32_s3_touch_amoled_1_8` 2.0.3, LVGL 9, all pinned in `dependencies.lock`;
  `CONFIG_BT_ENABLED=n`, no Wi-Fi init, no OTA, no TinyUSB, own partition table without OTA;
  secure boot and flash encryption stay off (A-04)
- Every flash of the board needs its own go from the owner. The factory image is archive only and is
  never flashed back
- Laptop program: C# .NET 8; logic in `net8.0`, adapter in `net8.0-windows`, late-bound
  `dynamic` COM, no `COMReference`, no trimming. Stage rules A-03 apply
- Public repo, MIT, English for code, README and commit messages (O-01, O-02)
- Work on `main`. GitHub repo, so the ADO develop/main rule does not apply
- the development PC is the development machine; the stage laptop is a separate Windows 11 machine with
  install rights

## Steps

Dates are "done by" targets. Steps marked **(the owner)** are manual tasks for the user; everything
else is development session work.

### Task 1: Repository skeleton and laptop CI (by Mon 26.10.)

1. [ ] Create `.gitignore` for .NET, ESP-IDF (`build/`, `managed_components/`, `sdkconfig`,
   `sdkconfig.old`) and CMake host builds. Expected: `git status` after a build shows no build
   output
2. [ ] Create `.editorconfig` (C#: file-scoped namespaces, 4 spaces; C: 4 spaces). Expected: file
   present
3. [ ] Create `laptop/SpeakerTimer.sln` with `laptop/src/SpeakerTimer.Core` (`net8.0`, class
   library) and `laptop/tests/SpeakerTimer.Core.Tests` (`net8.0`, xUnit, `coverlet.collector`).
   Expected: `dotnet test laptop/SpeakerTimer.sln` passes with one placeholder-free smoke test of
   a real type (for example `Mark` record equality)
4. [ ] Create `docs/protocol.md`: the S-02 lines (timer to laptop `{"v":1,"t":612,"run":true}`;
   laptop to timer `{"v":1,"c":"#00A000","d":90}`, `{"v":1,"L":2700}`,
   `{"v":1,"state":"ended"}`), 128-byte limit, drop rules, send cadence. Expected: the single
   source both sides implement against
5. [ ] Create `.github/workflows/ci.yml`, job `laptop` on `ubuntu-latest`, path filter
   `laptop/**` and the workflow file itself: `dotnet build -warnaserror`, `dotnet format
   --verify-no-changes`, `dotnet test --collect:"XPlat Code Coverage"`. Expected: green run on
   push
6. [ ] Replace the generated `README.md` with an English README: purpose, hardware, repo layout,
   status "work in progress, stage 1". Expected: renders on GitHub
7. [ ] Commit and push. Expected: CI `laptop` green on `main`

### Task 2: Evaluation logic in SpeakerTimer.Core (by Wed 28.10.)

Test first for every step: write the xUnit cases from the requirement text, see them fail, then
implement.

8. [ ] `MarkParser` (L-01): split on `\r`, `\n`, `\v`; a mark is a whole line `## m:ss`, minutes
   0 to 99, seconds 00 to 59; first mark per slide wins. Tests cover `## 10:00`, `## 0:30`,
   `## 99:59` (valid) and `## 10:75`, `## 10:00 Demo`, `## 1:05:00`, `##10:00` (rejected).
   Expected: all pass
9. [ ] `MarkSet` builder: input is a list of (slide index, hidden flag, notes text); hidden slides
   are ignored; a mark lower than an earlier one produces a warning and is dropped. Expected: tests
   for hidden slides and decreasing marks pass
10. [ ] `TalkLength` (L-07): `L` from argument if given, else the highest mark if it is at least
    5:00, else none; `tol = max(L / 45, 0:30)`; no marks means no evaluation. Expected: tests for
    45 min (1:00), 30 min (0:40), 15 min (0:30), 90 min (2:00), highest mark 4:00 without argument
    (no `L`)
11. [ ] `Evaluator` (B-01 to B-07 and the slide jump rules): state machine fed with `(t,
    slideIndex)`; a mark is reached the first time `slideIndex` is at or past its slide, evaluated
    once; going back re-evaluates nothing; jumping past marks reaches them; precedence B-06, then
    B-02 to B-04 on arrival, then B-05, then B-01; purple only while `t < m - tol`. Expected: the
    worked example from the requirements page (slide 15, `## 10:00`, arrivals at 10:40, 12:00,
    8:30 then 9:00, and 11:01 on slide 13) passes as a test, plus one test per rule
12. [ ] `Protocol` (S-02): serialize laptop lines, parse timer lines, drop lines that are broken,
    unknown, over 128 bytes or have `v` other than 1. Expected: round-trip and drop tests pass
13. [ ] `ColorScheme`: maps evaluation result to RGB (`neutral`, `green`, `red`, `purple`),
    defaults in one place so O-05 later only adds a config file. Expected: test pins the default
    values
14. [ ] Commit and push. Expected: CI `laptop` green, coverage report produced as an artifact

### Task 3: Firmware toolchain and board bring-up (by Thu 29.10.)

15. [x] ESP-IDF v5.5.5 installed on the development PC: source `%USERPROFILE%\esp\esp-idf-v5.5.5`, tools in
    `%USERPROFILE%\esp\.espressif` (`IDF_TOOLS_PATH`); `install.ps1 esp32s3` ended "All done!"
    on 2026-10-24. Shell: set `IDF_TOOLS_PATH`, dot-source `export.ps1`. Expected: `idf.py
    --version` prints `ESP-IDF v5.5.5`
16. [ ] Check that the factory backup is intact before the first flash:
    `Get-FileHash -Algorithm SHA256 "<private backup> Dateien\Elektronik\ESP32\waveshare-esp32-s3-touch-amoled-1.8\factory\factory-dump.bin"`
    and compare with the `.sha256` file next to it. Expected: identical hash. This is the safety
    net for step 20
17. [ ] Create the ESP-IDF project under `firmware/`: `CMakeLists.txt`, `main/main.c`,
    `main/idf_component.yml` pinning `waveshare/esp32_s3_touch_amoled_1_8: "2.0.3"` and LVGL 9,
    `sdkconfig.defaults` (`CONFIG_BT_ENABLED=n`, PSRAM octal, 16 MB flash, USB Serial/JTAG
    console; `CONFIG_ESP_WIFI_ENABLED=y` is an IDF default and links nothing while Wi-Fi is never
    initialised, the `nm` check in step 19 proves it), `partitions.csv` with `nvs`, `phy_init`, one `factory` app partition, no OTA.
    Expected: `idf.py set-target esp32s3` and `idf.py build` end with "Project build complete"
18. [ ] Commit `dependencies.lock` together with the project. Expected: lock file in git, A-04
    pinning satisfied. Reference versions from the BSP test build on 2026-10-24: LVGL 9.6.0~1,
    esp_lvgl_port 2.9.0, esp_lcd_co5300 2.2.0, cst816s 1.1.2, ft5x06 1.1.1, tca9554 2.0.3
19. [ ] Add `tools/check-image.ps1` and `tools/check-image.sh`: run `nm` from the IDF toolchain on
    `firmware/build/speaker_timer.elf` and fail if symbols match `esp_wifi_`, `esp_bt`, `btdm_`,
    `esp_ota_`, `tinyusb` or `tud_`. Expected: exit 0 on the current build. A-04 check: runs
    locally before every flash (steps 20, 28, 33 and any fix flashed during Task 7) until the CI firmware job (step 51) exists,
    then in CI as well (the owner, 2026-10-24)
20. [ ] **(the owner)** Go for the first flash of the `firmware/` project. Run `pwsh
    tools/check-image.ps1 firmware/build/speaker_timer.elf` (must exit 0), then `idf.py -p COM4
    flash monitor` with a minimal screen (solid colour plus "hello" text, touch coordinates
    logged). Expected: display shows the screen, touch logs coordinates, the BSP log names the
    touch chip (CO5300/CST816S = V2; BSP 2.0.3 ships only the CO5300 panel driver). The PCB is
    marked "Rev 1.1".
    Done outside the plan, 2026-10-24 after the go of 18:45: BOOT-mode test, `erase_flash` of the
    whole chip (removes xiaozhi and the factory remnants; the archive from step 16 is the only
    copy left), then the throwaway test app `%USERPROFILE%\esp\bsp-test` flashed (IDF v5.5.5,
    toolchain esp-14.2.0_20260121, `bsp_test.bin` 706 KB, SHA-256
    `6cccc1547e26b78e85ba0ad6b5154f6d164f947307d6c484784649e05d45ba31`, checked with `nm` by
    the dev session, not with `tools/check-image`). Result (about 18:55): BOOT-mode test PASS,
    archive SHA-256 matched, `erase_flash` OK (16.1 s), bsp-test flashed and hashes verified.
    Revision V2 verified by the serial log (`Touch CST816S 0x15 found`, `CST816S: IC id: 183`,
    probe `-> board V2`); PSRAM 8 MB octal OK; co5300 2.2.0 panel created. FAIL: display stays
    white, the app stops after the touch-probe line (no `Label shown`, no panic). Suspected,
    unverified: BSP 2.0.3 registers the QSPI CO5300 panel through `lvgl_port_add_disp_rgb`.
    Next: diagnostic image (step logging, lock timeout, heartbeat), flash waits for a go.
    Reset lessons for step 31 and the flash steps: after ROM download mode an RTS reset boots the
    app only after one power cycle (otherwise `boot:0x23`); the Windows `usbser` driver needs a
    dummy DTR write
21. [ ] Commit and push. Expected: `main` contains a buildable firmware project

### Task 4: Firmware clock and display (by Sun 01.11.)

22. [ ] Create component `firmware/components/timer_core/` in plain C without IDF headers:
    `clock.c` (start, pause, reset, `t` in seconds, `L`, blink decision for B-07, red from 60:00
    offline for D-06), `line.c` (S-02 parsing and the timer-to-laptop line, 128-byte limit),
    `format.c` (`mm:ss`, `+1:30`). Expected: compiles inside the IDF build
23. [ ] Create host tests in `firmware/test/host/` (`CMakeLists.txt`, a minimal assert runner, no
    PlatformIO) for `clock`, `line` and `format`. Expected: `cmake -S firmware/test/host -B
    build-host && cmake --build build-host && ctest --test-dir build-host` reports "100% tests
    passed"
24. [ ] Add CI job `firmware-host` on `ubuntu-latest`, path filter `firmware/**`, running step 23.
    Expected: green run
25. [ ] Create a font for the digits with `lv_font_conv` at the size that fills the landscape
    screen width for `88:88` (D-01), checked in as `firmware/main/fonts/digits_*.c` with the
    generating command in a comment. Expected: builds, glyphs `0-9 : + -` only
26. [ ] Implement `main/ui.c`: landscape layout, big `mm:ss`, small deviation line, full-screen
    background, "disconnected" symbol. Expected: builds
27. [ ] Implement `main/input.c`: short tap starts; long press pauses or resets (D-03), so a short
    tap never stops a running clock. Expected: builds
28. [ ] **(the owner)** Go for flash; `tools/check-image.ps1` exits 0 first; then test on the board: tap starts, long press pauses, long
    press again resets, time readable from 1.5 m. Expected: all four observed. Offline 60:00 rule
    is covered by the host test, not waited for
29. [ ] Commit and push. Expected: CI `firmware-host` green

### Task 5: Serial link, both sides (by Wed 04.11.)

30. [ ] Firmware `main/link.c`: read lines from USB Serial/JTAG, feed `timer_core/line.c`, apply
    `c`, `d`, `L`, `state`; send `{"v":1,"t":..,"run":..}` every second and on change; after 3 s
    without a valid line switch to offline display with the disconnected symbol, keep counting,
    keep blinking if `t > L` (D-04). Expected: builds, host tests still pass
31. [ ] Laptop `laptop/src/SpeakerTimer.App` (`net8.0-windows`), class `SerialLink`: find the port
    by VID `303A` PID `1001` (L-06), open with `DtrEnable = false`, `RtsEnable = false` (A-03),
    reconnect after unplug, read timer lines, write laptop lines. Expected: builds on Windows
32. [ ] Laptop test mode `speaker-timer.exe --demo`: cycles neutral, green, red, purple and a
    deviation every 3 s, sends `L` once. Expected: builds
33. [ ] **(the owner)** Go for flash; `tools/check-image.ps1` exits 0 first; then run `--demo`
    against the board on the development PC. Expected: colours
    and deviation change as sent; opening the port does not reset the board; unplugging the cable
    shows the disconnected symbol and the clock keeps running; replugging reconnects within a few
    seconds
34. [ ] Commit and push

### Task 6: PowerPoint adapter and tray program (by Sun 08.11.)

35. [ ] Class `PowerPointSource` in `SpeakerTimer.App`: `Marshal.GetActiveObject`-equivalent for
    .NET 8 (`[DllImport("oleaut32")] GetActiveObject`) on `PowerPoint.Application`, never starts
    PowerPoint; reads notes per slide, hidden flag (`SlideShowTransition.Hidden`), current
    `SlideShowWindows(1).View.Slide.SlideIndex`, slide show running or not, by polling once per
    tick; any COM exception skips the tick (A-03). `SlideShowWindows(1)` is ambiguous when more
    than one slide show runs: lock onto the presentation by `Presentation.FullName` when its slide
    show starts and pick that window on every tick, so a second deck or an embedded deck never
    switches the tracked presentation; test with two decks in slide show at once. Verify the object model calls against the Office
    VBA reference first, the requirement page marks them unverified. Expected: builds
36. [ ] Wire `Evaluator` to `PowerPointSource` and `SerialLink`: `t` comes from the timer line,
    the laptop sends state every second and on change, `L` once after connect and on change,
    `state: ended` when the slide show ends (L-03, L-05). Expected: builds
37. [ ] Tray shell (A-03): WinForms `ApplicationContext` with `NotifyIcon`, `OutputType WinExe` (no
    console window), menu entries "Stop and release port" (the kill switch, no global hotkey; on
    stage the cable pull per A-02 is the second way out) and "Open log"; no
    windows, balloons or toasts; log file under `%LOCALAPPDATA%\speaker-timer\logs`. Expected:
    starting the exe shows only a tray icon
38. [ ] Check mode `speaker-timer.exe --check deck.pptx` (L-09): reads the .pptx with
    `DocumentFormat.OpenXml` in `SpeakerTimer.Core` (no PowerPoint needed, testable on Ubuntu),
    prints marks per slide, near misses, hidden-slide marks and decreasing marks (OpenXml
    confirmed by the owner, 2026-10-24). Expected: unit
    test with a small fixture .pptx in `laptop/tests/fixtures/` passes in CI
39. [ ] Desk test on the development PC with a 5-slide test deck carrying marks: start timer by tap, start the
    slide show, page through on time, late and early. Expected: colours as in the worked example;
    PowerPoint never shows a dialog from the program
40. [ ] Commit and push

### Task 7: Stage laptop and rehearsal (by Thu the rehearsal date)

41. [ ] **(the owner)** Add `## m:ss` marks to the the first talk deck. Due before step 43
42. [ ] **(the owner)** Run `speaker-timer.exe --check <the first talk deck>`. Expected: no near misses,
    no decreasing marks, highest mark close to 30:00
43. [ ] **(the owner)** Install the .NET 8 Desktop Runtime on the stage laptop and copy the published
    program (`dotnet publish laptop/src/SpeakerTimer.App -c Release -r win-x64 --self-contained
    false`) to a local folder. Framework-dependent for the talk date; the self-contained single exe
    comes after the talk date (O-03). Expected: exe starts into the tray
44. [ ] **(the owner)** Desk rehearsal with the the first talk deck on the development PC (milestone 5 on the private notes
    page). Expected: colours behave as intended; fixes go back into Tasks 2 to 6
45. [ ] Tag the build that passed step 44 as `v0.1.0-rc1` and keep its `.bin` and exe next to the
    factory backup under `<private backup> Dateien\Elektronik\ESP32\waveshare-esp32-s3-touch-amoled-1.8\builds\v0.1.0-rc1\`.
    Expected: a known-good build exists before the rehearsal. This is the safety net for step 46
46. [ ] **(the owner)** Go/no-go: full dress rehearsal on the stage laptop with a second screen,
    presenter view, the timer on USB, the whole 30 minutes; pull the cable once mid-talk (A-02).
    Expected: timer keeps counting after the pull, the talk never shows a window from the program.
    Go means the rc build becomes `v0.1.0`; no-go means the stage runs on the offline timer only
47. [ ] Commit and push any fixes, tag `v0.1.0`

### Task 8: Freeze (Sun the freeze date) and talk (Tue the talk date)

48. [ ] Freeze: no flash, no code change to the stage build. The stage laptop keeps the tagged
    exe. Expected: board and laptop carry `v0.1.0`
49. [ ] **(the owner)** On site: fallback order offline timer, presenter view clock, backup timer; switch
    only by pulling the cable (A-02)

### Stretch, before the talk date only if every core step above is on time

50. [ ] Codecov upload for the `laptop` job with `CODECOV_TOKEN` and a README badge (R-03)
51. [ ] CI job `firmware-build` in the `espressif/idf:v5.5.5` container: `idf.py build`, run the
    `nm` check from step 20, upload `speaker_timer.bin` as artifact (R-02, A-04)

After the talk date (not planned here): `release.yml` with attestations and `SHA256SUMS` (R-07, R-09),
single-file exe (O-03), Dependabot (R-08), CSV log (L-08), brightness (D-05), O-04 to O-08.

## Rollback

- The `erase_flash` done before step 20 cannot be undone; the archived factory dump (step 16)
  stays archive only.
- Steps 20, 28, 33 (flash): the factory image is never restored. Rollback is flashing the last
  own build that worked: `idf.py -p COM4 flash` from the previous commit, or `esptool.py -p COM4
  write_flash 0x0 <merged .bin>` from the archived build. If the board stops enumerating, hold
  BOOT while plugging in to enter the ROM bootloader, then flash again. Step 16 confirms the
  archive before the first flash
- Steps 1 to 14, 22 to 27, 30 to 38 (code): `git revert <commit>`; nothing is deployed anywhere
  until a flash or a copy to the stage laptop
- Step 43 (stage laptop): delete the program folder; uninstall the .NET 8 Desktop Runtime if it
  was not there before
- Step 46 (rehearsal fails): stage runs on the offline timer alone (A-02); `v0.1.0-rc1` from step
  45 stays the last known-good build
- During the talk: pull the cable. The timer continues offline; PowerPoint is unaffected because
  the program only reads

## Verification

| Check | Command | Expected output |
| --- | --- | --- |
| Laptop logic | `dotnet test laptop/SpeakerTimer.sln` | `Passed!  - Failed: 0` |
| Laptop format | `dotnet format laptop/SpeakerTimer.sln --verify-no-changes` | exit 0, no output |
| Firmware host tests | `ctest --test-dir build-host` | `100% tests passed` |
| Firmware build | `idf.py build` in `firmware/` | `Project build complete` |
| Image provenance | `pwsh tools/check-image.ps1 firmware/build/speaker_timer.elf` | exit 0, `no forbidden symbols` |
| Pinning | `git ls-files firmware/dependencies.lock` | the path is listed |
| CI | GitHub Actions on `main` | `laptop` and `firmware-host` green |
| Deck | `speaker-timer.exe --check <deck>` | marks listed, `0 problems` |
| Board | steps 28, 33, 39, 46 | observed behaviour as stated per step |

## Unresolved Questions

None. All five were answered by the owner on 2026-10-24 at 18:43 and are folded into the steps:
`nm` check locally before every flash until step 51 (steps 19, 20), check mode via OpenXml
(step 38), kill switch as tray entry only (step 37), framework-dependent exe for the talk date (step 43),
`L` line `{"v":1,"L":2700}` confirmed (step 4).
