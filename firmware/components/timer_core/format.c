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
