/*
 * What the display shows, decided from the clock and the laptop link (D-01, D-02, D-04, D-06,
 * B-07). Plain C, no ESP-IDF or LVGL headers; ui.c only draws the result.
 */
#pragma once

#include <stdbool.h>
#include <stdint.h>

#include "timer_core/clock.h"

#ifdef __cplusplus
extern "C" {
#endif

#define TC_COLOR_BLACK 0x000000u
#define TC_COLOR_WHITE 0xFFFFFFu
#define TC_COLOR_OVERTIME 0xFF8000u /* orange */

/* Last state received from the laptop; online means a valid line arrived within the last 3 s */
typedef struct {
    bool online;
    uint32_t color;      /* background colour from the state line, 0xRRGGBB */
    bool has_deviation;  /* false when "d" was null */
    int32_t deviation_s;
} tc_link_state_t;

typedef struct {
    uint32_t seconds;
    uint32_t background; /* 0xRRGGBB */
    uint32_t foreground; /* digits and texts */
    bool show_deviation;
    int32_t deviation_s;
    bool show_disconnected;
} tc_view_t;

/*
 * Overtime: orange background with black digits in the visible blink phase, black background
 * with orange digits in the dark phase. Online otherwise: the laptop colour with white or black
 * digits, whichever contrasts. Offline: neutral (white on black) with the disconnected symbol.
 * The deviation line shows only while online and the laptop sent one.
 */
void tc_view_compose(const tc_clock_t *clock, uint32_t now_ms, const tc_link_state_t *link, tc_view_t *out);

/* Black for light backgrounds, white for dark ones (Rec. 601 luma, threshold at half scale) */
uint32_t tc_contrast_color(uint32_t background);

#ifdef __cplusplus
}
#endif
