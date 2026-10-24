#include "test.h"
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
    RUN(once_ended_it_never_returns);
    RUN(survives_the_millisecond_counter_wrapping);
    RUN(null_is_never_visible);
    return TEST_RESULT();
}
