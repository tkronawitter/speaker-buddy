#include "link.h"

#include <stdbool.h>
#include <stddef.h>

#include "bsp/esp-bsp.h"
#include "driver/usb_serial_jtag.h"
#include "driver/usb_serial_jtag_vfs.h"
#include "esp_log.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "timer_core/line.h"

static const char *TAG = "link";

#define LINK_READ_TIMEOUT_MS 50
#define LINK_WRITE_TIMEOUT_MS 20
#define LINK_TASK_STACK 4096
#define LINK_TASK_PRIORITY 5

static tc_clock_t *s_clock;
static tc_session_t *s_session;
static uint32_t (*s_now_ms)(void);

/* Current incoming line without terminator; a line longer than the protocol limit is dropped */
static char s_line[TC_LINE_MAX_BYTES];
static size_t s_line_len;
static bool s_line_too_long;

static void handle_line(const char *line, size_t len)
{
    tc_line_t parsed;
    if (!tc_line_parse(line, len, &parsed)) {
        return;
    }
    bsp_display_lock(0);
    uint32_t previous_length = s_clock->length_s;
    tc_session_apply(s_session, &parsed, s_clock, s_now_ms());
    uint32_t length = s_clock->length_s;
    bsp_display_unlock();

    if (parsed.type == TC_LINE_LENGTH && length != previous_length) {
        ESP_LOGI(TAG, "talk length L=%lu s", (unsigned long)length);
    } else if (parsed.type == TC_LINE_ENDED) {
        ESP_LOGI(TAG, "slide show ended");
    }
}

/* Logs link transitions once, so the console shows when the laptop came and went */
static void log_online_change(void)
{
    static bool was_online;
    bsp_display_lock(0);
    bool online = tc_session_online(s_session, s_now_ms());
    bsp_display_unlock();
    if (online != was_online) {
        ESP_LOGI(TAG, "%s", online ? "laptop connected" : "laptop lost, running offline");
        was_online = online;
    }
}

static void feed(const uint8_t *data, int count)
{
    for (int i = 0; i < count; i++) {
        char ch = (char)data[i];
        if (ch == '\n') {
            if (!s_line_too_long) {
                handle_line(s_line, s_line_len);
            }
            s_line_len = 0;
            s_line_too_long = false;
        } else if (s_line_len < sizeof s_line - 1) {
            s_line[s_line_len++] = ch;
        } else {
            s_line_too_long = true;
        }
    }
}

static void send_status_if_due(void)
{
    uint32_t t;
    bool run;
    bsp_display_lock(0);
    bool due = tc_session_status_due(s_session, s_clock, s_now_ms(), &t, &run);
    bsp_display_unlock();
    if (!due) {
        return;
    }

    /*
     * Console logs share this port. The leading newline guarantees the status line starts on a
     * line of its own even if it lands in the middle of a log line; the laptop drops the empty
     * line and the log fragments.
     */
    char line[TC_LINE_MAX_BYTES + 1];
    line[0] = '\n';
    int len = tc_line_format_status(line + 1, sizeof line - 1, t, run);
    if (len > 0) {
        usb_serial_jtag_write_bytes(line, (size_t)len + 1, pdMS_TO_TICKS(LINK_WRITE_TIMEOUT_MS));
    }
}

static void link_task(void *arg)
{
    (void)arg;
    uint8_t rx[64];
    for (;;) {
        int count = usb_serial_jtag_read_bytes(rx, sizeof rx, pdMS_TO_TICKS(LINK_READ_TIMEOUT_MS));
        if (count > 0) {
            feed(rx, count);
        }
        log_online_change();
        send_status_if_due();
    }
}

void link_start(tc_clock_t *clock, tc_session_t *session, uint32_t (*now_ms)(void))
{
    s_clock = clock;
    s_session = session;
    s_now_ms = now_ms;

    usb_serial_jtag_driver_config_t config = USB_SERIAL_JTAG_DRIVER_CONFIG_DEFAULT();
    config.rx_buffer_size = 512;
    config.tx_buffer_size = 1024;
    ESP_ERROR_CHECK(usb_serial_jtag_driver_install(&config));
    /* Console output goes through the driver too, so logs and status lines do not collide */
    usb_serial_jtag_vfs_use_driver();

    if (xTaskCreate(link_task, "link", LINK_TASK_STACK, NULL, LINK_TASK_PRIORITY, NULL) != pdPASS) {
        ESP_LOGE(TAG, "link task not started");
        return;
    }
    ESP_LOGI(TAG, "serial link started");
}
