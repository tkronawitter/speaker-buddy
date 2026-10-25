#include "test.h"
#include "timer_core/snapshot.h"

static void a_running_clock_goes_on_after_the_restore(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_set_length(&c, 2700);
    tc_clock_tap(&c, 1000);
    tc_snapshot_t s;
    tc_snapshot_encode(&c, 66000, &s); /* 65 s elapsed */

    tc_clock_t r;
    tc_clock_init(&r);
    CHECK(tc_snapshot_decode(&s, 800, &r)); /* new boot, tick restarted */
    CHECK(tc_clock_running(&r));
    CHECK_EQ_INT(65, tc_clock_seconds(&r, 800));
    CHECK_EQ_INT(75, tc_clock_seconds(&r, 10800));
    CHECK_EQ_INT(2700, r.length_s);
}

static void a_paused_clock_keeps_its_time(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_tap(&c, 0);
    tc_clock_long_press(&c, 42000); /* paused at 42 s */
    tc_snapshot_t s;
    tc_snapshot_encode(&c, 90000, &s);

    tc_clock_t r;
    tc_clock_init(&r);
    CHECK(tc_snapshot_decode(&s, 500, &r));
    CHECK(!tc_clock_running(&r));
    CHECK_EQ_INT(42, tc_clock_seconds(&r, 99999));
    CHECK_EQ_INT(0, r.length_s);
}

static void a_damaged_snapshot_is_rejected_and_leaves_the_clock(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_tap(&c, 0);
    tc_snapshot_t s;
    tc_snapshot_encode(&c, 5000, &s);

    tc_snapshot_t bad = s;
    bad.elapsed_ms ^= 1u;
    tc_clock_t r;
    tc_clock_init(&r);
    CHECK(!tc_snapshot_decode(&bad, 0, &r));
    CHECK(!tc_clock_running(&r));
    CHECK_EQ_INT(0, tc_clock_seconds(&r, 0));

    bad = s;
    bad.magic = 0x12345678u;
    CHECK(!tc_snapshot_decode(&bad, 0, &r));
    bad = s;
    bad.running = 2u;
    bad.checksum = s.checksum;
    CHECK(!tc_snapshot_decode(&bad, 0, &r));
}

static void a_cleared_snapshot_never_decodes(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_snapshot_t s;
    tc_snapshot_encode(&c, 0, &s);
    tc_snapshot_clear(&s);
    tc_clock_t r;
    tc_clock_init(&r);
    CHECK(!tc_snapshot_decode(&s, 0, &r));
}

static void random_memory_does_not_decode(void)
{
    /* What RTC memory may hold after a power-on: anything */
    tc_snapshot_t s = { 0xDEADBEEFu, 1u, 123456u, 2700u, 0x0BADF00Du };
    tc_clock_t r;
    tc_clock_init(&r);
    CHECK(!tc_snapshot_decode(&s, 0, &r));
}

static void encoding_survives_a_tick_wrap(void)
{
    tc_clock_t c;
    tc_clock_init(&c);
    tc_clock_tap(&c, UINT32_MAX - 999u);
    tc_snapshot_t s;
    tc_snapshot_encode(&c, 2000, &s); /* 3 s across the wrap */
    CHECK_EQ_INT(3000, s.elapsed_ms);
}

static void null_arguments_are_ignored(void)
{
    tc_snapshot_t s;
    tc_clock_t c;
    tc_clock_init(&c);
    tc_snapshot_encode(NULL, 0, &s);
    tc_snapshot_encode(&c, 0, NULL);
    CHECK(!tc_snapshot_decode(NULL, 0, &c));
    tc_snapshot_clear(NULL);
}

int main(void)
{
    RUN(a_running_clock_goes_on_after_the_restore);
    RUN(a_paused_clock_keeps_its_time);
    RUN(a_damaged_snapshot_is_rejected_and_leaves_the_clock);
    RUN(a_cleared_snapshot_never_decodes);
    RUN(random_memory_does_not_decode);
    RUN(encoding_survives_a_tick_wrap);
    RUN(null_arguments_are_ignored);
    return TEST_RESULT();
}
