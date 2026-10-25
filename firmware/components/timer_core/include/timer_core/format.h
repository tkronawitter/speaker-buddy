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

/* Cell widths of one time font: the widest digit and the colon */
typedef struct {
    int32_t digit_w;
    int32_t colon_w;
} tc_cell_widths_t;

/*
 * Font for the time row: the index of the first of fonts[0..count), largest first, whose row for
 * text fits room, or the last one if none fits. The time is never scaled: a scaled row needs an
 * LVGL layer buffer larger than LVGL's memory pool and is then not drawn at all.
 */
int tc_time_font_pick(const char *text, int max_cells, const tc_cell_widths_t *fonts, int count, int32_t room);

#ifdef __cplusplus
}
#endif
