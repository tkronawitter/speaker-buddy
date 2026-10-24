/*
 * Startup screen: logo, name and version for a moment after boot, then the timer. Plain C, no
 * ESP-IDF or LVGL headers; ui.c draws it.
 */
#pragma once

#include <stdbool.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* How long the startup screen stays after boot unless the clock starts first */
#define TC_SPLASH_MS 2000u

typedef struct {
    uint32_t start_ms;
    bool done;
} tc_splash_t;

void tc_splash_init(tc_splash_t *splash, uint32_t now_ms);

/*
 * Whether the startup screen shows now. It ends after TC_SPLASH_MS or as soon as the clock runs
 * (a tap starts it), and once ended it stays ended, so pausing the clock never brings it back.
 */
bool tc_splash_visible(tc_splash_t *splash, uint32_t now_ms, bool clock_running);

#ifdef __cplusplus
}
#endif
