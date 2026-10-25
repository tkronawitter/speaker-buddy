# Serial protocol

This is the single definition of the lines exchanged between the timer (ESP32-S3 firmware) and
the laptop program. Both sides implement against this page. The protocol does not depend on the
transport: stage 1 uses USB serial, a later Bluetooth LE transport carries the same lines.

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
| `t` | integer, seconds | Elapsed talk time as counted by the timer. The timer owns the clock |
| `run` | boolean | `true` while the clock runs, `false` while paused or reset |

Sent once per second and immediately on every change of `run` or a reset of `t`.

### Firmware version

```json
{"v":1,"fw":"v0.1.0-3-gabc1234"}
```

| Field | Type | Meaning |
| --- | --- | --- |
| `fw` | string | Firmware version from `git describe` at build time, 1 to 40 characters of `0-9 A-Z a-z . _ + -`. The timer replaces any other character with `_`, cuts longer versions to 40 characters and sends `unknown` for an empty one. The laptop drops a line whose `fw` does not match |

Sent when the laptop comes online and every 10 seconds, so a laptop that has not sent anything
yet still learns it. The laptop program logs it next to its own version.

## Laptop to timer

### State

```json
{"v":1,"c":"#8000A0","d":90}
```

| Field | Type | Meaning |
| --- | --- | --- |
| `c` | string `#RRGGBB` | Background colour to show. The laptop decides the colour, so colours change without reflashing |
| `d` | integer seconds, or `null` | Deviation that matches the colour: positive means behind plan (`+1:30`), negative ahead (`-0:45`). Red: `t - m` against the last reached mark, live. Purple: the lateness at the arrival on the last reached mark, or, while the next mark is overdue, `t` minus that next mark, live. Green and neutral send `null`, which hides the deviation line |

Sent once per second and immediately on every change of `c` or `d`. The line is also the
keepalive: while connected, the laptop sends at least one line per second. With no slide show
running, or before the timer's first status line, it sends the neutral state
(`{"v":1,"c":"#000000","d":null}`); while PowerPoint is busy it repeats the last state. So the
timer shows its "disconnected" symbol only when the cable or the laptop program is gone.

### Talk length

```json
{"v":1,"L":2700}
```

| Field | Type | Meaning |
| --- | --- | --- |
| `L` | integer seconds above 0, or `null` | Planned talk length. With it the timer itself switches to overtime when `t > L` (rule B-07): orange background, black digits, slow blinking. It keeps that when the laptop disappears. `null` means the length is unknown: the timer clears its `L`. `0` and negative values are dropped |

Sent once after the connection is established and again whenever `L` changes. When the length is
unknown, the laptop sends `{"v":1,"L":null}` after connecting and at every slide show start that
leaves `L` unknown. When a show ends, the laptop forgets the length taken from that deck; a
length given with `--length` stays.

### End of slide show

```json
{"v":1,"state":"ended"}
```

Sent once when the PowerPoint slide show ends. The timer keeps its clock running and returns to
the neutral display. After that the laptop keeps sending the neutral state once per second, so the
timer stays connected.

## Connection loss

The timer treats the link as lost when no valid laptop line arrived for 3 seconds. It keeps
counting from its own `t`, shows the neutral colour with a small "disconnected" symbol, and stays
in overtime if `t > L`. Offline it also switches to overtime from 60:00 (D-06).
The next valid laptop line restores the connected display.

## Versioning

A change that an old receiver would misread gets a new `v`. Receivers drop lines with a version
they do not know, so an old timer and a new laptop program fail safe to the offline display.
