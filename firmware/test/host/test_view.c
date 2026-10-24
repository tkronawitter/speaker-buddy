#include "test.h"
#include "timer_core/view.h"

static const tc_link_state_t offline = { false, 0, false, 0 };

static void offline_is_neutral_with_disconnected_symbol(void)
{
    tc_clock_t c;
    tc_view_t v;
    tc_clock_init(&c);
    tc_clock_tap(&c, 0);
    tc_view_compose(&c, 612000, &offline, &v);
    CHECK_EQ_INT(612, v.seconds);
    CHECK_EQ_INT(TC_COLOR_BLACK, v.background);
    CHECK_EQ_INT(TC_COLOR_WHITE, v.foreground);
    CHECK(!v.show_deviation);
    CHECK(v.show_disconnected);
}

static void online_shows_laptop_colour_and_deviation(void)
{
    tc_clock_t c;
    tc_view_t v;
    tc_link_state_t link = { true, 0x00A000, true, 90 };
    tc_clock_init(&c);
    tc_view_compose(&c, 0, &link, &v);
    CHECK_EQ_INT(0x00A000, v.background);
    CHECK_EQ_INT(TC_COLOR_WHITE, v.foreground);
    CHECK(v.show_deviation);
    CHECK_EQ_INT(90, v.deviation_s);
    CHECK(!v.show_disconnected);
}

static void online_null_deviation_hides_the_line(void)
{
    tc_clock_t c;
    tc_view_t v;
    tc_link_state_t link = { true, 0x000000, false, 77 };
    tc_clock_init(&c);
    tc_view_compose(&c, 0, &link, &v);
    CHECK(!v.show_deviation);
    CHECK_EQ_INT(0, v.deviation_s);
}

static void light_background_gets_black_digits(void)
{
    CHECK_EQ_INT(TC_COLOR_BLACK, tc_contrast_color(0xFFFFFF));
    CHECK_EQ_INT(TC_COLOR_BLACK, tc_contrast_color(0xFFFF00));
    CHECK_EQ_INT(TC_COLOR_WHITE, tc_contrast_color(0x000000));
    CHECK_EQ_INT(TC_COLOR_WHITE, tc_contrast_color(0x00A000));
    CHECK_EQ_INT(TC_COLOR_WHITE, tc_contrast_color(0xC00000));
    CHECK_EQ_INT(TC_COLOR_WHITE, tc_contrast_color(0x8000A0));
}

static void overtime_online_blinks_orange_with_black_digits(void)
{
    tc_clock_t c;
    tc_view_t v;
    tc_link_state_t link = { true, 0x8000A0, true, 120 };
    tc_clock_init(&c);
    tc_clock_set_length(&c, 2700);
    tc_clock_tap(&c, 0);

    tc_view_compose(&c, 2702000, &link, &v);
    CHECK_EQ_INT(TC_COLOR_OVERTIME, v.background);
    CHECK_EQ_INT(TC_COLOR_BLACK, v.foreground);
    CHECK(v.show_deviation);

    tc_view_compose(&c, 2703000, &link, &v);
    CHECK_EQ_INT(TC_COLOR_BLACK, v.background);
    CHECK_EQ_INT(TC_COLOR_OVERTIME, v.foreground);
}

static void overtime_offline_from_60_minutes_keeps_the_symbol(void)
{
    tc_clock_t c;
    tc_view_t v;
    tc_clock_init(&c);
    tc_clock_tap(&c, 0);

    tc_view_compose(&c, 3598000, &offline, &v);
    CHECK_EQ_INT(TC_COLOR_BLACK, v.background);
    CHECK_EQ_INT(TC_COLOR_WHITE, v.foreground);

    tc_view_compose(&c, 3600000, &offline, &v);
    CHECK_EQ_INT(TC_COLOR_OVERTIME, v.background);
    CHECK(v.show_disconnected);
}

static void link_colour_is_masked_to_24_bits(void)
{
    tc_clock_t c;
    tc_view_t v;
    tc_link_state_t link = { true, 0xFF00A000u, false, 0 };
    tc_clock_init(&c);
    tc_view_compose(&c, 0, &link, &v);
    CHECK_EQ_INT(0x00A000, v.background);
}

int main(void)
{
    RUN(offline_is_neutral_with_disconnected_symbol);
    RUN(online_shows_laptop_colour_and_deviation);
    RUN(online_null_deviation_hides_the_line);
    RUN(light_background_gets_black_digits);
    RUN(overtime_online_blinks_orange_with_black_digits);
    RUN(overtime_offline_from_60_minutes_keeps_the_symbol);
    RUN(link_colour_is_masked_to_24_bits);
    return TEST_RESULT();
}
