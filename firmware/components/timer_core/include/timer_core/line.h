/*
 * Serial protocol v1, timer side (S-02, docs/protocol.md). Parses laptop lines and formats the
 * timer status line. Plain C, no ESP-IDF headers, no JSON library: the protocol uses flat objects
 * only, so a small strict parser covers it.
 */
#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#define TC_PROTOCOL_VERSION 1
/* Maximum line length in bytes, including the '\n' terminator */
#define TC_LINE_MAX_BYTES 128

typedef enum {
    TC_LINE_STATE,  /* {"v":1,"c":"#RRGGBB","d":<int>|null} */
    TC_LINE_LENGTH, /* {"v":1,"L":<int > 0>} */
    TC_LINE_ENDED,  /* {"v":1,"state":"ended"} */
} tc_line_type_t;

typedef struct {
    tc_line_type_t type;
    uint32_t color;       /* TC_LINE_STATE: 0xRRGGBB */
    bool has_deviation;   /* TC_LINE_STATE: false when "d" is null */
    int32_t deviation_s;  /* TC_LINE_STATE: t - m in seconds */
    uint32_t length_s;    /* TC_LINE_LENGTH */
} tc_line_t;

/*
 * Parses one laptop line of len bytes, without the '\n'; a trailing '\r' is accepted. Returns
 * false, leaving *out untouched, for lines the protocol says to drop: too long, not a flat JSON
 * object, no integer "v" equal to 1, not exactly one of "t", "c", "L", "state", or invalid fields.
 * Timer status lines ("t") are dropped too: the timer never receives them.
 */
bool tc_line_parse(const char *line, size_t len, tc_line_t *out);

/*
 * Formats the timer status line {"v":1,"t":<t>,"run":<bool>} followed by '\n'. Returns the
 * length written, or -1 if the buffer is too small.
 */
int tc_line_format_status(char *buf, size_t size, uint32_t t, bool run);

#ifdef __cplusplus
}
#endif
