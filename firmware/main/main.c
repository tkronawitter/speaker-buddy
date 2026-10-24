/* Speaker Buddy firmware: the timer owns the elapsed time and runs on its own without a laptop. */
#include "bsp/esp-bsp.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "input.h"
#include "link.h"
#include "lvgl.h"
#include "timer_core/clock.h"
#include "timer_core/session.h"
#include "timer_core/view.h"
#include "ui.h"

static const char *TAG = "speaker_buddy";

#define UI_PERIOD_MS 100

/*
 * The panel has no reset GPIO, and the BSP never drives the TCA9554 IO expander that carries the
 * board's reset and power control lines. After a chip reset the panel can stay black and ignore
 * every command, software reset included; pulsing these lines recovers it. So pulse them before
 * every panel init, as Waveshare's own examples do (pins 0, 1, 2 and 6 low for 20 ms, then high).
 */
#define PANEL_RESET_PINS (IO_EXPANDER_PIN_NUM_0 | IO_EXPANDER_PIN_NUM_1 | IO_EXPANDER_PIN_NUM_2 | IO_EXPANDER_PIN_NUM_6)
#define PANEL_RESET_LOW_MS 20
#define PANEL_RESET_SETTLE_MS 150

/*
 * Clock and session are shared by the LVGL task (UI timer, input events) and the link task.
 * The LVGL lock guards both: LVGL callbacks run with it held, the link task takes it.
 */
static tc_clock_t s_clock;
static tc_session_t s_session;

static uint32_t now_ms(void)
{
    return (uint32_t)(esp_timer_get_time() / 1000);
}

static void on_ui_tick(lv_timer_t *timer)
{
    (void)timer;
    uint32_t now = now_ms();
    tc_link_state_t link;
    tc_view_t view;
    tc_session_link_state(&s_session, now, &link);
    tc_view_compose(&s_clock, now, &link, &view);
    ui_render(&view);
}

static void reset_panel(void)
{
    esp_io_expander_handle_t expander = bsp_io_expander_init();
    if (expander == NULL
        || esp_io_expander_set_dir(expander, PANEL_RESET_PINS, IO_EXPANDER_OUTPUT) != ESP_OK
        || esp_io_expander_set_level(expander, PANEL_RESET_PINS, 0) != ESP_OK) {
        ESP_LOGE(TAG, "panel reset via IO expander failed");
        return;
    }
    vTaskDelay(pdMS_TO_TICKS(PANEL_RESET_LOW_MS));
    if (esp_io_expander_set_level(expander, PANEL_RESET_PINS, 1) != ESP_OK) {
        ESP_LOGE(TAG, "panel reset release via IO expander failed");
    }
    vTaskDelay(pdMS_TO_TICKS(PANEL_RESET_SETTLE_MS));
}

void app_main(void)
{
    tc_clock_init(&s_clock);
    tc_session_init(&s_session);

    reset_panel();

    lv_display_t *disp = bsp_display_start();
    if (disp == NULL) {
        ESP_LOGE(TAG, "display start failed");
        return;
    }

    /* Panel IO is shared with the LVGL flush: every panel command and LVGL call goes under the lock */
    bsp_display_lock(0);
    bsp_display_brightness_set(80);
    bsp_display_rotate(disp, LV_DISPLAY_ROTATION_90);
    ui_create();
    input_attach(lv_screen_active(), bsp_display_get_input_dev(), &s_clock, now_ms);
    lv_timer_create(on_ui_tick, UI_PERIOD_MS, NULL);
    bsp_display_unlock();

    link_start(&s_clock, &s_session, now_ms);
    ESP_LOGI(TAG, "timer ready, tap to start");
}
