# Plan: Speaker Buddy, stage 1

**Status:** in-progress (approved by the owner 2026-10-24 18:45; the plan may change along the way)
**Last updated:** 2026-10-25
**Target:** github.com/tkronawitter/speaker-buddy (`firmware/`, `laptop/`, `docs/`, `.github/`), Waveshare ESP32-S3-Touch-AMOLED-1.8 (V2) on the development PC `COM4`, the owner' Windows 11 laptop

## Goal

A small AMOLED timer on the lectern shows the elapsed talk time and colours its background by
how the speaker is doing against `## m:ss` marks in the PowerPoint speaker notes. The colours are
instructions: green = keep going, red = ahead, talk slower, purple = behind, talk faster, orange
blinking = overtime. It has to be ready for the the first talk on the talk date.

Approach: the timer owns the time `t` and runs alone without a laptop. A .NET 8 tray program on
the laptop reads PowerPoint read-only over COM, evaluates the marks and sends one JSON line per
second over USB serial. All evaluation logic lives in a platform-neutral library that is tested
on Ubuntu; the firmware stays a display plus clock, built with ESP-IDF, the Waveshare BSP and
LVGL 9.

The confirmed requirements (IDs L-, D-, S-, B-, R-, O-, A-) are in the private notes page
`IT-Infrastructure/IoT Devices/Speaker-Buddy-Requirements` (German). This plan refers to them by
ID and does not restate them.

The project was renamed from "Speaker Timer" to "Speaker Buddy" on 2026-10-25 at 00:40 (the owner;
code in 7aa9b73). Commit hashes before the rename are kept as they were.

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
- Standing go from the owner for flashing over USB: `bsp-test` images since 2026-10-24 19:03,
  `firmware/` project images since 19:06. Conditions: `tools/check-image` exits 0 before every
  flash; `erase_flash` and eFuse writes always need a separate go; the A-01 freeze from the freeze date
  overrides the standing go. The factory image is archive only and is never flashed back.
  After every flash the board is restarted (`idf.py flash` resets it via RTS) and the report says
  so, with one status line read back as proof (the owner, 2026-10-24 22:15)
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

1. [x] Create `.gitignore` for .NET, ESP-IDF (`build/`, `managed_components/`, `sdkconfig`,
   `sdkconfig.old`) and CMake host builds. Expected: `git status` after a build shows no build
   output
2. [x] Create `.editorconfig` (C#: file-scoped namespaces, 4 spaces; C: 4 spaces). Expected: file
   present
3. [x] Create `laptop/SpeakerBuddy.sln` with `laptop/src/SpeakerBuddy.Core` (`net8.0`, class
   library) and `laptop/tests/SpeakerBuddy.Core.Tests` (`net8.0`, xUnit, `coverlet.collector`).
   Expected: `dotnet test laptop/SpeakerBuddy.sln` passes with one placeholder-free smoke test of
   a real type (for example `Mark` record equality)
4. [x] Create `docs/protocol.md`: the S-02 lines (timer to laptop `{"v":1,"t":612,"run":true}`;
   laptop to timer `{"v":1,"c":"#00A000","d":90}`, `{"v":1,"L":2700}`,
   `{"v":1,"state":"ended"}`), 128-byte limit, drop rules, send cadence. Expected: the single
   source both sides implement against
5. [x] Create `.github/workflows/ci.yml`, job `laptop` on `ubuntu-latest`, path filter
   `laptop/**` and the workflow file itself: `dotnet build -warnaserror`, `dotnet format
   --verify-no-changes`, `dotnet test --collect:"XPlat Code Coverage"`. Expected: green run on
   push
6. [x] Replace the generated `README.md` with an English README: purpose, hardware, repo layout,
   status "work in progress, stage 1". Expected: renders on GitHub
7. [x] Commit and push. Expected: CI `laptop` green on `main`

### Task 2: Evaluation logic in SpeakerBuddy.Core (by Wed 28.10.)

Test first for every step: write the xUnit cases from the requirement text, see them fail, then
implement.

8. [x] `MarkParser` (L-01): split on `\r`, `\n`, `\v`; a mark is a whole line `## m:ss`, minutes
   0 to 99, seconds 00 to 59; first mark per slide wins. Tests cover `## 10:00`, `## 0:30`,
   `## 99:59` (valid) and `## 10:75`, `## 10:00 Demo`, `## 1:05:00`, `##10:00` (rejected).
   Expected: all pass
   - [x] Follow-up (the owner, 2026-10-24 19:19): trim trailing whitespace before matching, so
     `## 10:00 ` is a mark; ` ## 10:00` (leading) and `## 10:00 Demo` stay rejected. Expected:
     new tests for both cases pass, CI `laptop` green. Done in 38390e4: spaces and tabs are
     trimmed; a trailing no-break space (U+00A0) stays invalid and check mode L-09 reports it.
     CI run 40115712254 green
9. [x] `MarkSet` builder: input is a list of (slide index, hidden flag, notes text); hidden slides
   are ignored; a mark lower than an earlier one produces a warning and is dropped. Expected: tests
   for hidden slides and decreasing marks pass
10. [x] `TalkLength` (L-07): `L` from argument if given, else the highest mark if it is at least
    5:00, else none; `tol = max(L / 45, 0:30)`; no marks means no evaluation. Expected: tests for
    45 min (1:00), 30 min (0:40), 15 min (0:30), 90 min (2:00), highest mark 4:00 without argument
    (no `L`)
    - [x] Follow-up (the owner, 2026-10-25 00:33-00:37): minimum tolerance 0:45, `tol = max(L / 45,
      0:45)` rounded down to whole seconds, `L` unknown gives 0:45. Expected tests: 45 min
      (1:00), 30 min (0:45, the first talk), 15 min (0:45), 90 min (2:00). Pushed in 57c345d, tests 206/206,
      CI run 40139694623 green
11. [x] `Evaluator` (B-01 to B-07 and the slide jump rules): state machine fed with `(t,
    slideIndex)`; a mark is reached the first time `slideIndex` is at or past its slide, evaluated
    once; going back re-evaluates nothing; jumping past marks reaches them; precedence B-06, then
    B-02 to B-04 on arrival, then B-05, then B-01; colours per the 2026-10-24 19:15 decision:
    within `tol` green, behind (`t - m > tol`, B-03, and B-06) purple, ahead red, red only while
    `t < m - tol`; overtime `t > L` (B-07) is its own state `overtime`. Expected: the worked
    example from the requirements page passes as a test (`L = 45:00`, `tol = 1:00`, slide 15
    `## 10:00`: arrival at 10:40 green, at 12:00 purple, at 8:30 red and green again at 9:00 on
    the same slide, 11:01 still on slide 13 purple per B-06, 45:01 overtime), plus one test per
    rule
    - [x] Follow-up (the owner, 2026-10-25 00:33-00:37): the deviation `d` follows the colour live
      instead of freezing at a mark. Green: no deviation line (`d` null); red: how far ahead,
      negative; purple: how far behind, positive, measured against the overdue next mark when
      B-06 applies; overtime unchanged. Exact `m:ss`, no rounding (30 s rounding discussed and
      dropped). Reason: the step 39 log showed the frozen `d` contradicting the colour (`t=90`
      green with `d=-38`, `t=211` purple with `d=-38`). Laptop side only. Expected: a test per
      colour case passes, board check by the owner. Pushed in 57c345d; green sends `d` null, so the
      firmware needs no change. Purple after a late arrival shows the lateness at arrival, frozen,
      and switches to the live value once the next mark is overdue (B-06); confirmed by the owner
      2026-10-25 00:46, each mark is an independent checkpoint.
      Board check PASS per the owner 2026-10-25 01:05 and 01:08: run 00:59:44-01:05:15 with
      34f0a86 (`--length 5`), app log `t=106` purple `d=46` live (next mark overdue), `t=138`
      purple `d=78`, `t=139` green `d` null, `t=226` purple `d=46` counting, `t=251` purple
      `d=71` frozen, `t=275` green `d` null, show end detected ("sah gut aus"). The red case was
      not reached in this run and is covered by `EvaluatorTests`. Static test pictures (purple
      `+1:10`, red `-0:55`, green without a line): "perfekt", readable and big enough. During
      overtime (`t > L`) the timer shows only orange and ignores the laptop colour, as designed.
      At the show end PowerPoint answered `RPC_E_SERVERCALL_RETRYLATER`; the tick was skipped,
      no crash (91cfc39 holds in practice)
12. [x] `Protocol` (S-02): serialize laptop lines, parse timer lines, drop lines that are broken,
    unknown, over 128 bytes or have `v` other than 1. Expected: round-trip and drop tests pass.
    Follow-up (dev, 2026-10-24, pushed in 49dc0f5): the protocol is flat JSON on both sides, no
    nested values, no backslash escapes, no duplicate names; the laptop drops the same lines as
    the timer
13. [x] `ColorScheme`: maps evaluation result to RGB (`neutral` white on black, `onPlan` green,
    `ahead` red = talk slower, `behind` purple = talk faster), defaults in one place so O-05 later
    only adds a config file. No orange here: the timer decides overtime alone from `t > L` and
    draws orange, blinking, black digits itself (confirmed by the owner 2026-10-24 19:19). Expected: test pins the default
    values
14. [x] Commit and push. Expected: CI `laptop` green, coverage report produced as an artifact

### Task 3: Firmware toolchain and board bring-up (by Thu 29.10.)

15. [x] ESP-IDF v5.5.5 installed on the development PC: source `%USERPROFILE%\esp\esp-idf-v5.5.5`, tools in
    `%USERPROFILE%\esp\.espressif` (`IDF_TOOLS_PATH`); `install.ps1 esp32s3` ended "All done!"
    on 2026-10-24. Shell: set `IDF_TOOLS_PATH`, dot-source `export.ps1`. Expected: `idf.py
    --version` prints `ESP-IDF v5.5.5`
16. [x] Check that the factory backup is intact before the first flash:
    `Get-FileHash -Algorithm SHA256 "<private backup> Dateien\Elektronik\ESP32\waveshare-esp32-s3-touch-amoled-1.8\factory\factory-dump.bin"`
    and compare with the `.sha256` file next to it. Expected: identical hash. This is the safety
    net for step 20
17. [x] Create the ESP-IDF project under `firmware/`: `CMakeLists.txt`, `main/main.c`,
    `main/idf_component.yml` pinning `waveshare/esp32_s3_touch_amoled_1_8: "2.0.3"` and LVGL 9,
    `sdkconfig.defaults` (`CONFIG_BT_ENABLED=n`, PSRAM octal, 16 MB flash, USB Serial/JTAG
    console; `CONFIG_ESP_WIFI_ENABLED=y` cannot be switched off in IDF v5.5.5 (promptless bool,
    default `y` if `SOC_WIFI_SUPPORTED`) and links nothing while Wi-Fi is never initialised, so
    A-04 "no Wi-Fi in the image" is proven by the `nm` check in step 19 only), `partitions.csv` with `nvs`, `phy_init`, one `factory` app partition, no OTA.
    Expected: `idf.py set-target esp32s3` and `idf.py build` end with "Project build complete"
18. [x] Commit `dependencies.lock` together with the project. Expected: lock file in git, A-04
    pinning satisfied. Reference versions from the BSP test build on 2026-10-24: LVGL 9.6.0~1,
    esp_lvgl_port 2.9.0, esp_lcd_co5300 2.2.0, cst816s 1.1.2, ft5x06 1.1.1, tca9554 2.0.3
19. [x] Add `tools/check-image.ps1` and `tools/check-image.sh`: run `nm` from the IDF toolchain on
    `firmware/build/speaker_buddy.elf` and fail if symbols match `esp_wifi_`, `esp_bt`, `btdm_`,
    `esp_ota_`, `tinyusb` or `tud_`. Expected: exit 0 on the current build. A-04 check: runs
    locally before every flash (steps 20, 28, 33 and any fix flashed during Task 7) until the CI firmware job (step 51) exists,
    then in CI as well (the owner, 2026-10-24).
    As built (dev, 2026-10-24), deviating from the list above: type-A symbols (absolute ROM
    addresses from the ROM linker scripts, for example `btdm_*`) are skipped; OTA is matched only
    by the update functions (`esp_ota_begin`, `esp_ota_write`, `esp_ota_end`,
    `esp_ota_set_boot_partition`, `esp_https_ota`), because the IDF core always links
    `esp_ota_get_running_partition`; Bluetooth prefixes are `esp_bt_`, `esp_ble_`,
    `esp_bluedroid_`, `nimble_port_`, `btdm_`; `esp_wifi_`, `tud_` and `tinyusb` stay forbidden.
    A negative test with a fake `nm` fails as intended
20. [x] First flash of the `firmware/` project under the standing go; the owner observes the board.
    Run `pwsh
    tools/check-image.ps1 firmware/build/speaker_buddy.elf` (must exit 0), then `idf.py -p COM4
    flash monitor` with a minimal screen (solid colour plus "hello" text, touch coordinates
    logged). Expected: display shows the screen, touch logs coordinates, the BSP log names the
    touch chip (CO5300/CST816S = V2; BSP 2.0.3 ships only the CO5300 panel driver). The PCB is
    marked "Rev 1.1".
    Result 2026-10-24 (flashed under the standing go): serial log PASS, partition table `nvs`,
    `phy_init`, `factory` 3 MB, no OTA; `Touch CST816S` found; bring-up screen shown.
    `speaker_buddy.bin` is 0xa9a80 bytes, 78 % of the 3 MB factory partition free. Open for
    the owner: visual check (green background, white "hello", landscape) and one touch tap.
    Partial, per the owner 2026-10-24 evening: the 150 px font "looks good so far", seen before the
    fixed-width change 13a3e52 (step 26).
    Final look PASS per the owner 2026-10-25 00:32 ("passt"), seen with `--demo` on the clean
    cf4de62 firmware: fixed-width 150 px time, 76 px deviation line, all backgrounds including
    the overtime orange.
    Done outside the plan, 2026-10-24 after the go of 18:45: BOOT-mode test, `erase_flash` of the
    whole chip (removes xiaozhi and the factory remnants; the archive from step 16 is the only
    copy left), then the throwaway test app `%USERPROFILE%\esp\bsp-test` flashed (IDF v5.5.5,
    toolchain esp-14.2.0_20260121, `bsp_test.bin` 706 KB, SHA-256
    `6cccc1547e26b78e85ba0ad6b5154f6d164f947307d6c484784649e05d45ba31`, checked with `nm` by
    the dev session, not with `tools/check-image`). Result (about 18:55): BOOT-mode test PASS,
    archive SHA-256 matched, `erase_flash` OK (16.1 s), bsp-test flashed and hashes verified.
    Revision V2 verified by the serial log (`Touch CST816S 0x15 found`, `CST816S: IC id: 183`,
    probe `-> board V2`); PSRAM 8 MB octal OK; co5300 2.2.0 panel created. FAIL: display stays
    white, the app stops after the touch-probe line (no `Label shown`, no panic). First
    suspicion (BSP 2.0.3 registers the QSPI panel through `lvgl_port_add_disp_rgb`) is not the
    cause per the dev session's diagnosis: `app_main` used the panel IO concurrently with the
    LVGL flush, the brightness call ran outside the LVGL lock. Third image sets brightness under
    the lock: PASS (the owner 19:03 "black screen and 00:00 in the middle", 19:05 landscape
    confirmed; heartbeat `step=done`, `heap_ok=1`, `free_int 240915`, `free_psram 8237152`).
    Root cause verified by the step log: `bsp_display_brightness_set` called
    `esp_lcd_panel_io_tx_param` on the QSPI panel IO while the LVGL task flushed and blocked
    forever. Lesson for Task 4: panel commands only under `bsp_display_lock`. Watch list: BSP
    2.0.3 registers the QSPI panel through `lvgl_port_add_disp_rgb` (no visible effect so far)
    Reset lessons for step 31 and the flash steps: after ROM download mode an RTS reset boots the
    app only after one power cycle (otherwise `boot:0x23`); the Windows `usbser` driver needs a
    dummy DTR write
21. [x] Commit and push. Expected: `main` contains a buildable firmware project

### Task 4: Firmware clock and display (by Sun 01.11.)

22. [x] Create component `firmware/components/timer_core/` in plain C without IDF headers:
    `clock.c` (start, pause, reset, `t` in seconds, `L`; overtime state for B-07 when `t > L`
    and for D-06 offline from 60:00: orange, blinking, black digits), `line.c` (S-02 parsing and the timer-to-laptop line, 128-byte limit),
    `format.c` (`mm:ss`, `+1:30`). Expected: compiles inside the IDF build.
    Status 2026-10-24: written together with steps 23-24, pushed in e4382a8; host tests 3/3 in
    WSL. Compiling `timer_core` inside the IDF build verified with 5582c33 (task 4b), which also
    adds `timer_core/view` (pure C display decision) with `test_view`; host tests 4/4
23. [x] Create host tests in `firmware/test/host/` (`CMakeLists.txt`, a minimal assert runner, no
    PlatformIO) for `clock`, `line` and `format`. Expected: `cmake -S firmware/test/host -B
    build-host && cmake --build build-host && ctest --test-dir build-host` reports "100% tests
    passed". Note (2026-10-24): the development PC had no host C compiler; the owner approved installing one at
    19:21 (dev session); installed in WSL (gcc 13.3.0, cmake, make), the host tests run there
24. [x] Add CI job `firmware-host` on `ubuntu-latest`, path filter `firmware/**`, running step 23.
    Expected: green run
25. [x] Create a font for the digits with `lv_font_conv` at the size that fills the landscape
    screen width for `88:88` (D-01), checked in as `firmware/main/fonts/digits_*.c` with the
    generating command in a comment. Expected: builds, glyphs `0-9 : + -` only. Installing
    `lv_font_conv` via `npx` on the development PC approved by the owner 2026-10-24 19:21.
    Done 2026-10-24 in 4928975: the dev session's classifier refused `npx lv_font_conv`, so
    the owner ran it himself at 21:38. Result `firmware/main/fonts/digits_150.c`, Montserrat Medium
    150 px, 4 bpp, glyphs `+ - 0-9 :`; `88:88` is about 420 px of the 448 px width. Build,
    `check-image` 0 and flash verified by the dev session on the development PC; how it reads from 1.5 m is
    checked in steps 20 and 28
26. [x] Implement `main/ui.c`: landscape layout, big `mm:ss`, small deviation line, full-screen
    background, "disconnected" symbol. Expected: builds
27. [x] Implement `main/input.c`: short tap starts; long press pauses or resets (D-03), so a short
    tap never stops a running clock. Expected: builds.
    Status for 26 and 27 (pushed in 5582c33): built, `check-image` clean, flashed under the
    standing go; serial log `Touch CST816S 0x15 found`, `timer ready, tap to start`, 12 s
    without panic; `speaker_buddy.bin` 0xb6810 bytes, 76 % of the factory partition free.
    Follow-up 13a3e52 (the owner asked at 21:42): fixed-width elapsed time, every digit cell as wide
    as the widest digit, and times of 100 minutes and more are scaled
    down to fit. Build, `check-image` 0 and flash verified by the dev session; the look on screen
    and the scale-down after 100 minutes are not verified yet (step 28; the scale-down check is
    in the backlog as issue #2). The scale-down lives in
    `main/ui.c` (`set_time_text`, LVGL transform), outside `timer_core`, so no host test covers
    it; it is a display check only.
    Regression from 13a3e52 (the owner, 22:06): long press no longer works. Cause confirmed by
    the owner at 22:07 (long press on an empty corner works, a touch on the digits does nothing):
    the new time container is clickable by LVGL default and swallows touches on the digit row,
    so they never reach the screen. Fixed in 618991f: `lv_obj_set_clickable(s_time, false)`
    clears `LV_OBJ_FLAG_CLICKABLE` on the container. Build without warnings, `check-image` 0 and
    flash verified by the dev session; step 28 retests long press on the digits
28. [x] Flash under the standing go, `tools/check-image.ps1` exits 0 first; the owner observes the
    board and tests: tap starts, long press pauses, long
    press again resets, time readable from 1.5 m. Expected: all four observed. Offline 60:00 rule
    is covered by the host test, not waited for.
    Result 2026-10-24 22:10 (per the owner, after 618991f): PASS, tap and long press on the digits
    work. The display check of the scale-down from 100 minutes (step 26) moved to the backlog
    by the owner 2026-10-25 01:09: GitHub issue #2, milestone `v2`
29. [x] Commit and push. Expected: CI `firmware-host` green

### Task 5: Serial link, both sides (by Wed 04.11.)

30. [x] Firmware `main/link.c`: read lines from USB Serial/JTAG, feed `timer_core/line.c`, apply
    `c`, `d`, `L`, `state`; send `{"v":1,"t":..,"run":..}` every second and on change; after 3 s
    without a valid line switch to offline display with the disconnected symbol, keep counting,
    keep blinking if `t > L` (D-04). Expected: builds, host tests still pass
31. [x] Laptop `laptop/src/SpeakerBuddy.App` (`net8.0-windows`), class `SerialLink`: find the port
    by VID `303A` PID `1001` (L-06), open with `DtrEnable = false`, `RtsEnable = false` (A-03),
    reconnect after unplug, read timer lines, write laptop lines. Expected: builds on Windows
32. [x] Laptop test mode `speaker-buddy.exe --demo`: cycles neutral, green, red, purple and a
    deviation every 3 s, sends `L` once (a short `L` so overtime orange shows within the demo). Expected: builds.
    Deviation in the code (found by the coordinator 2026-10-24 22:30): `DemoSequence.LengthSeconds`
    is `45 * 60` (`laptop/src/SpeakerBuddy.Core/DemoSequence.cs:13`), so overtime orange does not
    show in the demo. the owner decided 2026-10-25 00:00: change the code, short `L` of about 60 s.
    Done in 8424179: `DemoSequence.LengthSeconds = 60`, new test, `dotnet test` 204/204. Board
    check per the owner 2026-10-25 00:06: overtime orange blink after 1:00 seen, the first view of
    overtime on the board
    Status for 30-32 (2026-10-24, pushed in aa7d2c6 firmware link plus `timer_core/session`, and
    d7cdab2 laptop `SerialLink`, `TimerPort`, `LineFramer`, `DemoSequence`, App with `--demo`): `dotnet test` 150/150, host tests 5/5, `check-image` clean (6997 symbols). Verified by
    the dev session on the development PC without the owner: opening `COM4` with DTR/RTS low does not reset the
    board, status lines about once per second, `L` applied, a broken line is ignored, "laptop
    lost" about 3 s after the last line, `--demo` finds `COM4` via VID/PID and interface
    `MI_00` and sends colours. Still unverified: the Ubuntu CI build of the `net8.0-windows` App
    (`EnableWindowsTargeting`) is verified by CI run 40126609656, green
33. [x] Flash under the standing go, `tools/check-image.ps1` exits 0 first; then run `--demo`
    against the board on the development PC while the owner observes the board. Expected: colours
    and deviation change as sent; opening the port does not reset the board; unplugging the cable
    shows the disconnected symbol and the clock keeps running; replugging reconnects within a few
    seconds. Open for the owner: the visual check and the unplug/replug test (the serial side is
    already verified, see step 32).
    Result 2026-10-24 22:14 (per the owner): FAIL. After boot `00:00` shows on black; about 1 s
    later the whole screen goes black and stays black. The firmware keeps running: status lines
    `t=65..73` arrive, colours from `--demo` are received, link and touch work. Cause unknown,
    the dev session is investigating on the development PC. Unplug/replug of the serial link passes: the
    coordinator's log shows "disconnected", then "connected on COM4" and status lines again
    within seconds. The disconnected symbol could not be seen because the screen was black;
    that part is retested after the display fix.
    Diagnosis 22:28-22:29: a diagnostic image driven by the dev session's `phases.py` (nothing,
    then `L` plus neutral, green, green with `+45`, clock stopped) showed every phase as expected
    per the owner, no black screen.
    Cause found in part (22:44): reproduced 3 of 3 times by replugging while `--demo` runs, 0
    times without the demo. A diagnostic image with an RTC boot counter showed `boots=2`,
    reason 11 (`ESP_RST_USB`) in the black state: after power-on, the control-line toggling
    while the exe opens the port resets the chip a second time over USB Serial/JTAG, despite
    `DtrEnable = false` and `RtsEnable = false` (step 31). After that extra reset the panel stays
    black while the app runs and renders normally. Not confirmed: that the reset lands during
    panel init. The board has no panel reset line (`BSP_LCD_RST` is not connected). The extra
    reset also sets the talk clock back to 0.
    Side finding: on connect the exe first receives stale status lines the board queued while
    the port was closed (`t=110..118` before the live `t=182`). Fixed in 0f5d21f "laptop: drop
    the timer's backlog on connect"; board test PASS 2026-10-25 00:08 (first `t` is live, no
    jump).
    State at the end of 2026-10-24 (step 33 still FAIL, nothing committed after ff4612d; per
    the owner plus the dev logs):
    - Extra reset: a replug while the laptop program runs resets the chip again about 0.7 s
      after power-on (`boots=2`, reason 11, `prev_stage=1`, during panel init); a replug with no
      program running does not (`boots=1`, reason 1). First laptop fix, DTR on at open, did not
      help: .NET `Open` clears DTR first. Second fix `SafeLineState` (Win32 sets the lines off
      in a safe order before `Open`) is built, not yet proven on a replug
    - Dark panel: after any chip reset the panel often stays dark for about 10 s, although the
      app runs about 0.6 s after the reset. LVGL sends two full frames at boot and then nothing
      while the screen is static, so the panel shows nothing until the next full redraw; a
      stopped clock with no laptop traffic can stay black indefinitely (likely the 22:27 and
      22:59 cases). Not proven: why the first frames do not show
    - Counter checks: 14 port opens on a running board without a reset gave 0 black screens;
      10 forced resets gave 0 permanent black screens, each followed by about 10 s dark
    - Reading the panel registers back over QSPI returns 0 on this board and is not usable; its
      diagnostic image caused one black screen itself (artefact, 23:04)
    - The board runs a dev diagnostic image, not a committed build
    Cause found 2026-10-24 23:24-23:29 (per the owner plus the dev logs), replacing the redraw
    explanation above: one RTS reset makes the panel permanently black, on demand (23:24, 23:29).
    Full redraws and a sleep-out, display-on and brightness kick did not bring it back (23:24).
    The diagnostic trigger `L:2` did (23:29, `00:00` back): TCA9554 expander pins 0, 1, 2 and 6
    low for 20 ms, then high (the sequence from Waveshare's own demos), then a full panel init
    and redraw. BSP 2.0.3 never touches the expander (`BSP_LCD_RST` not connected), so after a
    chip reset the panel gets no hardware reset. Not known yet: which of those pins is the panel
    reset.
    Fix direction: pulse the expander pins before panel init at every boot (firmware). The
    redraw-after-boot candidate is refuted and dropped. `SafeLineState` on the laptop stays a
    separate fix for the extra reset after a replug.
    Panel fix PASS, pushed in a507838 (`main/main.c`): `reset_panel()` runs before
    `bsp_display_start` and pulls TCA9554 pins 0, 1, 2 and 6 low for 20 ms, then high, then
    waits 150 ms. All four pins are kept as in Waveshare's demos; which one is the panel reset
    will not be narrowed down. Proof: 10 RTS resets without laptop traffic (23:32-23:34), 0
    black screens per the owner 23:35; before the fix 1 of 1 black.
    Laptop fix PASS, pushed in e168210: new `SafeLineState`, called by `SerialLink.TryOpen`
    before `Open`, turns the control lines off in a safe order through Win32 (App csproj
    `AllowUnsafeBlocks`). Replug test 3 of 3 with `boots=1`, reason 1 (23:45-23:48, replugs by
    the owner, reads by the dev session); before the fix 3 of 3 had `boots=2`, reason 11. The board
    runs the clean a507838 image again.
    Retest PASS (per the owner 2026-10-24 23:55, "genau so"): `--demo` 23:54:39-23:55:09 on the
    a507838 image with the e168210 laptop program; colours cycled with the deviation shown, and
    after the demo stopped: black background, white time, USB symbol with an X. With the serial
    reconnect and the replug fix above, step 33 passes
34. [x] Commit and push

### Task 6: PowerPoint adapter and tray program (by Sun 08.11.)

35. [x] Class `PowerPointSource` in `SpeakerBuddy.App`: `Marshal.GetActiveObject`-equivalent for
    .NET 8 (`[DllImport("oleaut32")] GetActiveObject`) on `PowerPoint.Application`, never starts
    PowerPoint; reads notes per slide, hidden flag (`SlideShowTransition.Hidden`), current
    `SlideShowWindows(1).View.Slide.SlideIndex`, slide show running or not, by polling once per
    tick; any COM exception skips the tick (A-03). `SlideShowWindows(1)` is ambiguous when more
    than one slide show runs: lock onto the presentation by `Presentation.FullName` when its slide
    show starts and pick that window on every tick, so a second deck or an embedded deck never
    switches the tracked presentation; test with two decks in slide show at once. Verify the object model calls against the Office
    VBA reference first, the requirement page marks them unverified. Expected: builds
36. [x] Wire `Evaluator` to `PowerPointSource` and `SerialLink`: `t` comes from the timer line,
    the laptop sends state every second and on change, `L` once after connect and on change,
    `state: ended` when the slide show ends (L-03, L-05). Expected: builds
37. [x] Tray shell (A-03): WinForms `ApplicationContext` with `NotifyIcon`, `OutputType WinExe` (no
    console window), menu entries "Stop and release port" (the kill switch, no global hotkey; on
    stage the cable pull per A-02 is the second way out) and "Open log"; no
    windows, balloons or toasts; log file under `%LOCALAPPDATA%\speaker-buddy\logs`. Expected:
    starting the exe shows only a tray icon
38. [x] Check mode `speaker-buddy.exe --check deck.pptx` (L-09): reads the .pptx with
    `DocumentFormat.OpenXml` in `SpeakerBuddy.Core` (no PowerPoint needed, testable on Ubuntu),
    prints marks per slide, near misses, hidden-slide marks and decreasing marks (OpenXml
    confirmed by the owner, 2026-10-24). Expected: unit
    test with a small fixture .pptx in `laptop/tests/fixtures/` passes in CI.
    Status for 35-38 (2026-10-24, pushed in 34a9243 Core `DeckReader`, `DeckCheck`,
    `TalkController`, OpenXml 3.1.0, fixture .pptx, and 8992345 App `PowerPointSource`,
    `TalkRunner`, `TrayContext`, `FileLog`, modes tray, `--check` and `--demo`, README laptop
    section): tests 203/203. `--check` on the fixture works (exit 1, 3 problems).
    NOT verified: the live COM path (show detection, `FullName` lock, slide index, show end, two
    decks at once). On the development PC a deck opened via automation has 0 windows and `SlideShowSettings` is
    null, so no slide show could be started by script; root cause unknown after 2 attempts.
    Superseded 2026-10-25: in step 39 run 1 the coordinator started the show over COM and
    the live path worked (see step 39).
    Step 39, with the owner starting the show by hand, is the first real run. The Ubuntu CI build of
    the WinForms App builds on Ubuntu (CI run 40126609656 green, jobs `laptop` and
    `firmware-host`)
39. [x] Desk test on the development PC with a 5-slide test deck carrying marks: start timer by tap, start the
    slide show, page through on time, late and early. Expected: colours as in the worked example;
    PowerPoint never shows a dialog from the program. This is also the first test of the live COM
    path from steps 35-36, including a second deck in slide show at the same time (`FullName`
    lock) and the end of the show (`state: ended`).
    Test deck (2026-10-25): `<private backup> Dateien\Elektronik\ESP32\speaker-timer-desk-test.pptx`,
    5 slides, marks 1:00, 2:00, 3:00 and 4:30; `--check` with `--length 5` reports 0 problems,
    `tol` 0:30 (0:45 once the step 10 follow-up is in).
    Run 1, 2026-10-25 00:16:57-00:22:44, unattended at the owner' request (00:13): a test-only
    diagnostic image with clock autostart (not committed; the clean image was flashed back
    afterwards), tray app 91cfc39 with `--length 5`, show started over COM and paged by a
    coordinator script. App log: show detected, 4 marks, highest 270 s; slide 2 at `t=77` green
    `d=17`; slide 3 at `t=82` red `d=-38`, green at `t=90` once `t` reached `m - tol`; `t=211`
    purple (late for the next mark); slide 4 at `t=261` purple `d=81`; slide 5 at `t=286` green
    `d=16`; show end detected. PASS for show detection, marks, evaluation and show end, the first
    live run of the COM path.
    Fixed before the run: 91cfc39 "laptop: survive a busy PowerPoint" (PowerPoint rejected a
    call at show start, `RPC_E_CALL_REJECTED` surfaced as `MissingMemberException` and crashed
    the app).
    Per the owner 2026-10-25 00:24, who watched the board after all: "orange came. powerpoint did
    not show messages. worked like i would have wanted it." So the board view during the show,
    including overtime orange, and no PowerPoint dialog are confirmed.
    Not tested by decision: two decks in slide show at once (`FullName` lock). the owner
    2026-10-25 00:27: "won't be happening"; moved to the backlog as GitHub issue #1, milestone
    `v2` (after the talk date)
40. [x] Commit and push

### Task 7: Stage laptop and rehearsal (by Thu the rehearsal date)

41. [ ] **(the owner)** Add `## m:ss` marks to the the first talk deck. Due before step 43
42. [ ] **(the owner)** Run `speaker-buddy.exe --check <the first talk deck>`. Expected: no near misses,
    no decreasing marks, highest mark close to 30:00
43. [ ] **(the owner)** Put the signed program on the stage laptop. On the development PC: `pwsh
    tools/publish-laptop.ps1` (one self-contained `laptop/publish/speaker-buddy.exe`, about
    70 MB, no .NET runtime needed on the stage laptop; O-03 pulled forward in 768dcd4), then
    `pwsh tools/sign.ps1` (a570b83). Expected: exe starts into the tray.
    The stage laptop is the owner' private device. Signing (the owner, 2026-10-25 01:11, replacing
    "unsigned" from 01:09): self-signed code signing certificate `CN=Speaker Buddy`, thumbprint
    `8E1E2DC50FFA1A5E5709FF6EDAF2DE142C3FC8C7`, valid until 2028-12-30, private key not
    exportable in `Cert:\CurrentUser\My` on the development PC. The public file is
    `<private backup>\Elektronik\ESP32\speaker-buddy-codesign.cer` in private storage; importing it on the
    laptop is a Machine-Sync-Queue row. Verified on the development PC: published, signed with an RFC 3161
    timestamp, the signed exe runs `--check` on the desk-test deck (0 problems, `tol` 0:45);
    `Get-AuthenticodeSignature` there reports `UnknownError`, as expected without the root.
    So that Windows does not block it:
    - Import the `.cer` (Machine-Sync-Queue row), then `Get-AuthenticodeSignature
      <exe>` shows `Valid` with the thumbprint above
    - Copy the exe without a browser download (private storage sync or USB stick), or run
      `Unblock-File <exe>` after copying
    - Start the exe once by hand before the rehearsal
    - Check Smart App Control: Windows Security > App & browser control > Smart App Control, or
      `Get-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy -Name VerifiedAndReputablePolicyState`
      (0 = off, 1 = on, 2 = evaluation)
    - If Smart App Control is on: per the dev session (not tested), a self-signed certificate
      does not satisfy it even when imported; the options are turning Smart App Control off or
      a CA certificate (issue #3). the owner decides then
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
51. [x] CI job `firmware-build` in the `espressif/idf:v5.5.5` container: `idf.py build`, run the
    `nm` check from step 20, upload `speaker_buddy.bin` as artifact (R-02, A-04).
    Pushed in 7072ada "ci: build the firmware in the ESP-IDF container"; CI run 40141818016
    green: jobs `laptop`, `firmware-host` and `firmware-build`, the image check ran inside the
    container, artifact `firmware` uploaded
52. [x] Pulled forward from after the talk date (O-03): `speaker-buddy.exe` as one self-contained file,
    `pwsh tools/publish-laptop.ps1`, no .NET runtime on the stage laptop. Pushed in 768dcd4;
    verified on the development PC (published, signed, `--check` runs)

After the talk date (not planned here): `release.yml` with attestations and `SHA256SUMS` (R-07, R-09),
Dependabot (R-08), CSV log (L-08), brightness (D-05), O-04 to O-08.
Backlog on GitHub, milestone `v2`: issue #1, test the presentation lock with two slide shows at
once (from step 39); issue #2, display check of the time scale-down from 100 minutes (from steps
26 and 28); issue #3, a publicly trusted code signing certificate (options: SignPath Foundation,
free for open source with a release history; Certum Open Source, about 25-69 EUR a year; a CA
OV certificate, about 100 USD a year and up; Azure Artifact Signing is not available to
individuals in Germany).

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
| Laptop logic | `dotnet test laptop/SpeakerBuddy.sln` | `Passed!  - Failed: 0` |
| Laptop format | `dotnet format laptop/SpeakerBuddy.sln --verify-no-changes` | exit 0, no output |
| Firmware host tests | `ctest --test-dir build-host` | `100% tests passed` |
| Firmware build | `idf.py build` in `firmware/` | `Project build complete` |
| Image provenance | `pwsh tools/check-image.ps1 firmware/build/speaker_buddy.elf` | exit 0, `no forbidden symbols` |
| Pinning | `git ls-files firmware/dependencies.lock` | the path is listed |
| CI | GitHub Actions on `main` | `laptop`, `firmware-host` and `firmware-build` green |
| Signature | `Get-AuthenticodeSignature laptop/publish/speaker-buddy.exe` | `Valid` on a machine that trusts the certificate, `UnknownError` on the development PC |
| Deck | `speaker-buddy.exe --check <deck>` | marks listed, `0 problems` |
| Board | steps 28, 33, 39, 46 | observed behaviour as stated per step |

## Unresolved Questions

None open.

Answered by the owner on 2026-10-25 at 00:46: purple after a late arrival keeps the lateness at the
arrival (as built in 57c345d) and does not count up. Reason in his words: a mark is always the
time where he wants to be at that slide; earlier marks do not matter once he reaches it. He sets
marks on slides where he can talk more or less, to win time back, not on every slide. The README
section "How to set marks" describes this.

Answered by the owner on 2026-10-24 at 22:30 and 2026-10-25 at 00:00, the dev interpretations
from Task 4 and the questions from the board tests:

1. Overtime blink alternates orange background with black digits and black background with
   orange digits: keep as implemented.
2. Digit colour white or black chosen by the background's luma: keep.
3. Long press threshold 1.0 s: keep.
4. Disconnected symbol, USB icon with an X, top right: keep.
5. Deviation line in Montserrat 28: no, far too small (22:30). New: about half the height of the
   time, about 70 px, as a generated font like step 25. Done in cf4de62 (`digits_76`,
   Montserrat Medium 76 px); visual check PASS per the owner 2026-10-25 00:06, readable.
6. `--demo` sent `L` = 45:00, so overtime never showed in the demo: yes, `--demo` sends a short
   `L` (about 60 s) as step 32 planned. Done in 8424179 (`DemoSequence.LengthSeconds = 60`).
7. Black panel after an extra USB reset (step 33): superseded 2026-10-24 23:29, the panel gets
   a hardware reset through the TCA9554 expander at every boot (a507838).

Answered by the owner on 2026-10-24 at 19:21: install `lv_font_conv` via `npx` (step 25) and a host
C compiler (step 23) on the development PC; the dev session installs them.

Answered by the owner on 2026-10-24 at 19:19, the five dev interpretations from Task 2:

1. Deviation `d` freezes at arrival of the last mark: confirmed as implemented. Replaced
   2026-10-25: `d` follows the colour live (step 11 follow-up).
2. `L` unknown (no argument, highest mark below 5:00): `tol` = 0:30: confirmed. Replaced
   2026-10-25: 0:45 (step 10 follow-up).
3. Changed: trailing spaces after `## m:ss` are allowed and trimmed before matching; leading
   whitespace and text after the time stay invalid. Code change needed in `MarkParser` (step 8).
4. Overtime orange decided and drawn by the timer only, laptop `ColorScheme` has no orange:
   confirmed (step 13).
5. `tol` rounded down to whole seconds: confirmed.

Answered by the owner on 2026-10-24 at 18:43, the first five questions, folded into the steps:
`nm` check locally before every flash until step 51 (steps 19, 20), check mode via OpenXml
(step 38), kill switch as tray entry only (step 37), framework-dependent exe for the talk date (step 43),
`L` line `{"v":1,"L":2700}` confirmed (step 4).
