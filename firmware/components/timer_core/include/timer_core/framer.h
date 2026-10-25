/*
 * Splits the bytes from the laptop into protocol lines (S-02): a line ends at '\n' and may be at most
 * TC_LINE_MAX_BYTES long including the '\n'; a longer one is dropped whole. Plain C, no ESP-IDF.
 */
#pragma once

#include <stdbool.h>
#include <stddef.h>

#include "timer_core/line.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef struct {
    char buf[TC_LINE_MAX_BYTES]; /* the line so far, without the '\n' */
    size_t len;
    bool too_long; /* the line passed the limit; drop it at its '\n' */
} tc_framer_t;

void tc_framer_init(tc_framer_t *framer);

/*
 * Feeds one byte. Returns true when ch ended a line within the limit, with *line and *len set to
 * it (without the '\n', a '\r' before it kept; valid until the next call). Lines may arrive split
 * across any number of reads.
 */
bool tc_framer_push(tc_framer_t *framer, char ch, const char **line, size_t *len);

#ifdef __cplusplus
}
#endif
