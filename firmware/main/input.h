/* Touch control of the clock (D-03): short tap starts, long press pauses, long press again resets. */
#pragma once

#include <stdint.h>

#include "lvgl.h"
#include "timer_core/clock.h"

/* A press this long counts as a long press; shorter presses are taps */
#define INPUT_LONG_PRESS_MS 1000

/*
 * Wires tap and long press on target to the clock. Call with the display lock held. The clock is
 * then changed from the LVGL task only.
 */
void input_attach(lv_obj_t *target, lv_indev_t *indev, tc_clock_t *clock, uint32_t (*now_ms)(void));
