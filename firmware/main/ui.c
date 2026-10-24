#include "ui.h"

#include <stdbool.h>

#include "lvgl.h"
#include "timer_core/format.h"

/*
 * Font for the elapsed time. Plan step 25 replaces it with a generated digits-only font sized to
 * fill the landscape width for "88:88"; the built-in Montserrat 48 stands in until then.
 */
#define UI_TIME_FONT (&lv_font_montserrat_48)
#define UI_SMALL_FONT (&lv_font_montserrat_28)

static lv_obj_t *s_screen;
static lv_obj_t *s_time;
static lv_obj_t *s_deviation;
static lv_obj_t *s_disconnected;
static tc_view_t s_last;
static bool s_drawn;

static bool same_view(const tc_view_t *a, const tc_view_t *b)
{
    return a->seconds == b->seconds && a->background == b->background && a->foreground == b->foreground
           && a->show_deviation == b->show_deviation && a->deviation_s == b->deviation_s
           && a->show_disconnected == b->show_disconnected;
}

static lv_obj_t *create_label(const lv_font_t *font)
{
    lv_obj_t *label = lv_label_create(s_screen);
    lv_obj_set_style_text_font(label, font, 0);
    lv_label_set_text(label, "");
    return label;
}

void ui_create(void)
{
    s_screen = lv_screen_active();
    lv_obj_set_style_bg_opa(s_screen, LV_OPA_COVER, 0);
    lv_obj_set_style_pad_all(s_screen, 0, 0);
    lv_obj_set_scrollable(s_screen, false);

    s_time = create_label(UI_TIME_FONT);
    lv_obj_align(s_time, LV_ALIGN_CENTER, 0, -24);

    s_deviation = create_label(UI_SMALL_FONT);
    lv_obj_align(s_deviation, LV_ALIGN_BOTTOM_MID, 0, -40);

    s_disconnected = create_label(UI_SMALL_FONT);
    lv_label_set_text(s_disconnected, LV_SYMBOL_USB " " LV_SYMBOL_CLOSE);
    lv_obj_align(s_disconnected, LV_ALIGN_TOP_RIGHT, -16, 16);

    s_drawn = false;
}

void ui_render(const tc_view_t *view)
{
    if (s_drawn && same_view(view, &s_last)) {
        return;
    }

    char text[24];
    lv_color_t fg = lv_color_hex(view->foreground);

    lv_obj_set_style_bg_color(s_screen, lv_color_hex(view->background), 0);

    if (tc_format_elapsed(text, sizeof text, view->seconds) > 0) {
        lv_label_set_text(s_time, text);
    }
    lv_obj_set_style_text_color(s_time, fg, 0);

    lv_obj_set_hidden(s_deviation, !view->show_deviation);
    if (view->show_deviation && tc_format_deviation(text, sizeof text, view->deviation_s) > 0) {
        lv_label_set_text(s_deviation, text);
    }
    lv_obj_set_style_text_color(s_deviation, fg, 0);

    lv_obj_set_hidden(s_disconnected, !view->show_disconnected);
    lv_obj_set_style_text_color(s_disconnected, fg, 0);

    s_last = *view;
    s_drawn = true;
}
