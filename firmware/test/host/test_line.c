#include <stdint.h>

#include "test.h"
#include "timer_core/line.h"

static int parse(const char *text, tc_line_t *out)
{
    return tc_line_parse(text, strlen(text), out);
}

static void state_line_with_deviation(void)
{
    tc_line_t l;
    CHECK(parse("{\"v\":1,\"c\":\"#00A000\",\"d\":90}", &l));
    CHECK_EQ_INT(TC_LINE_STATE, l.type);
    CHECK_EQ_INT(0x00A000, l.color);
    CHECK(l.has_deviation);
    CHECK_EQ_INT(90, l.deviation_s);
}

static void state_line_with_negative_and_null_deviation(void)
{
    tc_line_t l;
    CHECK(parse("{\"v\":1,\"c\":\"#c00000\",\"d\":-45}", &l));
    CHECK_EQ_INT(0xC00000, l.color);
    CHECK_EQ_INT(-45, l.deviation_s);
    CHECK(parse("{\"v\":1,\"c\":\"#000000\",\"d\":null}", &l));
    CHECK(!l.has_deviation);
}

static void length_and_ended_lines(void)
{
    tc_line_t l;
    CHECK(parse("{\"v\":1,\"L\":2700}", &l));
    CHECK_EQ_INT(TC_LINE_LENGTH, l.type);
    CHECK_EQ_INT(2700, l.length_s);
    CHECK(parse("{\"v\":1,\"state\":\"ended\"}", &l));
    CHECK_EQ_INT(TC_LINE_ENDED, l.type);
}

static void whitespace_field_order_extra_fields_and_cr_are_accepted(void)
{
    tc_line_t l;
    CHECK(parse(" { \"L\" : 60 , \"v\" : 1 , \"x\" : [1] } ", &l) == 0);
    CHECK(parse(" { \"L\" : 60 , \"v\" : 1 , \"x\" : \"y\" } ", &l));
    CHECK_EQ_INT(60, l.length_s);
    CHECK(parse("{\"v\":1,\"L\":61}\r", &l));
    CHECK_EQ_INT(61, l.length_s);
}

static void length_limit_counts_the_terminator(void)
{
    tc_line_t l;
    char line[200];
    const char *head = "{\"v\":1,\"L\":5,\"p\":\"";
    const char *tail = "\"}";
    size_t pad = (TC_LINE_MAX_BYTES - 1) - strlen(head) - strlen(tail);
    snprintf(line, sizeof line, "%s%*s%s", head, (int)pad, "", tail);
    for (size_t i = strlen(head); i < strlen(head) + pad; i++) {
        line[i] = 'x';
    }
    CHECK_EQ_INT(TC_LINE_MAX_BYTES - 1, strlen(line));
    CHECK(parse(line, &l));

    snprintf(line, sizeof line, "%s%*s%s", head, (int)pad + 1, "", tail);
    CHECK(parse(line, &l) == 0);
}

static void invalid_lines_are_dropped_and_leave_out_untouched(void)
{
    static const char *const bad[] = {
        "",
        "not json",
        "{\"v\":1,\"L\":5",
        "[1,2]",
        "{\"L\":5}",
        "{\"v\":2,\"L\":5}",
        "{\"v\":\"1\",\"L\":5}",
        "{\"v\":1.0,\"L\":5}",
        "{\"v\":1e0,\"L\":5}",
        "{\"v\":01,\"L\":5}",
        "{\"v\":1}",
        "{\"v\":1,\"x\":1}",
        "{\"v\":1,\"t\":5,\"run\":true}",
        "{\"v\":1,\"c\":\"#00A000\",\"d\":0,\"L\":5}",
        "{\"v\":1,\"c\":\"green\",\"d\":0}",
        "{\"v\":1,\"c\":\"#00A00\",\"d\":0}",
        "{\"v\":1,\"c\":\"#00A00G\",\"d\":0}",
        "{\"v\":1,\"c\":\"#00A000\"}",
        "{\"v\":1,\"c\":\"#00A000\",\"d\":\"90\"}",
        "{\"v\":1,\"c\":\"#00A000\",\"d\":1.5}",
        "{\"v\":1,\"c\":\"#00A000\",\"d\":3000000000}",
        "{\"v\":1,\"L\":0}",
        "{\"v\":1,\"L\":-5}",
        "{\"v\":1,\"L\":\"2700\"}",
        "{\"v\":1,\"state\":\"paused\"}",
        "{\"v\":1,\"state\":\"ende\"}",
        "{\"v\":1,\"L\":5,\"L\":6}",
        "{\"v\":1,\"L\":5} x",
        "{\"v\":1,\"L\":5,}",
        "{\"v\":1,\"s\\\"\":1,\"L\":5}",
        "{\"v\":1,\"L\":{\"x\":1}}",
    };
    for (size_t i = 0; i < sizeof bad / sizeof bad[0]; i++) {
        tc_line_t l = { .type = TC_LINE_ENDED, .length_s = 4242 };
        int ok = parse(bad[i], &l);
        if (ok) {
            printf("accepted but should drop: %s\n", bad[i]);
        }
        CHECK(!ok);
        CHECK_EQ_INT(4242, l.length_s);
    }
}

static void null_arguments_are_rejected(void)
{
    tc_line_t l;
    CHECK(!tc_line_parse(NULL, 0, &l));
    CHECK(!tc_line_parse("{\"v\":1,\"L\":5}", 13, NULL));
}

static void status_line_is_formatted_with_terminator(void)
{
    char buf[64];
    CHECK_EQ_INT(27, tc_line_format_status(buf, sizeof buf, 612, true));
    CHECK_EQ_STR("{\"v\":1,\"t\":612,\"run\":true}\n", buf);
    tc_line_format_status(buf, sizeof buf, 0, false);
    CHECK_EQ_STR("{\"v\":1,\"t\":0,\"run\":false}\n", buf);
}

static void status_line_reports_a_too_small_buffer(void)
{
    char buf[10];
    CHECK_EQ_INT(-1, tc_line_format_status(buf, sizeof buf, 612, true));
}

static void version_line_is_formatted_with_terminator(void)
{
    char buf[TC_LINE_MAX_BYTES];
    CHECK_EQ_INT(33, tc_line_format_version(buf, sizeof buf, "v0.1.0-3-gabc1234"));
    CHECK_EQ_STR("{\"v\":1,\"fw\":\"v0.1.0-3-gabc1234\"}\n", buf);
    tc_line_format_version(buf, sizeof buf, "1ac6a99-dirty");
    CHECK_EQ_STR("{\"v\":1,\"fw\":\"1ac6a99-dirty\"}\n", buf);
}

static void version_line_replaces_unsafe_characters_and_cuts_long_versions(void)
{
    char buf[TC_LINE_MAX_BYTES];
    tc_line_format_version(buf, sizeof buf, "a\"b\\c d/e");
    CHECK_EQ_STR("{\"v\":1,\"fw\":\"a_b_c_d_e\"}\n", buf);
    tc_line_format_version(buf, sizeof buf, "0123456789012345678901234567890123456789overflow");
    CHECK_EQ_STR("{\"v\":1,\"fw\":\"0123456789012345678901234567890123456789\"}\n", buf);
    CHECK(strlen(buf) <= TC_LINE_MAX_BYTES);
}

static void version_line_without_a_version_says_unknown(void)
{
    char buf[TC_LINE_MAX_BYTES];
    tc_line_format_version(buf, sizeof buf, "");
    CHECK_EQ_STR("{\"v\":1,\"fw\":\"unknown\"}\n", buf);
    tc_line_format_version(buf, sizeof buf, NULL);
    CHECK_EQ_STR("{\"v\":1,\"fw\":\"unknown\"}\n", buf);
    char small[10];
    CHECK_EQ_INT(-1, tc_line_format_version(small, sizeof small, "v0.1.0"));
}

static void version_line_is_not_a_laptop_line(void)
{
    tc_line_t l;
    CHECK(!parse("{\"v\":1,\"fw\":\"v0.1.0\"}", &l));
}

int main(void)
{
    RUN(state_line_with_deviation);
    RUN(state_line_with_negative_and_null_deviation);
    RUN(length_and_ended_lines);
    RUN(whitespace_field_order_extra_fields_and_cr_are_accepted);
    RUN(length_limit_counts_the_terminator);
    RUN(invalid_lines_are_dropped_and_leave_out_untouched);
    RUN(null_arguments_are_rejected);
    RUN(status_line_is_formatted_with_terminator);
    RUN(status_line_reports_a_too_small_buffer);
    RUN(version_line_is_formatted_with_terminator);
    RUN(version_line_replaces_unsafe_characters_and_cuts_long_versions);
    RUN(version_line_without_a_version_says_unknown);
    RUN(version_line_is_not_a_laptop_line);
    return TEST_RESULT();
}
