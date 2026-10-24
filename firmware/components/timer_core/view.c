#include "timer_core/view.h"

uint32_t tc_contrast_color(uint32_t background)
{
    uint32_t r = (background >> 16) & 0xFFu;
    uint32_t g = (background >> 8) & 0xFFu;
    uint32_t b = background & 0xFFu;
    uint32_t luma = (299u * r + 587u * g + 114u * b) / 1000u;
    return luma >= 128u ? TC_COLOR_BLACK : TC_COLOR_WHITE;
}

void tc_view_compose(const tc_clock_t *clock, uint32_t now_ms, const tc_link_state_t *link, tc_view_t *out)
{
    out->seconds = tc_clock_seconds(clock, now_ms);
    out->show_deviation = link->online && link->has_deviation;
    out->deviation_s = out->show_deviation ? link->deviation_s : 0;
    out->show_disconnected = !link->online;

    if (tc_clock_overtime(clock, now_ms, link->online)) {
        bool visible = tc_blink_visible(now_ms);
        out->background = visible ? TC_COLOR_OVERTIME : TC_COLOR_BLACK;
        out->foreground = visible ? TC_COLOR_BLACK : TC_COLOR_OVERTIME;
    } else if (link->online) {
        out->background = link->color & 0xFFFFFFu;
        out->foreground = tc_contrast_color(out->background);
    } else {
        out->background = TC_COLOR_BLACK;
        out->foreground = TC_COLOR_WHITE;
    }
}
