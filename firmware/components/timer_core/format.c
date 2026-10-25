#include "timer_core/format.h"

#include <stdio.h>

static int checked(int written, size_t size)
{
    return (written < 0 || (size_t)written >= size) ? -1 : written;
}

int tc_format_elapsed(char *buf, size_t size, uint32_t seconds)
{
    if (buf == NULL || size == 0) {
        return -1;
    }
    unsigned long minutes = (unsigned long)(seconds / 60u);
    unsigned long rest = (unsigned long)(seconds % 60u);
    return checked(snprintf(buf, size, "%02lu:%02lu", minutes, rest), size);
}

int tc_format_deviation(char *buf, size_t size, int32_t deviation_s)
{
    if (buf == NULL || size == 0) {
        return -1;
    }
    /* Widen before negating so INT32_MIN does not overflow */
    int64_t value = deviation_s;
    const char *sign = value > 0 ? "+" : (value < 0 ? "-" : "");
    uint64_t magnitude = (uint64_t)(value < 0 ? -value : value);
    return checked(snprintf(buf, size, "%s%lu:%02lu", sign, (unsigned long)(magnitude / 60u),
                            (unsigned long)(magnitude % 60u)), size);
}

int32_t tc_time_row_width(const char *text, int max_cells, int32_t digit_w, int32_t colon_w)
{
    int32_t width = 0;
    if (text == NULL) {
        return 0;
    }
    for (int i = 0; i < max_cells && text[i] != '\0'; i++) {
        width += text[i] == ':' ? colon_w : digit_w;
    }
    return width;
}

int tc_time_font_pick(const char *text, int max_cells, const tc_cell_widths_t *fonts, int count, int32_t room)
{
    if (fonts == NULL || count <= 0) {
        return 0;
    }
    for (int i = 0; i < count - 1; i++) {
        if (tc_time_row_width(text, max_cells, fonts[i].digit_w, fonts[i].colon_w) <= room) {
            return i;
        }
    }
    return count - 1;
}
