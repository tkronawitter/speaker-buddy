#include "test.h"
#include "timer_core/framer.h"

/* Feeds text and returns how many lines came out; the last one is copied to out */
static int feed(tc_framer_t *f, const char *text, size_t n, char *out, size_t *out_len)
{
    int lines = 0;
    for (size_t i = 0; i < n; i++) {
        const char *line;
        size_t len;
        if (tc_framer_push(f, text[i], &line, &len)) {
            memcpy(out, line, len);
            out[len] = '\0';
            *out_len = len;
            lines++;
        }
    }
    return lines;
}

static void fill(char *buf, size_t chars, const char *end)
{
    memset(buf, 'x', chars);
    strcpy(buf + chars, end);
}

static void a_line_of_127_bytes_plus_newline_is_accepted(void)
{
    tc_framer_t f;
    tc_framer_init(&f);
    char in[200], out[200];
    size_t len = 0;
    fill(in, TC_LINE_MAX_BYTES - 1, "\n");
    CHECK_EQ_INT(1, feed(&f, in, strlen(in), out, &len));
    CHECK_EQ_INT(TC_LINE_MAX_BYTES - 1, len);
}

static void a_line_of_128_bytes_plus_newline_is_dropped_and_the_next_accepted(void)
{
    tc_framer_t f;
    tc_framer_init(&f);
    char in[400], out[200];
    size_t len = 0;
    fill(in, TC_LINE_MAX_BYTES, "\n{\"v\":1,\"L\":5}\n");
    CHECK_EQ_INT(1, feed(&f, in, strlen(in), out, &len));
    CHECK_EQ_STR("{\"v\":1,\"L\":5}", out);
}

static void a_line_of_126_bytes_plus_crlf_is_accepted_with_its_cr(void)
{
    tc_framer_t f;
    tc_framer_init(&f);
    char in[200], out[200];
    size_t len = 0;
    fill(in, TC_LINE_MAX_BYTES - 2, "\r\n");
    CHECK_EQ_INT(1, feed(&f, in, strlen(in), out, &len));
    CHECK_EQ_INT(TC_LINE_MAX_BYTES - 1, len);
    CHECK(out[len - 1] == '\r');
}

static void a_line_split_across_reads_comes_out_whole(void)
{
    tc_framer_t f;
    tc_framer_init(&f);
    char out[200];
    size_t len = 0;
    const char *parts[] = { "{\"v\":1,", "\"L\":27", "00}\n{\"v\"", ":1,\"L\":5}\n" };
    CHECK_EQ_INT(0, feed(&f, parts[0], strlen(parts[0]), out, &len));
    CHECK_EQ_INT(0, feed(&f, parts[1], strlen(parts[1]), out, &len));
    CHECK_EQ_INT(1, feed(&f, parts[2], strlen(parts[2]), out, &len));
    CHECK_EQ_STR("{\"v\":1,\"L\":2700}", out);
    CHECK_EQ_INT(1, feed(&f, parts[3], strlen(parts[3]), out, &len));
    CHECK_EQ_STR("{\"v\":1,\"L\":5}", out);
}

static void an_empty_line_comes_out_empty(void)
{
    tc_framer_t f;
    tc_framer_init(&f);
    char out[200];
    size_t len = 99;
    CHECK_EQ_INT(1, feed(&f, "\n", 1, out, &len));
    CHECK_EQ_INT(0, len);
}

static void null_arguments_are_ignored(void)
{
    tc_framer_t f;
    const char *line;
    size_t len;
    tc_framer_init(NULL);
    tc_framer_init(&f);
    CHECK(!tc_framer_push(NULL, '\n', &line, &len));
    CHECK(!tc_framer_push(&f, '\n', NULL, &len));
    CHECK(!tc_framer_push(&f, '\n', &line, NULL));
}

int main(void)
{
    RUN(a_line_of_127_bytes_plus_newline_is_accepted);
    RUN(a_line_of_128_bytes_plus_newline_is_dropped_and_the_next_accepted);
    RUN(a_line_of_126_bytes_plus_crlf_is_accepted_with_its_cr);
    RUN(a_line_split_across_reads_comes_out_whole);
    RUN(an_empty_line_comes_out_empty);
    RUN(null_arguments_are_ignored);
    return TEST_RESULT();
}
