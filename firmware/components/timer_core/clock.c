#include "timer_core/clock.h"

static uint32_t elapsed_ms(const tc_clock_t *clock, uint32_t now_ms)
{
    /* Unsigned subtraction stays correct across a wrap of the tick */
    return clock->running ? clock->accumulated_ms + (now_ms - clock->started_ms) : clock->accumulated_ms;
}

void tc_clock_init(tc_clock_t *clock)
{
    clock->running = false;
    clock->accumulated_ms = 0;
    clock->started_ms = 0;
    clock->length_s = 0;
}

void tc_clock_tap(tc_clock_t *clock, uint32_t now_ms)
{
    if (clock->running) {
        return;
    }
    clock->started_ms = now_ms;
    clock->running = true;
}

void tc_clock_long_press(tc_clock_t *clock, uint32_t now_ms)
{
    if (clock->running) {
        clock->accumulated_ms = elapsed_ms(clock, now_ms);
        clock->running = false;
    } else {
        clock->accumulated_ms = 0;
    }
}

bool tc_clock_running(const tc_clock_t *clock)
{
    return clock->running;
}

uint32_t tc_clock_seconds(const tc_clock_t *clock, uint32_t now_ms)
{
    return elapsed_ms(clock, now_ms) / 1000u;
}

void tc_clock_set_length(tc_clock_t *clock, uint32_t length_s)
{
    clock->length_s = length_s;
}

bool tc_clock_overtime(const tc_clock_t *clock, uint32_t now_ms, bool online)
{
    uint32_t t = tc_clock_seconds(clock, now_ms);
    if (clock->length_s > 0 && t > clock->length_s) {
        return true;
    }
    return !online && t >= TC_OFFLINE_OVERTIME_S;
}

bool tc_blink_visible(uint32_t now_ms)
{
    return (now_ms / TC_BLINK_HALF_PERIOD_MS) % 2u == 0u;
}
