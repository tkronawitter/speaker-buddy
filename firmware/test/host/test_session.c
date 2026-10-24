#include "test.h"
#include "timer_core/session.h"

static tc_line_t state_line(uint32_t color, bool has_deviation, int32_t deviation_s)
{
    tc_line_t l = { 0 };
    l.type = TC_LINE_STATE;
    l.color = color;
    l.has_deviation = has_deviation;
    l.deviation_s = deviation_s;
    return l;
}

static void offline_until_the_first_line(void)
{
    tc_session_t s;
    tc_session_init(&s);
    CHECK(!tc_session_online(&s, 0));
    CHECK(!tc_session_online(&s, 100000));
}

static void state_line_sets_colour_and_deviation_and_goes_online(void)
{
    tc_session_t s;
    tc_clock_t c;
    tc_link_state_t link;
    tc_session_init(&s);
    tc_clock_init(&c);
    tc_line_t l = state_line(0x00A000, true, 90);
    tc_session_apply(&s, &l, &c, 5000);
    tc_session_link_state(&s, 5000, &link);
    CHECK(link.online);
    CHECK_EQ_INT(0x00A000, link.color);
    CHECK(link.has_deviation);
    CHECK_EQ_INT(90, link.deviation_s);
}

static void link_is_lost_after_three_seconds_without_a_line(void)
{
    tc_session_t s;
    tc_clock_t c;
    tc_session_init(&s);
    tc_clock_init(&c);
    tc_line_t l = state_line(0x00A000, false, 0);
    tc_session_apply(&s, &l, &c, 10000);
    CHECK(tc_session_online(&s, 12999));
    CHECK(!tc_session_online(&s, 13000));
    tc_session_apply(&s, &l, &c, 14000);
    CHECK(tc_session_online(&s, 14000));
}

static void online_check_survives_tick_wraparound(void)
{
    tc_session_t s;
    tc_clock_t c;
    tc_session_init(&s);
    tc_clock_init(&c);
    tc_line_t l = state_line(0, false, 0);
    tc_session_apply(&s, &l, &c, UINT32_MAX - 500u);
    CHECK(tc_session_online(&s, 1000u));
    CHECK(!tc_session_online(&s, 2600u));
}

static void length_line_sets_the_clock_length(void)
{
    tc_session_t s;
    tc_clock_t c;
    tc_session_init(&s);
    tc_clock_init(&c);
    tc_line_t l = { 0 };
    l.type = TC_LINE_LENGTH;
    l.length_s = 2700;
    tc_session_apply(&s, &l, &c, 0);
    CHECK_EQ_INT(2700, c.length_s);
    CHECK(tc_session_online(&s, 0));
}

static void null_length_clears_the_length_and_its_overtime(void)
{
    tc_session_t s;
    tc_clock_t c;
    tc_session_init(&s);
    tc_clock_init(&c);
    tc_line_t l = { 0 };
    l.type = TC_LINE_LENGTH;
    l.length_s = 60;
    tc_session_apply(&s, &l, &c, 0);
    tc_clock_tap(&c, 0);
    CHECK(tc_clock_overtime(&c, 61000u, true));

    l.length_s = 0; /* {"v":1,"L":null} */
    tc_session_apply(&s, &l, &c, 61000u);
    CHECK_EQ_INT(0, c.length_s);
    CHECK(!tc_clock_overtime(&c, 62000u, true));
}

static void ended_line_returns_to_neutral_but_stays_online(void)
{
    tc_session_t s;
    tc_clock_t c;
    tc_link_state_t link;
    tc_session_init(&s);
    tc_clock_init(&c);
    tc_line_t l = state_line(0x8000A0, true, 120);
    tc_session_apply(&s, &l, &c, 0);
    tc_line_t end = { 0 };
    end.type = TC_LINE_ENDED;
    tc_session_apply(&s, &end, &c, 1000);
    tc_session_link_state(&s, 1000, &link);
    CHECK(link.online);
    CHECK_EQ_INT(TC_COLOR_BLACK, link.color);
    CHECK(!link.has_deviation);
}

static void status_goes_out_first_then_every_second(void)
{
    tc_session_t s;
    tc_clock_t c;
    uint32_t t;
    bool run;
    tc_session_init(&s);
    tc_clock_init(&c);
    CHECK(tc_session_status_due(&s, &c, 0, &t, &run));
    CHECK_EQ_INT(0, t);
    CHECK(!run);
    CHECK(!tc_session_status_due(&s, &c, 999, &t, &run));
    CHECK(tc_session_status_due(&s, &c, 1000, &t, &run));
}

static void status_goes_out_at_once_on_start_pause_and_reset(void)
{
    tc_session_t s;
    tc_clock_t c;
    uint32_t t;
    bool run;
    tc_session_init(&s);
    tc_clock_init(&c);
    tc_session_status_due(&s, &c, 0, &t, &run);

    tc_clock_tap(&c, 100);
    CHECK(tc_session_status_due(&s, &c, 100, &t, &run));
    CHECK(run);

    tc_session_status_due(&s, &c, 5100, &t, &run);
    CHECK_EQ_INT(5, t);
    tc_clock_long_press(&c, 5200);
    CHECK(tc_session_status_due(&s, &c, 5200, &t, &run));
    CHECK(!run);
    CHECK_EQ_INT(5, t);

    tc_clock_long_press(&c, 5300);
    CHECK(tc_session_status_due(&s, &c, 5300, &t, &run));
    CHECK_EQ_INT(0, t);
}

int main(void)
{
    RUN(offline_until_the_first_line);
    RUN(state_line_sets_colour_and_deviation_and_goes_online);
    RUN(link_is_lost_after_three_seconds_without_a_line);
    RUN(online_check_survives_tick_wraparound);
    RUN(length_line_sets_the_clock_length);
    RUN(null_length_clears_the_length_and_its_overtime);
    RUN(ended_line_returns_to_neutral_but_stays_online);
    RUN(status_goes_out_first_then_every_second);
    RUN(status_goes_out_at_once_on_start_pause_and_reset);
    return TEST_RESULT();
}
