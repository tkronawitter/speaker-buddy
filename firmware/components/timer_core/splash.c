#include "timer_core/splash.h"

#include <stddef.h>

void tc_splash_init(tc_splash_t *splash, uint32_t now_ms)
{
    if (splash == NULL) {
        return;
    }
    splash->start_ms = now_ms;
    splash->done = false;
}

bool tc_splash_visible(tc_splash_t *splash, uint32_t now_ms, bool clock_running)
{
    if (splash == NULL || splash->done) {
        return false;
    }
    if (clock_running || now_ms - splash->start_ms >= TC_SPLASH_MS) {
        splash->done = true;
        return false;
    }
    return true;
}
