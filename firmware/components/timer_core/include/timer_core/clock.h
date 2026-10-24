/*
 * Talk clock of the timer: the timer owns the elapsed time t (L-03). Plain C without ESP-IDF
 * headers, so it builds for the host tests too. Time comes in as a millisecond tick from the
 * caller; unsigned wrap-around of the tick is handled.
 */
#pragma once

#include <stdbool.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Offline the timer switches to overtime from 60:00 (D-06). */
#define TC_OFFLINE_OVERTIME_S 3600u

/* Slow blink in overtime: this long visible, then this long dark (B-07). */
#define TC_BLINK_HALF_PERIOD_MS 1000u

typedef struct {
    bool running;
    uint32_t accumulated_ms; /* time counted in earlier runs since the last reset */
    uint32_t started_ms;     /* tick at which the current run started */
    uint32_t length_s;       /* talk length L from the laptop, 0 = unknown */
} tc_clock_t;

void tc_clock_init(tc_clock_t *clock);

/* Short tap (D-03): starts the clock, or resumes it after a pause. No effect while running. */
void tc_clock_tap(tc_clock_t *clock, uint32_t now_ms);

/* Long press (D-03): pauses a running clock; on a stopped clock it resets t to 0. */
void tc_clock_long_press(tc_clock_t *clock, uint32_t now_ms);

bool tc_clock_running(const tc_clock_t *clock);

/* Elapsed whole seconds. */
uint32_t tc_clock_seconds(const tc_clock_t *clock, uint32_t now_ms);

/* Sets L in seconds from the laptop line {"v":1,"L":...}; 0 clears it. Survives a reset. */
void tc_clock_set_length(tc_clock_t *clock, uint32_t length_s);

/*
 * Overtime (orange, black digits, blinking): t > L whenever L is known (B-07, also offline per
 * D-04), and offline from 60:00 (D-06).
 */
bool tc_clock_overtime(const tc_clock_t *clock, uint32_t now_ms, bool online);

/* Blink phase for overtime: true in the visible half of each period. */
bool tc_blink_visible(uint32_t now_ms);

#ifdef __cplusplus
}
#endif
