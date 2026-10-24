#include "test.h"
#include "timer_core/clock.h"

static void starts_stopped_at_zero(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    CHECK(!tc_clock_running(&c));
    CHECK_EQ_INT(0, tc_clock_seconds(&c, 5000));
}

static void tap_starts_and_counts(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_tap(&c, 1000);
    CHECK(tc_clock_running(&c));
    CHECK_EQ_INT(0, tc_clock_seconds(&c, 1999));
    CHECK_EQ_INT(1, tc_clock_seconds(&c, 2000));
    CHECK_EQ_INT(612, tc_clock_seconds(&c, 613000));
}

static void tap_while_running_changes_nothing(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_tap(&c, 0);
    tc_clock_tap(&c, 5000);
    CHECK(tc_clock_running(&c));
    CHECK_EQ_INT(10, tc_clock_seconds(&c, 10000));
}

static void long_press_pauses_then_resets(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_tap(&c, 0);
    tc_clock_long_press(&c, 90500);
    CHECK(!tc_clock_running(&c));
    CHECK_EQ_INT(90, tc_clock_seconds(&c, 200000));

    tc_clock_long_press(&c, 210000);
    CHECK(!tc_clock_running(&c));
    CHECK_EQ_INT(0, tc_clock_seconds(&c, 220000));
}

static void tap_after_pause_resumes_from_paused_time(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_tap(&c, 0);
    tc_clock_long_press(&c, 30000);
    tc_clock_tap(&c, 100000);
    CHECK_EQ_INT(40, tc_clock_seconds(&c, 110000));
}

static void counting_survives_tick_wraparound(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_tap(&c, UINT32_MAX - 999u);
    CHECK_EQ_INT(5, tc_clock_seconds(&c, 4000u));
}

static void length_survives_reset(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_set_length(&c, 2700);
    tc_clock_tap(&c, 0);
    tc_clock_long_press(&c, 1000);
    tc_clock_long_press(&c, 2000);
    CHECK_EQ_INT(2700, c.length_s);
}

static void overtime_after_length_online_and_offline(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_set_length(&c, 2700);
    tc_clock_tap(&c, 0);
    CHECK(!tc_clock_overtime(&c, 2700000, true));
    CHECK(tc_clock_overtime(&c, 2701000, true));
    CHECK(tc_clock_overtime(&c, 2701000, false));
}

static void no_overtime_online_without_length(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_tap(&c, 0);
    CHECK(!tc_clock_overtime(&c, 4000000, true));
}

static void offline_overtime_from_60_minutes(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_tap(&c, 0);
    CHECK(!tc_clock_overtime(&c, 3599999, false));
    CHECK(tc_clock_overtime(&c, 3600000, false));
}

static void offline_overtime_from_60_minutes_even_with_longer_length(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_set_length(&c, 5400);
    tc_clock_tap(&c, 0);
    CHECK(tc_clock_overtime(&c, 3600000, false));
    CHECK(!tc_clock_overtime(&c, 3600000, true));
}

static void blink_is_one_second_on_one_second_off(void)
{
    CHECK(tc_blink_visible(0));
    CHECK(tc_blink_visible(999));
    CHECK(!tc_blink_visible(1000));
    CHECK(!tc_blink_visible(1999));
    CHECK(tc_blink_visible(2000));
}

int main(void)
{
    RUN(starts_stopped_at_zero);
    RUN(tap_starts_and_counts);
    RUN(tap_while_running_changes_nothing);
    RUN(long_press_pauses_then_resets);
    RUN(tap_after_pause_resumes_from_paused_time);
    RUN(counting_survives_tick_wraparound);
    RUN(length_survives_reset);
    RUN(overtime_after_length_online_and_offline);
    RUN(no_overtime_online_without_length);
    RUN(offline_overtime_from_60_minutes);
    RUN(offline_overtime_from_60_minutes_even_with_longer_length);
    RUN(blink_is_one_second_on_one_second_off);
    return TEST_RESULT();
}
