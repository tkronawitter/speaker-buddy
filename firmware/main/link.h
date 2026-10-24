/* Serial link to the laptop over the built-in USB Serial/JTAG port (S-01, S-02, D-04). */
#pragma once

#include <stdint.h>

#include "timer_core/clock.h"
#include "timer_core/session.h"

/*
 * Installs the USB Serial/JTAG driver (console output then goes through it as well) and starts
 * the link task: it reads laptop lines into the session and sends the status line every second
 * and on every start, pause or reset. Clock and session are shared with the LVGL task, so the
 * link task touches them only under bsp_display_lock.
 */
void link_start(tc_clock_t *clock, tc_session_t *session, uint32_t (*now_ms)(void));
