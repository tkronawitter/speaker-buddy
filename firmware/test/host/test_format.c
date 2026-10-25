#include <stdint.h>

#include "test.h"
#include "timer_core/format.h"

static void elapsed_is_mm_ss(void)
{
    char buf[16];
    CHECK_EQ_INT(5, tc_format_elapsed(buf, sizeof buf, 0));
    CHECK_EQ_STR("00:00", buf);
    tc_format_elapsed(buf, sizeof buf, 7);
    CHECK_EQ_STR("00:07", buf);
    tc_format_elapsed(buf, sizeof buf, 612);
    CHECK_EQ_STR("10:12", buf);
    tc_format_elapsed(buf, sizeof buf, 3690);
    CHECK_EQ_STR("61:30", buf);
    tc_format_elapsed(buf, sizeof buf, 5999);
    CHECK_EQ_STR("99:59", buf);
}

static void elapsed_beyond_99_minutes_grows(void)
{
    char buf[16];
    CHECK_EQ_INT(6, tc_format_elapsed(buf, sizeof buf, 6000));
    CHECK_EQ_STR("100:00", buf);
}

static void elapsed_reports_a_too_small_buffer(void)
{
    char buf[5];
    CHECK_EQ_INT(-1, tc_format_elapsed(buf, sizeof buf, 612));
    CHECK_EQ_INT(-1, tc_format_elapsed(NULL, 0, 612));
}

static void deviation_has_sign_and_m_ss(void)
{
    char buf[16];
    tc_format_deviation(buf, sizeof buf, 90);
    CHECK_EQ_STR("+1:30", buf);
    tc_format_deviation(buf, sizeof buf, -45);
    CHECK_EQ_STR("-0:45", buf);
    tc_format_deviation(buf, sizeof buf, 0);
    CHECK_EQ_STR("0:00", buf);
    tc_format_deviation(buf, sizeof buf, 600);
    CHECK_EQ_STR("+10:00", buf);
}

static void deviation_handles_int32_min(void)
{
    char buf[32];
    CHECK(tc_format_deviation(buf, sizeof buf, INT32_MIN) > 0);
    CHECK_EQ_STR("-35791394:08", buf);
}

static void deviation_reports_a_too_small_buffer(void)
{
    char buf[4];
    CHECK_EQ_INT(-1, tc_format_deviation(buf, sizeof buf, 90));
}

/*
 * Cell widths of the 150 px time font as LVGL reports them (fonts/digits_150.c, adv_w in 1/16 px
 * rounded): widest digit "4" 1606 -> 100 px, ':' 545 -> 34 px. The screen is 448 px wide.
 */
#define DIGIT_W 100
#define COLON_W 34
#define SCREEN_W 448
#define FULL 256

static void row_width_counts_digit_and_colon_cells(void)
{
    CHECK_EQ_INT(434, tc_time_row_width("88:88", 12, DIGIT_W, COLON_W));
    CHECK_EQ_INT(434, tc_time_row_width("11:11", 12, DIGIT_W, COLON_W));
    CHECK_EQ_INT(534, tc_time_row_width("100:00", 12, DIGIT_W, COLON_W));
    CHECK_EQ_INT(0, tc_time_row_width("", 12, DIGIT_W, COLON_W));
    CHECK_EQ_INT(0, tc_time_row_width(NULL, 12, DIGIT_W, COLON_W));
}

static void row_width_stops_at_the_cell_count(void)
{
    CHECK_EQ_INT(200, tc_time_row_width("100:00", 2, DIGIT_W, COLON_W));
}

static void time_below_100_minutes_is_not_scaled(void)
{
    char buf[16];
    tc_format_elapsed(buf, sizeof buf, 5999);
    int32_t width = tc_time_row_width(buf, 12, DIGIT_W, COLON_W);
    CHECK_EQ_INT(FULL, tc_time_row_scale(width, SCREEN_W, FULL));
}

static void time_from_100_minutes_shrinks_to_the_screen(void)
{
    char buf[16];
    tc_format_elapsed(buf, sizeof buf, 6000);
    int32_t width = tc_time_row_width(buf, 12, DIGIT_W, COLON_W);
    int32_t scale = tc_time_row_scale(width, SCREEN_W, FULL);

    CHECK_EQ_INT(214, scale);
    CHECK(width * scale / FULL <= SCREEN_W);
    /* Not more than one scale step smaller than needed */
    CHECK(width * (scale + 1) > SCREEN_W * FULL);
}

static void every_minute_count_up_to_999_fits_the_screen(void)
{
    char buf[16];
    for (uint32_t seconds = 0; seconds < 1000u * 60u; seconds += 59u) {
        tc_format_elapsed(buf, sizeof buf, seconds);
        int32_t width = tc_time_row_width(buf, 12, DIGIT_W, COLON_W);
        int32_t scale = tc_time_row_scale(width, SCREEN_W, FULL);
        CHECK(scale > 0 && scale <= FULL);
        CHECK(width * scale / FULL <= SCREEN_W);
    }
}

static void longest_time_fits_the_screen(void)
{
    char buf[16];
    CHECK_EQ_INT(11, tc_format_elapsed(buf, sizeof buf, UINT32_MAX));
    int32_t width = tc_time_row_width(buf, 12, DIGIT_W, COLON_W);
    int32_t scale = tc_time_row_scale(width, SCREEN_W, FULL);
    CHECK(scale > 0);
    CHECK(width * scale / FULL <= SCREEN_W);
}

static void scale_guards_degenerate_input(void)
{
    CHECK_EQ_INT(FULL, tc_time_row_scale(0, SCREEN_W, FULL));
    CHECK_EQ_INT(FULL, tc_time_row_scale(534, 0, FULL));
    CHECK_EQ_INT(FULL, tc_time_row_scale(SCREEN_W, SCREEN_W, FULL));
}

int main(void)
{
    RUN(elapsed_is_mm_ss);
    RUN(elapsed_beyond_99_minutes_grows);
    RUN(elapsed_reports_a_too_small_buffer);
    RUN(deviation_has_sign_and_m_ss);
    RUN(deviation_handles_int32_min);
    RUN(deviation_reports_a_too_small_buffer);
    RUN(row_width_counts_digit_and_colon_cells);
    RUN(row_width_stops_at_the_cell_count);
    RUN(time_below_100_minutes_is_not_scaled);
    RUN(time_from_100_minutes_shrinks_to_the_screen);
    RUN(every_minute_count_up_to_999_fits_the_screen);
    RUN(longest_time_fits_the_screen);
    RUN(scale_guards_degenerate_input);
    return TEST_RESULT();
}
