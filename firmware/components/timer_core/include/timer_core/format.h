/* Text for the display: elapsed time and deviation (D-01). Plain C, no ESP-IDF headers. */
#pragma once

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/*
 * Elapsed time as "mm:ss" with two-digit minutes below 100 minutes ("00:07", "61:30"), then as
 * many digits as needed ("100:00"). Returns the length written, or -1 if the buffer is too small.
 */
int tc_format_elapsed(char *buf, size_t size, uint32_t seconds);

/*
 * Deviation t - m as "+m:ss" (behind) or "-m:ss" (ahead), "0:00" for zero ("+1:30", "-0:45").
 * Returns the length written, or -1 if the buffer is too small.
 */
int tc_format_deviation(char *buf, size_t size, int32_t deviation_s);

/*
 * Width of the time row when text is drawn one character per cell: colon_w for ':', digit_w for
 * every other character. Counts at most max_cells characters.
 */
int32_t tc_time_row_width(const char *text, int max_cells, int32_t digit_w, int32_t colon_w);

/*
 * Transform scale that fits a row of the given width into room, where full means unscaled (256
 * in LVGL). Full when the row fits, otherwise full * room / width rounded down, so the scaled row
 * never exceeds room.
 */
int32_t tc_time_row_scale(int32_t width, int32_t room, int32_t full);

#ifdef __cplusplus
}
#endif
