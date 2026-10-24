/* Minimal assert runner for the host tests: each test executable returns non-zero on failure. */
#pragma once

#include <stdio.h>
#include <string.h>

static int tc_test_failures;
static int tc_test_checks;

#define CHECK(cond)                                                                     \
    do {                                                                                \
        tc_test_checks++;                                                               \
        if (!(cond)) {                                                                  \
            tc_test_failures++;                                                         \
            printf("%s:%d: CHECK failed: %s\n", __FILE__, __LINE__, #cond);             \
        }                                                                               \
    } while (0)

#define CHECK_EQ_INT(expected, actual)                                                  \
    do {                                                                                \
        long long tc_e = (long long)(expected);                                         \
        long long tc_a = (long long)(actual);                                           \
        tc_test_checks++;                                                               \
        if (tc_e != tc_a) {                                                             \
            tc_test_failures++;                                                         \
            printf("%s:%d: expected %s == %lld, got %lld\n", __FILE__, __LINE__,        \
                   #actual, tc_e, tc_a);                                                \
        }                                                                               \
    } while (0)

#define CHECK_EQ_STR(expected, actual)                                                  \
    do {                                                                                \
        const char *tc_e = (expected);                                                  \
        const char *tc_a = (actual);                                                    \
        tc_test_checks++;                                                               \
        if (strcmp(tc_e, tc_a) != 0) {                                                  \
            tc_test_failures++;                                                         \
            printf("%s:%d: expected %s == \"%s\", got \"%s\"\n", __FILE__, __LINE__,    \
                   #actual, tc_e, tc_a);                                                \
        }                                                                               \
    } while (0)

#define RUN(test)                                                                       \
    do {                                                                                \
        int tc_before = tc_test_failures;                                               \
        test();                                                                         \
        printf("%s %s\n", tc_test_failures == tc_before ? "PASS" : "FAIL", #test);      \
    } while (0)

#define TEST_RESULT()                                                                   \
    (printf("%d checks, %d failed\n", tc_test_checks, tc_test_failures), tc_test_failures != 0)
