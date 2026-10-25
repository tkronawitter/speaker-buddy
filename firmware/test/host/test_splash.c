#include "test.h"
#include "timer_core/clock.h"
#include "timer_core/splash.h"

static void shows_until_the_time_is_up(void)
{
    tc_splash_t s;
    tc_splash_init(&s, 600);
    CHECK(tc_splash_visible(&s, 600, false));
    CHECK(tc_splash_visible(&s, 600 + TC_SPLASH_MS - 1, false));
    CHECK(!tc_splash_visible(&s, 600 + TC_SPLASH_MS, false));
}

static void a_running_clock_ends_it_at_once(void)
{
    tc_splash_t s;
    tc_splash_init(&s, 0);
    CHECK(tc_splash_visible(&s, 100, false));
    CHECK(!tc_splash_visible(&s, 500, true));
}

/* The sequence main.c runs: boot, a tap 500 ms in, the next 100 ms UI tick shows the timer from 0 */
static void a_tap_during_it_starts_the_clock_and_ends_it(void)
{
    tc_clock_t clock;
    tc_splash_t s;
    tc_clock_init(&clock);
    tc_splash_init(&s, 0);
    CHECK(tc_splash_visible(&s, 400, tc_clock_running(&clock)));

    tc_clock_tap(&clock, 500);
    CHECK(tc_clock_running(&clock));
    CHECK(!tc_splash_visible(&s, 600, tc_clock_running(&clock)));
    CHECK_EQ_INT(0, tc_clock_seconds(&clock, 600));
    CHECK_EQ_INT(1, tc_clock_seconds(&clock, 1500));
}

static void once_ended_it_never_returns(void)
{
    tc_splash_t s;
    tc_splash_init(&s, 0);
    CHECK(!tc_splash_visible(&s, 500, true));
    /* Paused again within the first two seconds: stays on the timer */
    CHECK(!tc_splash_visible(&s, 900, false));
    CHECK(!tc_splash_visible(&s, 1900, false));
}

static void survives_the_millisecond_counter_wrapping(void)
{
    tc_splash_t s;
    tc_splash_init(&s, UINT32_MAX - 500);
    CHECK(tc_splash_visible(&s, UINT32_MAX, false));
    CHECK(tc_splash_visible(&s, 1000, false));
    CHECK(!tc_splash_visible(&s, 1500, false));
}

static void null_is_never_visible(void)
{
    tc_splash_init(NULL, 0);
    CHECK(!tc_splash_visible(NULL, 0, false));
}

int main(void)
{
    RUN(shows_until_the_time_is_up);
    RUN(a_running_clock_ends_it_at_once);
    RUN(a_tap_during_it_starts_the_clock_and_ends_it);
    RUN(once_ended_it_never_returns);
    RUN(survives_the_millisecond_counter_wrapping);
    RUN(null_is_never_visible);
    return TEST_RESULT();
}
