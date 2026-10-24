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

int main(void)
{
    RUN(elapsed_is_mm_ss);
    RUN(elapsed_beyond_99_minutes_grows);
    RUN(elapsed_reports_a_too_small_buffer);
    RUN(deviation_has_sign_and_m_ss);
    RUN(deviation_handles_int32_min);
    RUN(deviation_reports_a_too_small_buffer);
    return TEST_RESULT();
}
