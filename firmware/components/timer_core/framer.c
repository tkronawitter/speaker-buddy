#include "timer_core/framer.h"

void tc_framer_init(tc_framer_t *framer)
{
    if (framer == NULL) {
        return;
    }
    framer->len = 0;
    framer->too_long = false;
}

bool tc_framer_push(tc_framer_t *framer, char ch, const char **line, size_t *len)
{
    if (framer == NULL || line == NULL || len == NULL) {
        return false;
    }
    if (ch == '\n') {
        bool complete = !framer->too_long;
        *line = framer->buf;
        *len = framer->len;
        framer->len = 0;
        framer->too_long = false;
        return complete;
    }
    /* Room for TC_LINE_MAX_BYTES - 1 bytes: with the '\n' the line is at the limit */
    if (framer->len < sizeof framer->buf - 1) {
        framer->buf[framer->len++] = ch;
    } else {
        framer->too_long = true;
    }
    return false;
}
