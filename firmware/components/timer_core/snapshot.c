#include "timer_core/snapshot.h"

#include <stddef.h>

/* FNV-1a over the words before the checksum */
static uint32_t checksum(const tc_snapshot_t *s)
{
    const uint32_t words[] = { s->magic, s->running, s->elapsed_ms, s->length_s };
    uint32_t hash = 2166136261u;
    for (size_t i = 0; i < sizeof words / sizeof words[0]; i++) {
        for (int shift = 0; shift < 32; shift += 8) {
            hash ^= (words[i] >> shift) & 0xFFu;
            hash *= 16777619u;
        }
    }
    return hash;
}

void tc_snapshot_encode(const tc_clock_t *clock, uint32_t now_ms, tc_snapshot_t *out)
{
    if (clock == NULL || out == NULL) {
        return;
    }
    out->magic = TC_SNAPSHOT_MAGIC;
    out->running = clock->running ? 1u : 0u;
    /* Same as the clock's own elapsed time; unsigned subtraction survives a tick wrap */
    out->elapsed_ms = clock->running ? clock->accumulated_ms + (now_ms - clock->started_ms) : clock->accumulated_ms;
    out->length_s = clock->length_s;
    out->checksum = checksum(out);
}

bool tc_snapshot_decode(const tc_snapshot_t *snapshot, uint32_t now_ms, tc_clock_t *clock)
{
    if (snapshot == NULL || clock == NULL || snapshot->magic != TC_SNAPSHOT_MAGIC || snapshot->running > 1u
        || snapshot->checksum != checksum(snapshot)) {
        return false;
    }
    clock->running = snapshot->running == 1u;
    clock->accumulated_ms = snapshot->elapsed_ms;
    clock->started_ms = now_ms;
    clock->length_s = snapshot->length_s;
    return true;
}

void tc_snapshot_clear(tc_snapshot_t *snapshot)
{
    if (snapshot != NULL) {
        snapshot->magic = 0;
        snapshot->checksum = 0;
    }
}
