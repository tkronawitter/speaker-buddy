/*
 * Clock snapshot that survives a chip reset (not a power cut): main.c keeps it in RTC memory
 * that the reset leaves alone and restores the clock from it at boot. Plain C, no ESP-IDF headers.
 */
#pragma once

#include <stdbool.h>
#include <stdint.h>

#include "timer_core/clock.h"

#ifdef __cplusplus
extern "C" {
#endif

/* Changes whenever the layout changes, so an old layout never decodes */
#define TC_SNAPSHOT_MAGIC 0x53425431u /* "SBT1" */

typedef struct {
    uint32_t magic;
    uint32_t running;    /* 1 if the clock ran */
    uint32_t elapsed_ms; /* elapsed time at the moment of the snapshot */
    uint32_t length_s;   /* talk length L, 0 = unknown */
    uint32_t checksum;   /* over the fields above */
} tc_snapshot_t;

/* Records the clock as it stands at now_ms. */
void tc_snapshot_encode(const tc_clock_t *clock, uint32_t now_ms, tc_snapshot_t *out);

/*
 * Restores a clock from a snapshot, with now_ms as the new time base: a running clock goes on from
 * the recorded elapsed time (the reset gap itself is lost), a stopped one keeps its time. Returns
 * false and leaves *clock untouched if the snapshot is missing or damaged (magic or checksum).
 */
bool tc_snapshot_decode(const tc_snapshot_t *snapshot, uint32_t now_ms, tc_clock_t *clock);

/* Makes a snapshot fail to decode, e.g. after a power-on that left random RTC contents. */
void tc_snapshot_clear(tc_snapshot_t *snapshot);

#ifdef __cplusplus
}
#endif
