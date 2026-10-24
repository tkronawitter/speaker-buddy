#include "input.h"

#include "esp_log.h"

static const char *TAG = "input";

static tc_clock_t *s_clock;
static uint32_t (*s_now_ms)(void);

static void on_short_click(lv_event_t *e)
{
    (void)e;
    uint32_t now = s_now_ms();
    bool was_running = tc_clock_running(s_clock);
    tc_clock_tap(s_clock, now);
    if (!was_running) {
        ESP_LOGI(TAG, "tap: clock started at t=%lu", (unsigned long)tc_clock_seconds(s_clock, now));
    }
}

static void on_long_press(lv_event_t *e)
{
    (void)e;
    uint32_t now = s_now_ms();
    bool was_running = tc_clock_running(s_clock);
    tc_clock_long_press(s_clock, now);
    if (was_running) {
        ESP_LOGI(TAG, "long press: clock paused at t=%lu", (unsigned long)tc_clock_seconds(s_clock, now));
    } else {
        ESP_LOGI(TAG, "long press: clock reset");
    }
}

void input_attach(lv_obj_t *target, lv_indev_t *indev, tc_clock_t *clock, uint32_t (*now_ms)(void))
{
    s_clock = clock;
    s_now_ms = now_ms;
    if (indev != NULL) {
        lv_indev_set_long_press_time(indev, INPUT_LONG_PRESS_MS);
    } else {
        ESP_LOGW(TAG, "no input device, touch control disabled");
    }
    /* LVGL sends SHORT_CLICKED only for releases before the long press time, so a tap never pauses */
    lv_obj_add_event_cb(target, on_short_click, LV_EVENT_SHORT_CLICKED, NULL);
    lv_obj_add_event_cb(target, on_long_press, LV_EVENT_LONG_PRESSED, NULL);
}
