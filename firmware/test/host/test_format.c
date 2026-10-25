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

/*
 * The time fonts of ui.c, largest first: digits_150, digits_120 (from 100:00) and digits_76 (from
 * 1000:00). Widths as above: 120 px "4" 1284 -> 80, ':' 436 -> 27; 76 px "4" 814 -> 51, ':' 276 -> 17.
 */
static const tc_cell_widths_t FONTS[] = {{DIGIT_W, COLON_W}, {80, 27}, {51, 17}};
#define FONT_COUNT 3

static int pick_for(uint32_t seconds, int32_t *width)
{
    char buf[16];
    tc_format_elapsed(buf, sizeof buf, seconds);
    int font = tc_time_font_pick(buf, 12, FONTS, FONT_COUNT, SCREEN_W);
    *width = tc_time_row_width(buf, 12, FONTS[font].digit_w, FONTS[font].colon_w);
    return font;
}

static void time_below_100_minutes_keeps_the_large_font(void)
{
    int32_t width;
    CHECK_EQ_INT(0, pick_for(0, &width));
    CHECK_EQ_INT(0, pick_for(5999, &width));
    CHECK_EQ_INT(434, width);
}

static void time_from_100_minutes_takes_the_120_px_font(void)
{
    int32_t width;
    CHECK_EQ_INT(1, pick_for(6000, &width));
    CHECK_EQ_INT(427, width);
    CHECK_EQ_INT(1, pick_for(999u * 60u + 59u, &width));
}

static void time_from_1000_minutes_takes_the_76_px_font(void)
{
    int32_t width;
    CHECK_EQ_INT(2, pick_for(1000u * 60u, &width));
    CHECK_EQ_INT(323, width);
}

static void every_reachable_time_fits_the_screen(void)
{
    /* The clock counts milliseconds in a uint32_t, so t never exceeds UINT32_MAX / 1000 s (71582:47) */
    int32_t width;
    for (uint32_t seconds = 0; seconds < 1001u * 60u; seconds += 7u) {
        pick_for(seconds, &width);
        CHECK(width <= SCREEN_W);
    }
    pick_for(UINT32_MAX / 1000u, &width);
    CHECK(width <= SCREEN_W);
}

static void pick_falls_back_to_the_last_font(void)
{
    CHECK_EQ_INT(2, tc_time_font_pick("71582788:15", 12, FONTS, FONT_COUNT, SCREEN_W));
    CHECK_EQ_INT(0, tc_time_font_pick("00:00", 12, FONTS, 1, 10));
    CHECK_EQ_INT(0, tc_time_font_pick("00:00", 12, NULL, 0, SCREEN_W));
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
    RUN(time_below_100_minutes_keeps_the_large_font);
    RUN(time_from_100_minutes_takes_the_120_px_font);
    RUN(time_from_1000_minutes_takes_the_76_px_font);
    RUN(every_reachable_time_fits_the_screen);
    RUN(pick_falls_back_to_the_last_font);
    return TEST_RESULT();
}
