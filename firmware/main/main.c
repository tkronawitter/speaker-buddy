/* Board bring-up: solid background, "hello" label, touch coordinates on the console. */
#include "bsp/esp-bsp.h"
#include "esp_log.h"
#include "lvgl.h"

static const char *TAG = "speaker_timer";

static void on_touch(lv_event_t *e)
{
    lv_point_t point;
    lv_indev_get_point(lv_indev_active(), &point);
    ESP_LOGI(TAG, "touch %s x=%d y=%d",
             lv_event_get_code(e) == LV_EVENT_PRESSED ? "down" : "up", (int)point.x, (int)point.y);
}

void app_main(void)
{
    lv_display_t *disp = bsp_display_start();
    if (disp == NULL) {
        ESP_LOGE(TAG, "display start failed");
        return;
    }

    /* Panel IO is shared with the LVGL flush: every panel command and LVGL call goes under the lock */
    bsp_display_lock(0);
    bsp_display_brightness_set(80);
    bsp_display_rotate(disp, LV_DISPLAY_ROTATION_90);

    lv_obj_t *scr = lv_screen_active();
    lv_obj_set_style_bg_color(scr, lv_color_hex(0x00A000), 0);
    lv_obj_set_style_bg_opa(scr, LV_OPA_COVER, 0);
    lv_obj_add_event_cb(scr, on_touch, LV_EVENT_PRESSED, NULL);
    lv_obj_add_event_cb(scr, on_touch, LV_EVENT_RELEASED, NULL);

    lv_obj_t *label = lv_label_create(scr);
    lv_label_set_text(label, "hello");
    lv_obj_set_style_text_color(label, lv_color_white(), 0);
    lv_obj_set_style_text_font(label, &lv_font_montserrat_48, 0);
    lv_obj_center(label);

    bsp_display_unlock();
    ESP_LOGI(TAG, "bring-up screen shown");
}
