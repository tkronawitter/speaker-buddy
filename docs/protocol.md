# Serial protocol

This is the single definition of the lines exchanged between the timer (ESP32-S3 firmware) and
the laptop program. Both sides implement against this page. The protocol does not depend on the
transport: v0.x uses USB serial, a later Bluetooth LE transport is meant to carry the same lines.

## Who does what

- The timer owns the clock. Only touch starts, pauses and resets it: a tap starts it, holding for
  1 second pauses it, holding for 1 second while paused resets it to 0. The laptop never sets `t`.
- The laptop program reads PowerPoint, evaluates the marks and tells the timer which colour and
  which deviation to show.
- The timer decides overtime itself, from `t` and the talk length `L`, so it keeps working when the
  laptop is gone.

## Framing

- Transport: USB Serial/JTAG of the ESP32-S3 (VID `303A`, PID `1001`), seen as a CDC serial port
  on Windows. The laptop opens the port with DTR and RTS deasserted so the board does not reset.
- One JSON object per line, UTF-8, terminated by `\n`. A receiver also accepts `\r\n`.
- The object is flat: every value is a string, an integer, a boolean or `null`. Strings carry no
  escape sequences. This keeps the parser on the timer small.
- A line is at most **128 bytes**, including the terminator.
- Every line carries the protocol version `v`. This page describes `"v":1`.

## Receiver rules

A receiver drops a line, without an error reply and without changing its state, when the line

- is longer than 128 bytes,
- is not a valid JSON object,
- contains a nested object or array, or a backslash (an escape sequence in a string),
- repeats a field name,
- has no `v` field or a `v` other than `1`,
- matches none of the line types below.

Unknown extra fields in an otherwise known line are ignored. Dropping is silent on the wire; each
side may log it locally.

## Timer to laptop

### Status

```json
{"v":1,"t":612,"run":true}
```

| Field | Type | Meaning |
| --- | --- | --- |
| `t` | integer, seconds | Elapsed talk time as counted by the timer |
| `run` | boolean | `true` while the clock runs, `false` while paused or reset |

Sent once per second and immediately on every change of `run` or a reset of `t`. The laptop
treats `t` = 0 with `run` = `false` as a reset of the timer and starts its evaluation over.

### Firmware version

```json
{"v":1,"fw":"v0.1.0-3-gabc1234"}
```

| Field | Type | Meaning |
| --- | --- | --- |
| `fw` | string | Firmware version from `git describe` at build time, 1 to 40 characters of `0-9 A-Z a-z . _ + -`. The timer replaces any other character with `_`, cuts longer versions to 40 characters and sends `unknown` for an empty one. The laptop drops a line whose `fw` does not match |

Sent once at startup, after the first valid laptop line that follows a connect, and every 10 seconds, so a laptop
that has not sent anything yet still learns it. The laptop program logs it next to its own
version.

## Laptop to timer

### State

```json
{"v":1,"c":"#8000A0","d":90}
```

| Field | Type | Meaning |
| --- | --- | --- |
| `c` | string `#RRGGBB` | Background colour to show. The laptop decides the colour, so colours change without reflashing. The timer draws the digits in white or black, whichever contrasts |
| `d` | integer seconds, or `null` | The deviation line under the time; positive means behind plan (shown as `+1:30`), negative ahead (`-0:45`), `null` shows no line |

What `d` holds always matches the colour:

| Colour | `d` |
| --- | --- |
| Green, within the tolerance of the mark | `null` |
| Red, ahead of the last reached mark | `t` minus that mark, negative, updated every second |
| Purple, arrived late at the last reached mark | the lateness on arrival, fixed |
| Purple, the next mark is overdue before its slide comes up | `t` minus that next mark, positive, updated every second |
| Neutral | `null` |

Neutral is black (`#000000`) with no deviation: `{"v":1,"c":"#000000","d":null}`. It is shown
before the first mark is reached (unless that mark is already overdue, which is purple) and when
no slide show runs.

Sent once per second and immediately on every change of `c` or `d`. The line is also the
keepalive: while connected, the laptop sends at least one line per second. With no slide show
running it sends the neutral state; before the timer's first status line it repeats the last state,
or the neutral state if there is none yet; while PowerPoint is busy it repeats the last state. So the timer shows its "disconnected" symbol only when the cable
or the laptop program is gone.

### Talk length

```json
{"v":1,"L":2700}
```

| Field | Type | Meaning |
| --- | --- | --- |
| `L` | integer seconds above 0, or `null` | Planned talk length. Once `t > L` the timer shows overtime by itself and keeps it when the laptop disappears. `null` means the length is unknown: the timer clears its `L`. `0` and negative values are dropped |

Overtime display: orange background with black digits, swapping to black with orange digits every
second.

Sent once after the connection is established and again whenever `L` changes. When the length is
unknown, the laptop sends `{"v":1,"L":null}` after connecting and at every slide show start that
leaves `L` unknown. When a show ends, the laptop forgets the length taken from that deck and
sends `{"v":1,"L":null}`; a length given with `--length` stays and is not sent again.

### End of slide show

```json
{"v":1,"state":"ended"}
```

Sent once when the PowerPoint slide show ends. The timer keeps its clock running and returns to
the neutral display. The line itself does not clear `L`, so without a start length the laptop
follows it with `{"v":1,"L":null}`: the timer drops the deck's length and an orange overtime
display ends with the show, while `t` keeps counting. With `--length` the laptop sends no `L`
line, the timer keeps that length and stays orange past it. After that the laptop keeps sending
the neutral state once per second, so the timer stays connected.

## Connection loss

The timer treats the link as lost when no valid laptop line arrived for 3 seconds. It keeps
counting from its own `t`, shows white digits on black with a small "disconnected" symbol, and
stays in overtime if `t > L`. Without the laptop it also switches to overtime from 60:00. The next
valid laptop line restores the connected display.

## A typical session

1. The timer is plugged in; it shows its startup screen, then `00:00`, stopped.
2. The laptop program finds the port, opens it and sends `{"v":1,"L":null}` (or the length from
   `--length`), then the neutral state. The timer answers with status lines and its firmware version.
3. The speaker taps the timer: status lines now carry `"run":true`.
4. The slide show starts. The laptop reads the marks and the talk length from the notes, sends
   `{"v":1,"L":2700}`, then a state line every second: neutral until the first mark, then green,
   red or purple with `d`.
5. Past `L` the timer blinks orange, whatever colour the laptop sends.
6. The show ends: the laptop sends `{"v":1,"state":"ended"}` and `{"v":1,"L":null}`, then the
   neutral state every second. The timer leaves overtime and keeps counting.

## Versioning

A change that an old receiver would misread gets a new `v`. Receivers drop lines with a version
they do not know, so an old timer and a new laptop program fail safe to the offline display.
