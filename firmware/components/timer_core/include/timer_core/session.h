/*
 * Link session with the laptop (S-02, D-04): applies received lines, decides online or offline,
 * and when to send the status line. Plain C, no ESP-IDF headers; main/link.c does the I/O.
 */
#pragma once

#include <stdbool.h>
#include <stdint.h>

#include "timer_core/clock.h"
#include "timer_core/line.h"
#include "timer_core/view.h"

#ifdef __cplusplus
extern "C" {
#endif

/* Without a valid laptop line for this long the link counts as lost (D-04) */
#define TC_LINK_TIMEOUT_MS 3000u
/* The status line goes out at least this often (S-02) */
#define TC_STATUS_PERIOD_MS 1000u

typedef struct {
    bool received_any;
    uint32_t last_rx_ms;
    uint32_t color;
    bool has_deviation;
    int32_t deviation_s;

    bool sent_any;
    uint32_t last_sent_ms;
    bool last_run;
    uint32_t last_t;
} tc_session_t;

void tc_session_init(tc_session_t *session);

/*
 * Applies a valid laptop line: state lines set colour and deviation, length lines set L on the
 * clock, and the end line returns the display to neutral. Every valid line counts as contact.
 */
void tc_session_apply(tc_session_t *session, const tc_line_t *line, tc_clock_t *clock, uint32_t now_ms);

/* True while the last valid line is less than TC_LINK_TIMEOUT_MS old */
bool tc_session_online(const tc_session_t *session, uint32_t now_ms);

/* Link state for tc_view_compose */
void tc_session_link_state(const tc_session_t *session, uint32_t now_ms, tc_link_state_t *out);

/*
 * True when a status line is due: none sent yet, the period elapsed, the clock started or
 * stopped, or t went back (reset). On true, *t and *run hold the values to send and are recorded
 * as sent.
 */
bool tc_session_status_due(tc_session_t *session, const tc_clock_t *clock, uint32_t now_ms, uint32_t *t,
                           bool *run);

#ifdef __cplusplus
}
#endif
