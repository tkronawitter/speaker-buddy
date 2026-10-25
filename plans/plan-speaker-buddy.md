# Plan: Speaker Buddy, stage 1

**Status:** in-progress (approved by the owner 2026-10-24 18:45; the plan may change along the way)
**Last updated:** 2026-10-25
**Target:** github.com/tkronawitter/speaker-buddy (`firmware/`, `laptop/`, `docs/`, `.github/`), Waveshare ESP32-S3-Touch-AMOLED-1.8 (V2) on the development PC `COM4`, the owner's Windows 11 laptop

## Goal

A small AMOLED timer on the lectern shows the elapsed talk time and colours its background by
how the speaker is doing against `## m:ss` marks in the PowerPoint speaker notes. The colours are
instructions: green = keep going, red = ahead, talk slower, purple = behind, talk faster, orange
blinking = overtime. It has to be ready for the owner's first talk with it.

Approach: the timer owns the time `t` and runs alone without a laptop. A .NET 8 tray program on
the laptop reads PowerPoint read-only over COM, evaluates the marks and sends one JSON line per
second over USB serial. All evaluation logic lives in a platform-neutral library that is tested
on Ubuntu; the firmware stays a display plus clock, built with ESP-IDF, the Waveshare BSP and
LVGL 9.

The confirmed requirements (IDs L-, D-, S-, B-, R-, O-, A-) are in the owner's
private requirements notes (German). This plan refers to them by
ID and does not restate them.

The project was renamed from "Speaker Timer" to "Speaker Buddy" on 2026-10-25 at 00:40 (the owner;
code in 82316f3). Commit hashes before the rename are kept as they were.

## Constraints

- Hard dates (A-01): go/no-go with a full dress rehearsal on the stage laptop with a second
  screen by the **go/no-go date**; **freeze** a few days before the talk, nothing is flashed
  after; then the **first talk**
- Scope tiers are fixed by the user (2026-10-24): core before the first talk, stretch only if time
  allows, everything else after the first talk. Stretch work never delays a core milestone
- Firmware: ESP-IDF v5.5.5 (`%USERPROFILE%\esp\esp-idf-v5.5.5`), BSP
  `waveshare/esp32_s3_touch_amoled_1_8` 2.0.3, LVGL 9, all pinned in `dependencies.lock`;
  `CONFIG_BT_ENABLED=n`, no Wi-Fi init, no OTA, no TinyUSB, own partition table without OTA;
  secure boot and flash encryption stay off (A-04)
- Standing go from the owner for flashing over USB: `bsp-test` images since 2026-10-24 19:03,
  `firmware/` project images since 19:06. Conditions: `tools/check-image` exits 0 before every
  flash; `erase_flash` and eFuse writes always need a separate go; the A-01 freeze before the talk
  overrides the standing go. The factory image is archive only and is never flashed back.
  After every flash the board is restarted (`idf.py flash` resets it via RTS) and the report says
  so, with one status line read back as proof (the owner, 2026-10-24 22:15)
- Laptop program: C# .NET 8; logic in `net8.0`, adapter in `net8.0-windows`, late-bound
  `dynamic` COM, no `COMReference`, no trimming. Stage rules A-03 apply
- Public repo, MIT, English for code, README and commit messages (O-01, O-02)
- Work on `main`. GitHub repo, so the ADO develop/main rule does not apply
- The development PC holds the board and the tools; the stage laptop is a separate Windows 11 machine with
  install rights

## Steps

Dates are "done by" targets. Steps marked **(Owner)** are manual tasks for the user; everything
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
   laptop to timer `{"v":1,"c":"#8000A0","d":90}` (purple; green sends `d` null since e0806a6), `{"v":1,"L":2700}`,
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
     new tests for both cases pass, CI `laptop` green. Done in e7dca8a: spaces and tabs are
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
      (1:00), 30 min (0:45), 15 min (0:45), 90 min (2:00). Pushed in e0806a6, tests 206/206,
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
    rule.
    As built (corrected 2026-10-25 after the code review of ff57d6a): the `Evaluator` covers
    B-01 to B-06 only. Overtime B-07 is decided by the timer alone (`timer_core` `clock.c` and
    `view.c`, see step 13 and step 22), so there is no 45:01 overtime test in the laptop tests
    - [x] Follow-up (the owner, 2026-10-25 00:33-00:37): the deviation `d` follows the colour live
      instead of freezing at a mark. Green: no deviation line (`d` null); red: how far ahead,
      negative; purple: how far behind, positive, measured against the overdue next mark when
      B-06 applies; overtime unchanged. Exact `m:ss`, no rounding (30 s rounding discussed and
      dropped). Reason: the step 39 log showed the frozen `d` contradicting the colour (`t=90`
      green with `d=-38`, `t=211` purple with `d=-38`). Laptop side only. Expected: a test per
      colour case passes, board check by the owner. Pushed in e0806a6; green sends `d` null, so the
      firmware needs no change. Purple after a late arrival shows the lateness at arrival, frozen,
      and switches to the live value once the next mark is overdue (B-06); confirmed by the owner
      2026-10-25 00:46, each mark is an independent checkpoint.
      Board check PASS per the owner 2026-10-25 01:05 and 01:08: run 00:59:44-01:05:15 with
      4a38566 (`--length 5`), app log `t=106` purple `d=46` live (next mark overdue), `t=138`
      purple `d=78`, `t=139` green `d` null, `t=226` purple `d=46` counting, `t=251` purple
      `d=71` frozen, `t=275` green `d` null, show end detected ("sah gut aus"). The red case was
      not reached in this run and is covered by `EvaluatorTests`. Static test pictures (purple
      `+1:10`, red `-0:55`, green without a line): "perfekt", readable and big enough. During
      overtime (`t > L`) the timer shows only orange and ignores the laptop colour, as designed.
      At the show end PowerPoint answered `RPC_E_SERVERCALL_RETRYLATER`; the tick was skipped,
      no crash (3d5734b holds in practice)
12. [x] `Protocol` (S-02): serialize laptop lines, parse timer lines, drop lines that are broken,
    unknown, over 128 bytes or have `v` other than 1. Expected: round-trip and drop tests pass.
    Follow-up (dev, 2026-10-24, pushed in af0fd62): the protocol is flat JSON on both sides, no
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
    `Get-FileHash -Algorithm SHA256 "<private backup>\factory\factory-dump.bin"`
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
    fixed-width change 638f266 (step 26).
    Final look PASS per the owner 2026-10-25 00:32 ("passt"), seen with `--demo` on the clean
    8ce0500 firmware: fixed-width 150 px time, 76 px deviation line, all backgrounds including
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
    Status 2026-10-24: written together with steps 23-24, pushed in 2ed967e; host tests 3/3 in
    WSL. Compiling `timer_core` inside the IDF build verified with 12c0329 (task 4b), which also
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
    Done 2026-10-24 in c731543: the dev session's classifier refused `npx lv_font_conv`, so
    the owner ran it by hand at 21:38. Result `firmware/main/fonts/digits_150.c`, Montserrat Medium
    150 px, 4 bpp, glyphs `+ - 0-9 :`; `88:88` is about 420 px of the 448 px width. Build,
    `check-image` 0 and flash verified by the dev session on the development PC; how it reads from 1.5 m is
    checked in steps 20 and 28
26. [x] Implement `main/ui.c`: landscape layout, big `mm:ss`, small deviation line, full-screen
    background, "disconnected" symbol. Expected: builds
27. [x] Implement `main/input.c`: short tap starts; long press pauses or resets (D-03), so a short
    tap never stops a running clock. Expected: builds.
    Status for 26 and 27 (pushed in 12c0329): built, `check-image` clean, flashed under the
    standing go; serial log `Touch CST816S 0x15 found`, `timer ready, tap to start`, 12 s
    without panic; `speaker_buddy.bin` 0xb6810 bytes, 76 % of the factory partition free.
    Follow-up 638f266 (the owner asked at 21:42): fixed-width elapsed time, every digit cell as wide
    as the widest digit, and times of 100 minutes and more are scaled
    down to fit. Build, `check-image` 0 and flash verified by the dev session; the look on screen
    and the scale-down after 100 minutes are not verified yet (step 28; the scale-down check is
    in the backlog as issue #2). Superseded by 4424258 (2026-10-25, see step 28): no scaling any
    more, the time row switches between fonts of fixed sizes, picked in `timer_core`
    (`tc_time_font_pick`) with host tests.
    Regression from 638f266 (the owner, 22:06): long press no longer works. Cause confirmed by
    the owner at 22:07 (long press on an empty corner works, a touch on the digits does nothing):
    the new time container is clickable by LVGL default and swallows touches on the digit row,
    so they never reach the screen. Fixed in b976562: `lv_obj_set_clickable(s_time, false)`
    clears `LV_OBJ_FLAG_CLICKABLE` on the container. Build without warnings, `check-image` 0 and
    flash verified by the dev session; step 28 retests long press on the digits
28. [x] Flash under the standing go, `tools/check-image.ps1` exits 0 first; the owner observes the
    board and tests: tap starts, long press pauses, long
    press again resets, time readable from 1.5 m. Expected: all four observed. Offline 60:00 rule
    is covered by the host test, not waited for.
    Result 2026-10-24 22:10 (per the owner, after b976562): PASS, tap and long press on the digits
    work. The display check of the scale-down from 100 minutes (step 26) moved to the backlog
    by the owner 2026-10-25 01:09: GitHub issue #2, milestone `v1.0`. Pulled back by the owner
    2026-10-25 09:35: issue #2 goes into the user test block of 2026-10-25; dev-Repository-2
    builds the way to reach 100 minutes on the board. 2c93aed moves the width and scale maths of
    the time row from `ui.c` into `timer_core` (`tc_time_row_width`, `tc_time_row_scale`), host
    tests with the real font widths: 99:59 unscaled, 100:00 is 534 px wide and scaled to 446 px
    on the 448 px row; CI green (run 40181498702). For the board check a test-only image (an
    uncommitted `main.c` line, clock starts at 99:50) runs on the board; the clean image is
    flashed back afterwards. Issue #2 stays open until the board check passes.
    Board check 2026-10-25 12:27 (per the owner, confirmed 12:29): FAIL. 99:59 is drawn, from
    100:00 the time row stays black. The firmware keeps running (serial `t` 6053 to 6057 with
    `run` true, no reboot). Cause (dev-Repository-2 from config and code, timing confirmed by the
    owner): the LVGL transform scale on the 534 x 108 px row needs a layer buffer of about
    230 KB, the LVGL builtin pool is `CONFIG_LV_MEM_SIZE` 64 KB, the allocation fails and LVGL
    skips the row. Present since 638f266, not caused by 2c93aed; the host tests cover only the
    maths. Affects only talks past 100 minutes. Decision (the owner, 12:30): fix A, a second digit
    font of about 125 px for a row that does not fit, no transform; this matches the requirement
    "large digits as a font of the right size, not scaled". Option B (LVGL on the PSRAM heap)
    rejected for its blast radius. Owner: dev-Repository-2; board re-check in the user test block.
    Fix A pushed in 4424258: own font sizes instead of a scaled row, 150 px below 100:00, a new
    `digits_120` from 100:00 ("100:00" is 427 px wide), 76 px from 1000:00. The transform code is
    gone (no `transform_scale` or pivot left in `firmware/`); `tc_time_font_pick` replaces
    `tc_time_row_scale`; host tests 8615 checks; the image grows by 29,136 bytes. A test image
    with fix A is on the board.
    Board re-check 2026-10-25 12:43 (per the owner, test image `4424258-dirty`): PASS, at 100:00
    the smaller digits show, nothing is cut off, no black screen. New finding, GitHub issue #15 "Even spacing around the digit 1 in the
    time display" (enhancement, milestone `v1.0`, not a blocker): the fixed cells are sized for
    the widest digit, so the gap after a narrow 1 looks larger (100:00, also at 101:xx). After the
test block the clean firmware 1814f3c (built from HEAD) was flashed back; #2 and #12 closed on
GitHub 2026-10-25 13:31
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
    As built (`DemoSequence.cs`): neutral without deviation, green `0`, green `+0:45`, purple
    `+1:30`, purple `+2:05`, red `-1:30`, red `-10:15`, 3 s each, then again. The demo sends
    these values directly, so its green steps carry a deviation, unlike the live evaluation.
    Deviation in the code (found by the coordinator 2026-10-24 22:30): `DemoSequence.LengthSeconds`
    is `45 * 60` (`laptop/src/SpeakerBuddy.Core/DemoSequence.cs:13`), so overtime orange does not
    show in the demo. The owner decided 2026-10-25 00:00: change the code, short `L` of about 60 s.
    Done in d11211a: `DemoSequence.LengthSeconds = 60`, new test, `dotnet test` 204/204. Board
    check per the owner 2026-10-25 00:06: overtime orange blink after 1:00 seen, the first view of
    overtime on the board
    Status for 30-32 (2026-10-24, pushed in 8b33604 firmware link plus `timer_core/session`, and
    206abf7 laptop `SerialLink`, `TimerPort`, `LineFramer`, `DemoSequence`, App with `--demo`): `dotnet test` 150/150, host tests 5/5, `check-image` clean (6997 symbols). Verified by
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
    the port was closed (`t=110..118` before the live `t=182`). Fixed in 2394fc2 "laptop: drop
    the timer's backlog on connect"; board test PASS 2026-10-25 00:08 (first `t` is live, no
    jump).
    State at the end of 2026-10-24 (step 33 still FAIL, nothing committed after ef420e5; per
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
    Panel fix PASS, pushed in e6a4712 (`main/main.c`): `reset_panel()` runs before
    `bsp_display_start` and pulls TCA9554 pins 0, 1, 2 and 6 low for 20 ms, then high, then
    waits 150 ms. All four pins are kept as in Waveshare's demos; which one is the panel reset
    will not be narrowed down. Proof: 10 RTS resets without laptop traffic (23:32-23:34), 0
    black screens per the owner 23:35; before the fix 1 of 1 black.
    Laptop fix PASS, pushed in 6cc5a9a: new `SafeLineState`, called by `SerialLink.TryOpen`
    before `Open`, turns the control lines off in a safe order through Win32 (App csproj
    `AllowUnsafeBlocks`). Replug test 3 of 3 with `boots=1`, reason 1 (23:45-23:48, replugs by
    the owner, reads by the dev session); before the fix 3 of 3 had `boots=2`, reason 11. The board
    runs the clean e6a4712 image again.
    Retest PASS (per the owner 2026-10-24 23:55, "genau so"): `--demo` 23:54:39-23:55:09 on the
    e6a4712 image with the 6cc5a9a laptop program; colours cycled with the deviation shown, and
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
    console window), menu entries "Stop and release port" (the kill switch, no global hotkey; the
    cable pull as second way out is dropped, see step 49) and "Open log"; no
    windows, balloons or toasts; log file under `%LOCALAPPDATA%\speaker-buddy\logs`. Expected:
    starting the exe shows only a tray icon
38. [x] Check mode `speaker-buddy.exe --check deck.pptx` (L-09): reads the .pptx with
    `DocumentFormat.OpenXml` in `SpeakerBuddy.Core` (no PowerPoint needed, testable on Ubuntu),
    prints marks per slide, near misses, hidden-slide marks and decreasing marks (OpenXml
    confirmed by the owner, 2026-10-24). Expected: unit
    test with a small fixture .pptx in `laptop/tests/fixtures/` passes in CI.
    Status for 35-38 (2026-10-24, pushed in c56df02 Core `DeckReader`, `DeckCheck`,
    `TalkController`, OpenXml 3.1.0, fixture .pptx, and b32de38 App `PowerPointSource`,
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
    Test deck (2026-10-25): `<private backup>\speaker-timer-desk-test.pptx`,
    5 slides, marks 1:00, 2:00, 3:00 and 4:30; `--check` with `--length 5` reports 0 problems,
    `tol` 0:30 (0:45 once the step 10 follow-up is in).
    Run 1, 2026-10-25 00:16:57-00:22:44, unattended at the owner's request (00:13): a test-only
    diagnostic image with clock autostart (not committed; the clean image was flashed back
    afterwards), tray app 3d5734b with `--length 5`, show started over COM and paged by a
    coordinator script. App log: show detected, 4 marks, highest 270 s; slide 2 at `t=77` green
    `d=17`; slide 3 at `t=82` red `d=-38`, green at `t=90` once `t` reached `m - tol`; `t=211`
    purple (late for the next mark); slide 4 at `t=261` purple `d=81`; slide 5 at `t=286` green
    `d=16`; show end detected. PASS for show detection, marks, evaluation and show end, the first
    live run of the COM path.
    Fixed before the run: 3d5734b "laptop: survive a busy PowerPoint" (PowerPoint rejected a
    call at show start, `RPC_E_CALL_REJECTED` surfaced as `MissingMemberException` and crashed
    the app).
    Per the owner 2026-10-25 00:24, who watched the board after all: "orange came. powerpoint did
    not show messages. worked like i would have wanted it." So the board view during the show,
    including overtime orange, and no PowerPoint dialog are confirmed.
    Not tested by decision: two decks in slide show at once (`FullName` lock). The owner
    2026-10-25 00:27: "won't be happening"; moved to the backlog as GitHub issue #1, milestone
    `v1.0` (after the first talk)
40. [x] Commit and push

### Task 7: Stage laptop and rehearsal (by the go/no-go date)

41. [ ] **(Owner)** Add `## m:ss` marks to the talk deck. Due before step 43
42. [ ] **(Owner)** Run `speaker-buddy.exe --check <talk deck>`. Expected: no near misses,
    no decreasing marks, highest mark close to 30:00
43. [ ] **(Owner)** Put the signed program on the stage laptop. On the development PC: `pwsh
    tools/publish-laptop.ps1` (one self-contained `laptop/publish/speaker-buddy.exe`, about
    70 MB, no .NET runtime needed on the stage laptop; O-03 pulled forward in e4b85ad), then
    `pwsh tools/sign.ps1` (b82b6eb). Expected: exe starts into the tray.
    The stage laptop is the owner's private device. Signing (the owner, 2026-10-25 01:11, replacing
    "unsigned" from 01:09): self-signed code signing certificate `CN=Speaker Buddy (self-signed)`, thumbprint
    `8E1E2DC50FFA1A5E5709FF6EDAF2DE142C3FC8C7`, valid until 2028-12-30, private key not
    exportable in `Cert:\CurrentUser\My` on the development PC. The public file is
    `speaker-buddy-codesign.cer` in the owner's private backup storage; importing it on the
    laptop is a private setup note. Verified on the development PC: published, signed with an RFC 3161
    timestamp, the signed exe runs `--check` on the desk-test deck (0 problems, `tol` 0:45);
    `Get-AuthenticodeSignature` there reports `UnknownError`, as expected without the root.
    So that Windows does not block it:
    - Import the `.cer` (private setup note), then `Get-AuthenticodeSignature
      <exe>` shows `Valid` with the thumbprint above
    - Copy the exe without a browser download (private file sync or USB stick), or run
      `Unblock-File <exe>` after copying
    - Start the exe once by hand before the rehearsal
    - Check Smart App Control: Windows Security > App & browser control > Smart App Control, or
      `Get-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy -Name VerifiedAndReputablePolicyState`
      (0 = off, 1 = on, 2 = evaluation)
    - If Smart App Control is on: per the dev session (not tested), a self-signed certificate
      does not satisfy it even when imported; the options are turning Smart App Control off or
      a CA certificate (issue #3). The owner decides then
44. [ ] **(Owner)** Desk rehearsal with the talk deck on the development PC (milestone 5 in the private
    requirements notes). Expected: colours behave as intended; fixes go back into Tasks 2 to 6
45. [ ] Tag the build that passed step 44 as `v0.1.0-rc1` and keep its `.bin` and exe next to the
    factory backup under `<private backup>\builds\v0.1.0-rc1\`.
    Expected: a known-good build exists before the rehearsal. This is the safety net for step 46
46. [ ] **(Owner)** Go/no-go: full dress rehearsal on the stage laptop with a second screen,
    presenter view, the timer on USB, the whole 30 minutes. Expected: the talk never shows a
    window from the program. (The cable pull mid-talk is dropped: the cable is the timer's only
    power until v2, so pulling it switches the timer off; the owner 2026-10-25 02:38-02:41.)
    Go means the rc build becomes the release for the first talk; the owner then chooses `v0.1.0` or `v1.0.0`,
    depending on whether the `v1.0` milestone is done (decision 2026-10-25 02:19); no-go means the owner runs the talk with a backup timer only
47. [ ] Commit and push any fixes, tag the release for the first talk (`v0.1.0` or `v1.0.0`, see step 46)

### Task 8: Freeze and the first talk

48. [ ] Freeze: no flash, no code change to the stage build. The stage laptop keeps the tagged
    exe. Expected: board and laptop carry the tag from step 47
49. [ ] **(Owner)** On site: a backup timer runs in parallel; there is no other
    stage fallback (the owner, 2026-10-25 02:38-02:41, replacing the A-02 order "offline timer,
    presenter view clock, backup timer, switch by pulling the cable"). The cable is the timer's only
    power until v2: pulled, the timer is off, and after replugging it starts at 00:00, stopped.
    If only the laptop program stops, the timer keeps counting with the disconnected symbol and
    no colours

### Stretch, before the first talk only if every core step above is on time

50. [x] Codecov upload for the `laptop` job with `CODECOV_TOKEN` and a README badge (R-03).
    Pushed 2026-10-25 ("Upload laptop coverage to Codecov"): `ci.yml` job `laptop` with
    `codecov-action@v7`, flag `laptop`, `fail_ci_if_error: false` until the secret exists;
    `codecov.yml` with informational checks and carryforward for the `laptop` flag. README badge
    added. Local coverage (dev-Repository-2): lines 93.3 % (979/1049), branches 82.6 %.
    The owner set the repository secret `CODECOV_TOKEN` on 2026-10-25 at 01:46; CI run 40144113537
    on cb6c842 uploaded the coverage report to Codecov. Follow-up for later:
    `fail_ci_if_error` can go to `true` now that the token exists
51. [x] CI job `firmware-build` in the `espressif/idf:v5.5.5` container: `idf.py build`, run the
    `nm` check from step 20, upload `speaker_buddy.bin` as artifact (R-02, A-04).
    Pushed in fff75c8 "ci: build the firmware in the ESP-IDF container"; CI run 40141818016
    green: jobs `laptop`, `firmware-host` and `firmware-build`, the image check ran inside the
    container, artifact `firmware` uploaded
52. [x] Pulled forward from after the first talk (O-03): `speaker-buddy.exe` as one self-contained file,
    `pwsh tools/publish-laptop.ps1`, no .NET runtime on the stage laptop. Pushed in e4b85ad;
    verified on the development PC (published, signed, `--check` runs)
53. [x] Firmware version (the owner, 2026-10-25 01:17-01:19): the version from `git describe` in the
    boot log and reported to the laptop; the laptop logs both versions. Expected: boot log and
    laptop log show the same version as `git describe`.
    Pushed in 2858deb "firmware and laptop: report their versions": line `{"v":1,"fw":"..."}`
    (documented in `docs/protocol.md`), sent when the laptop comes online and every 10 s.
    Check PASS on the development PC (dev session, 2026-10-25 01:27): boot log `Speaker Buddy 13a20bc-dirty
    ready, tap to start`, wire `{"v":1,"fw":"13a20bc-dirty"}` every 10 s; tray log `started,
    speaker-buddy da3daea-dirty` and `timer firmware 13a20bc-dirty` 8 s after connect; both match
    `git describe` of their builds. Not checked: the version in the tray tooltip
54. [x] Release workflow, pulled forward in part from after the first talk (R-07, R-09): `release.yml` on
    a tag `v*` builds the firmware with the `check-image` gate and the single-file exe, writes
    `SHA256SUMS` and creates a GitHub release. The release exe is unsigned, because the signing
    key is not exportable from the development PC; the exe for the first talk is signed locally (step 43). Attestations
    are not part of it yet. Pushed in a5c731e. Expected: a test tag produces a release with
    firmware, exe and `SHA256SUMS`.
    Test 2026-10-25: the first tag `v0.0.1-test1` (run 40146824888) failed, because
    downloading the artifacts into `assets/` collided with the repo's `assets/logo`; fixed in
    e8dfa7d "Download release assets into a folder of their own". Tag `v0.0.1-test2` on e8dfa7d,
    run 40147332708: jobs `firmware-host`, `firmware`, `laptop` and `release` green, marked as
    pre-release; assets `bootloader.bin`, `flash_args` (0x0 bootloader, 0x8000 partition table,
    0x10000 app), `partition-table.bin`, `SHA256SUMS`, `speaker-buddy.exe` (73.5 MB, product
    version `v0.0.1-test2`), `speaker_buddy-merged.bin`, `speaker_buddy.bin`; the downloaded exe
    matches `SHA256SUMS`. Test release and tags deleted afterwards (no tags on the remote)
55. [x] Tray icon and exe icon from the logo (the owner, 2026-10-25 01:27-01:32): logo D "face in
    the colour ring", a three-colour ring (purple, green, red) around the dark Buddy face with
    white eyes and a green smile; a grey "waiting" variant for the tray.
    Design canvas: private. Expected: the exe in
    Explorer and the tray icon show the logo, grey while waiting.
    Pushed in 5c5b87d (dev-Repository-2): SVG masters in `assets/logo/` (64, 32 and 16 px plus
    waiting variants), `speaker-buddy.ico` and `speaker-buddy-waiting.ico` (16, 24, 32, 48 and
    256 px) made by `tools/make-icons.ps1` (same SHA-256 over two runs), exe `ApplicationIcon`,
    tray grey while waiting and in colour when connected. Verified by the dev session: build
    without warnings, tests 215/215, format clean, render sheet at 16-256 px on light and dark.
    Live check PASS per the owner 2026-10-25 01:41: tray app started with the board unplugged
    showed the grey logo, after plugging in the colour logo. Remaining checks PASS per the owner
    2026-10-25: single-file publish 2c93aed grey without the board and in colour with it (12:44);
    sharp at 125 % (12:51) and 150 % (12:52) display scaling, publish 2c935bf restarted at each
    scale
56. [x] Firmware startup screen of about 2 s with the logo, "Speaker Buddy" and the firmware
    version (step 53), then the normal clock screen. Expected: `check-image` 0, flash, the owner
    sees the screen after a restart.
    Pushed in 74fcbdd "firmware: startup screen with logo and version":
    logo D drawn with LVGL primitives (three arcs, face, eyes, smile arc; 160 px), "Speaker
    Buddy" in Montserrat 48 and the version (`esp_app_get_description`, `git describe`) in
    Montserrat 28 grey, on `lv_layer_top` for 2 s or until the clock runs, then gone for good;
    `timer_core/splash.c` with 5 host tests. Build without warnings, `check-image` 0, flashed by
    the dev session. PASS per the owner 2026-10-25 01:39 ("das logo und version waren toll.
    perfekt"). Per the owner 02:27: a tap during the startup screen ends it and starts the clock;
    after power-on the clock shows 00:00, stopped

Follow-ups from the code review of ff57d6a, decided by the owner 2026-10-25 01:51 ("alles so").
Dev sessions: dev-Repository (firmware, CI), dev-Repository-2 (laptop).

57. [x] Keepalive: while connected, the laptop sends a state line at least once per second, a
    neutral one when no slide show runs and the last state while PowerPoint is busy. The
    disconnected symbol then only means cable or program gone. After `state: ended` the laptop
    keeps sending neutral, so the timer stays online and neutral (`docs/protocol.md` follows).
    Pushed in f9603cd "Keep the timer link alive once per second" (tests 217/217). Expected:
    tests pass; on the board no disconnected symbol between shows. Board check PASS per the owner
    2026-10-25 02:28: tray app (5aa7b3f build) running without PowerPoint, board replugged, no
    disconnected symbol after 15 s
58. [x] New protocol line `{"v":1,"L":null}` = talk length unknown, clear it. The laptop sends
    it after connecting and at show start when `L` is unknown; the firmware clears `L`.
    Firmware pushed in 13a1361, board-checked by the dev session (`L` 60 logged, `L` null
    cleared it, `L` 0 dropped). Laptop side pushed in 21f4f6b: sends `L` null after connecting
    when unknown and at every show start that leaves `L` unknown; a show end forgets the deck's
    `L`, `--length` stays. Expected: host and laptop tests for the new line pass. Laptop tests
    223/223, CI on 21f4f6b green
59. [x] The clock survives an unexpected chip reset: RTC snapshot of `t` and the running state,
    resumed after a reset unless it was a power-on or brownout. Expected: host tests pass; on
    the board a reset during a running clock resumes it.
    Pushed in 635ffbb "firmware: the talk clock survives a chip reset". Host tests 7/7. Board
    (dev session): `t=11` running before an RTS reset, 13 to 16 running after; the real image
    logged "clock restored after reset (reason 11): t=78 s, running"
60. [x] The laptop counts a timer reset only when `t == 0` and `run == false`. Expected: tests pass.
    Pushed in e0d50a5 "Treat only a stopped clock at zero as a timer reset", tests 226/226, the
    3 new tests fail with the old rule, CI green. Board fact: a long-press reset reports `t=0`,
    `run=false`
61. [x] Hardening without new decisions. Expected: CI green, tests pass.
    Done: 3332815 actions pinned by commit, tokens kept out of checkouts, `dependencies.lock`
    gate, the release workflow runs the firmware host tests as its own job; 63c8836 kill switch
    safe to run twice; aa2995d never an error dialog (log and recover) and a robust serial loop
    (each iteration caught and retried, guarded event handlers, closing the port always clears
    it); c1d7a79 panel reset pulse retried; 32a0b82 `check-image` also checks the linker map
    and the ELF (radio blobs in the map are fatal, `libesp_phy` allowed; fewer than 1000 symbols
    or no `app_main` fails; ROM type-A radio symbols are reported); e611c0b `--check` on
    unreadable or non-`.pptx` files, log rate limit and 30-day cleanup (tests 245/245); 610d8e1
    firmware parses lines with up to 32 fields (`MAX_FIELDS` 8 to 32; 9-field and 20-plus-field
    lines parse, the test fails with 8; board: `L` 60, null and 0 as before).
    CI green on all of them except 610d8e1 (running); the map scan and the lock gate pass inside
    the IDF container.
    Line framer pushed in 5aa7b3f "firmware: line framer in timer_core with host tests": 6 host
    cases, 8/8 suites, build clean, `check-image` 0; board: a 195-byte line ignored, a line split
    across reads parsed, `\r\n` parsed. CI 610d8e1 and 5aa7b3f green
62. [x] Deck line `## session length m:ss` (for example `## session length 60:00`) sets the talk
    length (the owner, 2026-10-25 02:38-02:41). Precedence: `--length`, then `## session length`,
    then the highest mark if it is at least 5:00. Laptop only (dev-Repository-2). Expected: tests
    for the precedence pass.
    Pushed in fdf3f92 "Read the talk length from a "## session length" line in the deck": the words
    in any case, minutes as for `--length`; the first line on a visible slide counts, others and
    ones on hidden slides are reported by `--check` and ignored; near misses such as
    `## session lenght 60:00` are reported. Tests 278/278 locally on abb1d38 (dev session,
    2026-10-25 03:01). The force pushes of the history rewrite did not trigger `ci.yml` (no code
    change between the old and new tips); CI green with the next code push, run 40175800237 on
    65a35a3 (2026-10-25)
63. [x] At slide show end the laptop also sends `{"v":1,"L":null}`, so the timer stops the
    overtime blink after the show and keeps counting neutral (the owner, 2026-10-25 03:07; found
    by the fact check of the README: today the timer keeps the old `L` and stays orange after a
    show that ran long). A length given with `--length` stays. Owner: dev-Repository-2, next
    session. Expected: tests pass; on the board the orange blink ends with the show. Then update
    `docs/protocol.md` ("End of slide show") and the README screen table
    Pushed in 1359af4 "Clear the talk length on the timer when the slide show ends": without
    `--length`, `TalkController` sends `{"v":1,"L":null}` right after `{"v":1,"state":"ended"}`;
    with `--length` only the end line. `docs/protocol.md` (Talk length, End of slide show, typical
    session) and the README screen table updated in the same commit. Tests 281/281 locally
    (dev-Repository-2, 2026-10-25), 3 new; CI green (run 40175800237 on 65a35a3). Board check 2026-10-25 12:31
    (per the owner): PASS, "das orange ging aus als der präsentationsmodus aus gegangen ist".
    Setup: the laptop program from 2c93aed (single file, self-signed), a deck copy with
    `## session length 1:00` (log: talk length 60 s from slide 2, show started 12:31:22, ended
    12:31:31); board on the #2 test image `7b83e44-dirty`, clock stopped at 99:50
64. [x] Raise test coverage, tests and injection only, no behaviour change, no user test block
    (the owner, 2026-10-25 13:48). Baseline Codecov on main: 70.84 % over 33 files, flag
    `laptop` 66.7 %, flag `firmware` 87.1 %; the drop from about 96 % came from adding the
    firmware (bac64f8) and the App project (c92cef4) to the measurement, not from lost tests.
    Only `SerialDevice`/`SafeLineState` (about 50 lines, real COM port) stay hardware-only.
    Expected: CI green, Codecov up, no change in behaviour.
    Pushed 2026-10-25 (local numbers by the dev sessions): c187001 `line.c` (lines 93.9 to
    99.5 %, branches 82.4 to 98.7 %); 4470766 `PowerPointSource` with fakes (14 to 92 %);
    e7625a7 `DeckReader` (lines 89.7 to 100 %, branches 63.6 to 100 %); 5d86079 `TrayContext`
    (18 to 92 %); aed6cd4 `FileLog` (0 to 100 %); ee6c5d3 `WindowsPortDiscovery` registry walk
    (0 to 100 %); 52ed900 splits `Program` into `CommandLine` and `ConsoleModes` (100 % each;
    the `Program` shell, 48 lines, stays untested), output byte-identical to 2c93aed for 13
    argument lists. Result on 52ed900 (Codecov API, verified by the coordinator): total 89.49 %
    over 35 files, flag `laptop` 87.57 %, flag `firmware` 97.15 %; per file `PowerPointSource`
    90.44 %, `TrayContext` 87.17 %, `WindowsPortDiscovery` 95.83 %, `TalkRunner` 85.63 %; CI
    green (run 40195972530).
    Facts: the kill switch is `TalkRunner` Stop/Dispose behind the tray entry "Stop and release
    port"; there is no single-instance lock, a second program only fails to get the busy port.
    Finding: `DeckReader.Read(path)` leaks the file handle when the file is not a zip
    (`PresentationDocument.Open` throws without disposing its `FileStream`); no impact today,
    `--check` exits right after and the tray never uses `DeckReader`. Decision (the owner,
    14:05): fix it. Fixed in 6663599 (dev-Repository-2): Core tests 306/306, App 98/98 locally,
    the new test fails on the old code; CI green (run 40197377600, 4 jobs)

After the first talk (not planned here), as GitHub issues in milestone `v1.0`: #6 build attestations for
the release (R-07), #7 Dependabot (R-08), #8 CSV rehearsal log (L-08), #9 brightness (D-05),
#10 flashing from the browser (O-04), #11 settings without code changes (O-05), #12 technical
follow-ups (Codecov `fail_ci_if_error`, firmware coverage with gcov, an App test project, the
icon check at 125 % and 150 % scaling, COM intermediates, a tap during the startup screen).
From #12 done on 2026-10-25: Codecov `fail_ci_if_error: true` (f894051, CI green in run
40175768809; the laptop upload passed with it in run 40175800237) and a host test for a tap
during the startup screen (65a35a3, CI green; board PASS by the owner at 02:27). Also from #12,
pushed 2026-10-25: firmware host-test coverage with gcov/gcovr under the Codecov flag `firmware`
(bac64f8; locally lines 96.6 %, functions 100 %; CI green in run 40177225061), `PowerPointSource`
releasing every COM object on each poll (b07f2ee; live check 2026-10-25 12:31: PowerPoint exits
and the app survives, PASS, verified by the coordinator: after PowerPoint was closed
`POWERPNT.EXE` was gone and `speaker-buddy.exe` kept running; the log shows one "tick skipped"
at show start, `0x8001010A` busy, and one at PowerPoint exit, `0x800706BE` at 12:31:58, no
repeats. Open finding (the owner, 12:32): closing PowerPoint after the show had ended made the
board blink orange once; the board ran the test image at 99:50, so above the offline 60:00
overtime rule. Suspected, not verified: a blocking COM call into the exiting PowerPoint stalls
the keepalive for more than 3 s and the timer goes offline; the owner saw the USB symbol with
the cross during the blink (12:37), which is the offline timeout. 2c935bf moves the PowerPoint
poll to its own thread, so the talk thread never waits on COM; polls of 1 s or more log
"PowerPoint poll took N ms"; Stop joins both threads within 2 s. App tests 29/29, Core 281/281
(dev-Repository, local), CI green (run 40189941608, 4 jobs). Cause measured (dev-Repository
harness without the board, 3 closes per build, `WM_CLOSE`): `GetActiveObject` into the exiting
PowerPoint blocks for about 2.25-2.29 s and ends in RPC `0x800706BE`; it hit 3 of 3 with 82316f3
(before b07f2ee) and 1 of 3 with the current `PowerPointSource`, so it is a race and b07f2ee is
not the cause; releasing the COM objects takes 0-18 ms. Live proof PASS 2026-10-25 13:27-13:28
(publish 2c935bf, board on the fix A test image at 99:50, past the offline 60:00 rule): 3
closes, 2 hung polls logged ("PowerPoint poll took 2239 ms", "2347 ms"), no "timer
disconnected"; the owner saw no USB symbol with the cross and no orange (13:28). The finding is
fixed by 2c935bf), and an App test project (c92cef4): internal seams `ISerialDevice`/`WindowsSerialDevice`,
`ITimerLink`, a poll delegate and `TalkRunner.Tick`; 26 App tests run in a new `windows-latest`
CI job, the Ubuntu job tests Core only. CI green on 2c93aed (run 40181498702, 4 jobs). #12 code
done; the tray icon checks at 125 % and 150 % scaling passed (step 55) and the poll thread passed
its live proof; #12 complete, closed on GitHub 2026-10-25 13:31.
Also in milestone `v1.0` since 2026-10-25: #15 even spacing around the digit 1 in the time
display (from the #2 board check).
#7 pushed 2026-10-25: c741329 `.github/dependabot.yml` (weekly; GitHub Actions in one group
including majors; NuGet minor and patch in one group, majors as single pull requests); the
ESP-IDF component manager is no Dependabot ecosystem, so 18a73c3 adds the README section
"Dependency updates" with the monthly manual check (`idf.py update-dependencies`, exact pins
ESP-IDF 5.5.5 and BSP 2.0.3). #7 closes after CI.
Decisions of the owner, 2026-10-25 15:11: (1) an alpha release `v0.1.0-alpha.1` when work stops
on 2026-10-25; it also gives the README link "latest release" a target. (2) #8 CSV rehearsal
log: comma as separator; dev-Repository-2 reworks it, the README rationale changes with it.
(3) #1 two slide shows: the laptop program follows only the show it started with, and switching
must not disturb the timer; shows that are already running when the followed show ends are
ignored. Owner: dev-Repository. Live result before the change (2026-10-25 15:08-15:09, real
PowerPoint, harness, no board): the lock holds while show B runs (issue steps 3-4 PASS); after
show A ended, B was taken about 315 ms later (step 5 FAIL against the issue text), which the
change removes.
Pushed 2026-10-25: 3e6d351 #8 rehearsal log (`--rehearsal`, one CSV per show in the log folder,
comma, README section "Rehearsal log"; Core 320/320, App 112/112 locally). 55a84a8 #1 in
`PowerPointSource`: when the followed show ends, every show still running is ignored until it
ends, a show started later is followed; at program start the first running show is taken as
before; log line "ignoring the running show of <path> until it is started again"; real
PowerPoint harness 15:15-15:16 without the board: issue step 5 PASS. README wording for #1 and
two fixes to the rehearsal section (example row matched the tolerance, file name from the first
row) by the documentation session. CI on 55a84a8 red (run 40201728872): 4 `RehearsalRecorder`
file name tests failed on Ubuntu, because the .NET path functions follow the OS; fixed in
bab1386 (plain string operations, the characters Windows forbids in file names replaced), CI
green in run 40204184587. #1 and #7 closed 2026-10-25 15:56; #8 was closed by "Closes #8" in
3e6d351.
Dependabot opened pull requests #16-#20 (NuGet group and 4 majors); merges wait for the owner's
go. All five are red only in the Codecov upload step, because Dependabot runs get no repository
secrets; build and tests are green. Decision (the owner, 15:56): no Codecov secret for
Dependabot; dev-Repository-2 changes `ci.yml` so a failed Codecov upload does not fail runs by
`dependabot[bot]` (`fail_ci_if_error` false for them). Also 15:56: the firmware moves to the
newest versions now, Renovate for #21 is decided later; dev-Repository compares ESP-IDF 6.1 and
5.5.6 against the BSP and component ranges, then raises the pins. cfa9f54 sets
`fail_ci_if_error` false only for `dependabot[bot]`; Dependabot was asked to rebase #16-#20.
Decision (the owner, 16:20): ESP-IDF 6.1, a major version. Trial by dev-Repository: every
component manifest allows it (no upper bounds); BSP 2.0.3 has a rule for 6.x that adds
`espressif/usb` `^1.0.0` (resolves to 1.6.0); build with 0 errors and 0 warnings; app 831,520
bytes instead of 854,400 (picolibc); no removed API in use; esp_codec_dev stays 1.5.11 (BSP
`~1.5`) and cmake_utilities 0.5.3 (co5300 and panel_io_additions `0.*`), no overrides.
Change: `idf_component.yml` pins idf `==6.1.0` (the tag `v6.1` names the same release); the
CI and release image is `espressif/idf:v6.1` with the Docker Hub digest of that tag in
`ci.yml` and `release.yml`; BSP stays `==2.0.3`, lvgl `>=9.6.0,<10`; `dependencies.lock`
format 3.0.0 (component manager 3.1.2); picolibc is the default C library from 6.0; no
`sdkconfig.defaults` setting changed. `check-image` exit 0 on the 6.1 build including the
linker map: no TinyUSB or `tud_` symbols, no radio archives, so `espressif/usb` adds no
TinyUSB code. Commands unchanged. Board (dev-Repository): flashed, boot log "ESP-IDF v6.1",
the laptop program from f7143cf connected and received the firmware line. README version
mentions updated by the documentation session. Pushed as 04001ba (IDF 6.1), 88b9d42 (README);
CI green on 04001ba (run 40207994426, `firmware-build` in the v6.1 image). Dependabot merged
(the owner's go 16:26): #16 as 801ab6a, #19 System.IO.Ports 10.0.13 as f7143cf.
Board check PASS (the owner, 2026-10-25 16:44, "1 ok, 2 ok, 3 ok"; firmware `f7143cf-dirty` on
IDF 6.1, publish exe f7143cf): after a replug grey icon, startup screen, 00:00, colour icon (log
16:43:11 disconnected, 16:43:14 connected on COM4, 3.4 s); a tap starts the clock, the digits
stay steady; tray "Stop and release port" logs "stopped, port released" at 16:43:53, the icon
goes, the board shows the USB symbol with the cross. This covers IDF 6.1 and System.IO.Ports 10
on the board. The remaining test package updates (coverlet 10, Test.Sdk 18, xunit runner 4)
came as dc5cc95, CI green; Dependabot #17, #18 and #20 were closed as superseded by it.
Alpha release (the owner's go 2026-10-25 17:45):
[v0.1.0-alpha.1](https://github.com/tkronawitter/speaker-buddy/releases/tag/v0.1.0-alpha.1),
pre-release, tag on 0adb3bc, release run 40213387093 green (4 jobs). The release text starts
with "What is in it / Known limitations", written by the coordinator in the release only, then
the generated notes. Checked by dev-Repository: all 6 assets match `SHA256SUMS`;
`speaker_buddy-merged.bin` flashed at 0x0 with esptool 5.6.0, boot log "App version:
v0.1.0-alpha.1" and "ESP-IDF: v6.1"; the release exe (unsigned, product version
v0.1.0-alpha.1) connected on COM4 at 17:53:13. The owner's smoke test is open. esptool 5
renames `write_flash` to `write-flash` (the old name still works with a warning); README,
`.github/release-notes.md` and the rollback below use the new name.
New issue #21 "Automatic update pull requests for the firmware dependencies" (milestone `v1.0`,
the owner's ask 2026-10-25 15:22). Options: a Renovate custom regex manager, a scheduled
workflow running `idf.py update-dependencies`, Dependabot `docker` for the image digest. State on
2026-10-25 (component registry and GitHub, read by the coordinator): ESP-IDF 5.5.5 in use, 5.5.6
released 2026-10-16 (the image digest moves with it); BSP 2.0.3, lvgl 9.6.0~1, esp_lvgl_port
2.9.0 and the panel, touch and io components current; transitive esp_codec_dev 1.5.11 (newest
stable 1.6.2) and cmake_utilities 0.5.3 (1.1.1), probably held back by the BSP's ranges, not
checked.
GitHub ruleset "main" (id 25823862, the owner's go 2026-10-25 15:21; read back with the GitHub
API): no force push, no deletion, required checks `laptop`, `laptop-app`, `firmware-host`,
`firmware-build`; bypass "always" for the repository admin role, so direct pushes by the owner
still work and show "Bypassed rule violations". `ci.yml` runs only on `laptop/**`,
`firmware/**`, `tools/**` and itself, so a pull request outside these paths gets no checks and
merges only through the bypass (README "Branch rules"). Before a future history rewrite,
disable the ruleset (or confirm that the admin bypass lets the force push through), because it
blocks force pushes.
Milestone `backlog`: #13 other boards (O-07), #14 printable case (O-08).
Roadmap (the owner, 2026-10-25 01:17-01:19, version scheme 01:59-02:01): release candidates
for the first talk are `v0.1.0-rcN`, USB only, no radio (A-04 unchanged). At the go/no-go (step 46)
the owner chooses `v0.1.0` or `v1.0.0` for the first talk, depending on whether the `v1.0` milestone is
done (02:19). v1.0 is the first stable USB release, with its own milestone for the backlog issues. v2: Bluetooth LE and a battery, issue #4, milestone `v2`; A-04 becomes "Bluetooth
allowed, Wi-Fi blocked". v3: Wi-Fi and OTA updates, issue #5, milestone `v3`; A-04 is lifted for
Wi-Fi and OTA.
Also in milestone `v1.0` (moved from `v2` on 2026-10-25 02:19): issue #1, test the presentation lock with two slide shows at
once (from step 39); issue #2, display check of the time scale-down from 100 minutes (from steps
26 and 28; pulled into the user test block of 2026-10-25 by the owner at 09:35); issue #3, a publicly trusted code signing certificate (options: SignPath Foundation,
free for open source with a release history; Certum Open Source, about 25-69 EUR a year; a CA
OV certificate, about 100 USD a year and up; Azure Artifact Signing is not available to
individuals in Germany).

## Rollback

- The `erase_flash` done before step 20 cannot be undone; the archived factory dump (step 16)
  stays archive only.
- Steps 20, 28, 33 (flash): the factory image is never restored. Rollback is flashing the last
  own build that worked: `idf.py -p COM4 flash` from the previous commit, or `esptool -p COM4
  write-flash 0x0 <merged .bin>` from the archived build. If the board stops enumerating, hold
  BOOT while plugging in to enter the ROM bootloader, then flash again. Step 16 confirms the
  archive before the first flash
- Steps 1 to 14, 22 to 27, 30 to 38 (code): `git revert <commit>`; nothing is deployed anywhere
  until a flash or a copy to the stage laptop
- Step 43 (stage laptop): delete `speaker-buddy.exe` (one self-contained file, no runtime was
  installed); remove the certificate as in the private setup note
- Step 46 (rehearsal fails): the talk runs with a backup timer only; `v0.1.0-rc1` from step 45
  stays the last known-good build
- During the talk: if the laptop program misbehaves, "Stop and release port" in its tray menu;
  the timer keeps counting offline and PowerPoint is unaffected because the program only reads.
  Pulling the cable switches the timer off (no battery until v2); the backup timer is the fallback

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

1. Rehearsal (step 46): with the cable pull dropped, should the rehearsal test something else
   mid-talk, for example "Stop and release port" in the tray menu? For the next session.

Answered by the owner on 2026-10-25 at 00:46: purple after a late arrival keeps the lateness at the
arrival (as built in e0806a6) and does not count up. Reason in the owner's words: a mark is always the
time where the owner wants to be at that slide; earlier marks do not matter once the slide is
reached. Marks go on slides where the owner can talk more or less, to win time back, not on every slide. The README
section "How to set marks" describes this.

Answered by the owner on 2026-10-24 at 22:30 and 2026-10-25 at 00:00, the dev interpretations
from Task 4 and the questions from the board tests:

1. Overtime blink alternates orange background with black digits and black background with
   orange digits: keep as implemented.
2. Digit colour white or black chosen by the background's luma: keep.
3. Long press threshold 1.0 s: keep.
4. Disconnected symbol, USB icon with an X, top right: keep.
5. Deviation line in Montserrat 28: no, far too small (22:30). New: about half the height of the
   time, about 70 px, as a generated font like step 25. Done in 8ce0500 (`digits_76`,
   Montserrat Medium 76 px); visual check PASS per the owner 2026-10-25 00:06, readable.
6. `--demo` sent `L` = 45:00, so overtime never showed in the demo: yes, `--demo` sends a short
   `L` (about 60 s) as step 32 planned. Done in d11211a (`DemoSequence.LengthSeconds = 60`).
7. Black panel after an extra USB reset (step 33): superseded 2026-10-24 23:29, the panel gets
   a hardware reset through the TCA9554 expander at every boot (e6a4712).

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
(step 38), kill switch as tray entry only (step 37), framework-dependent exe for the first talk (step 43;
replaced 2026-10-25 by the self-contained single file, step 52), `L` line `{"v":1,"L":2700}`
confirmed (step 4).
