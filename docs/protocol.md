# Serial protocol

This is the single definition of the lines exchanged between the timer (ESP32-S3 firmware) and
the laptop program. Both sides implement against this page. The protocol does not depend on the
transport: stage 1 uses USB serial, a later Bluetooth LE transport carries the same lines.

## Framing

- Transport: USB Serial/JTAG of the ESP32-S3 (VID `303A`, PID `1001`), seen as a CDC serial port
  on Windows. The laptop opens the port with DTR and RTS deasserted so the board does not reset.
- One JSON object per line, UTF-8, terminated by `\n`. A receiver also accepts `\r\n`.
- A line is at most **128 bytes**, including the terminator.
- Every line carries the protocol version `v`. This page describes `"v":1`.

## Receiver rules

A receiver drops a line, without an error reply and without changing its state, when the line

- is longer than 128 bytes,
- is not a valid JSON object,
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

## Laptop to timer

### State

```json
{"v":1,"c":"#00A000","d":90}
```

| Field | Type | Meaning |
| --- | --- | --- |
| `c` | string `#RRGGBB` | Background colour to show. The laptop decides the colour, so colours change without reflashing |
| `d` | integer seconds, or `null` | Deviation from the last reached mark, `t - m`: positive means behind plan (`+1:30`), negative ahead (`-0:45`). `null` hides the deviation line |

Sent once per second and immediately on every change of `c` or `d`.

### Talk length

```json
{"v":1,"L":2700}
```

| Field | Type | Meaning |
| --- | --- | --- |
| `L` | integer, seconds | Planned talk length. With it the timer itself starts slow red blinking when `t > L` (rule B-07) and keeps blinking when the laptop disappears |

Sent once after the connection is established and again whenever `L` changes.

### End of slide show

```json
{"v":1,"state":"ended"}
```

Sent once when the PowerPoint slide show ends. The timer keeps its clock running and returns to
the neutral display.

## Connection loss

The timer treats the link as lost when no valid laptop line arrived for 3 seconds. It keeps
counting from its own `t`, shows the neutral colour with a small "disconnected" symbol, and keeps
blinking if `t > L`. The next valid laptop line restores the connected display.

## Versioning

A change that an old receiver would misread gets a new `v`. Receivers drop lines with a version
they do not know, so an old timer and a new laptop program fail safe to the offline display.
