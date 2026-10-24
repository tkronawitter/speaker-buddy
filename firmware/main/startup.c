#include "startup.h"

#include <stdint.h>

#include "lvgl.h"

/*
 * Logo D from assets/logo/speaker-buddy.svg (viewBox 0 0 64 64), drawn with LVGL primitives at
 * LOGO_SCALE: a ring of three 46-unit segments (radius 24, width 10) in purple, green and red from
 * 12 o'clock, a dark face (radius 16), two white eyes (radius 3) and a green smile (width 3.5).
 */
#define LOGO_SCALE 2.5f
#define LOGO_TOP 30
#define TITLE_Y 205
#define VERSION_Y 268

#define COLOR_FACE 0x141414
#define COLOR_VERSION 0x9e9e9e

/*
 * Ring segments in LVGL angles (0 = 3 o'clock, clockwise): each spans 46 of 2*pi*24 units, about
 * 110 degrees, starting every 120 degrees from 12 o'clock (270)
 */
static const struct {
    uint32_t color;
    int32_t start;
    int32_t end;
} RING[] = {
    { 0x8000a0, 270, 20 },
    { 0x1a9e3a, 30, 140 },
    { 0xc00000, 150, 260 },
};

/*
 * The SVG smile is the quadratic curve M25 36 q7 6 14 0; the circle arc through its ends and its
 * lowest point (centre 32,29.33, radius 9.67) runs from 44 to 136 degrees
 */
#define SMILE_COLOR 0x1a9e3a
#define SMILE_CX 32.0f
#define SMILE_CY 29.333f
#define SMILE_R 9.667f
#define SMILE_W 3.5f

static lv_obj_t *s_root;
static int32_t s_logo_left;

static int32_t px(float units)
{
    return (int32_t)(units * LOGO_SCALE + 0.5f);
}

static lv_obj_t *create_plain(lv_obj_t *parent)
{
    lv_obj_t *obj = lv_obj_create(parent);
    lv_obj_remove_style_all(obj);
    lv_obj_set_clickable(obj, false);
    lv_obj_set_scrollable(obj, false);
    return obj;
}

/* An arc around (cx, cy) in logo units: radius is the stroke centre line, as in SVG */
static void create_arc(float cx, float cy, float radius, float width, uint32_t color, int32_t start,
                       int32_t end, bool rounded)
{
    lv_obj_t *arc = lv_arc_create(s_root);
    lv_obj_remove_style_all(arc);
    lv_obj_set_clickable(arc, false);
    float outer = radius + width / 2.0f;
    lv_obj_set_size(arc, px(2.0f * outer), px(2.0f * outer));
    lv_obj_set_pos(arc, s_logo_left + px(cx - outer), LOGO_TOP + px(cy - outer));
    lv_arc_set_bg_angles(arc, (lv_value_precise_t)start, (lv_value_precise_t)end);
    lv_obj_set_style_arc_width(arc, px(width), LV_PART_MAIN);
    lv_obj_set_style_arc_color(arc, lv_color_hex(color), LV_PART_MAIN);
    lv_obj_set_style_arc_opa(arc, LV_OPA_COVER, LV_PART_MAIN);
    lv_obj_set_style_arc_rounded(arc, rounded, LV_PART_MAIN);
}

static void create_dot(float cx, float cy, float radius, uint32_t color)
{
    lv_obj_t *dot = create_plain(s_root);
    lv_obj_set_size(dot, px(2.0f * radius), px(2.0f * radius));
    lv_obj_set_pos(dot, s_logo_left + px(cx - radius), LOGO_TOP + px(cy - radius));
    lv_obj_set_style_radius(dot, LV_RADIUS_CIRCLE, 0);
    lv_obj_set_style_bg_color(dot, lv_color_hex(color), 0);
    lv_obj_set_style_bg_opa(dot, LV_OPA_COVER, 0);
}

static void create_text(const char *text, const lv_font_t *font, uint32_t color, int32_t y)
{
    lv_obj_t *label = lv_label_create(s_root);
    lv_obj_set_clickable(label, false);
    lv_obj_set_style_text_font(label, font, 0);
    lv_obj_set_style_text_color(label, lv_color_hex(color), 0);
    lv_label_set_text(label, text);
    lv_obj_align(label, LV_ALIGN_TOP_MID, 0, y);
}

void startup_create(const char *version)
{
    s_root = create_plain(lv_layer_top());
    lv_obj_set_size(s_root, lv_pct(100), lv_pct(100));
    lv_obj_set_style_bg_color(s_root, lv_color_black(), 0);
    lv_obj_set_style_bg_opa(s_root, LV_OPA_COVER, 0);
    s_logo_left = (lv_obj_get_width(lv_screen_active()) - px(64.0f)) / 2;

    for (size_t i = 0; i < sizeof RING / sizeof RING[0]; i++) {
        create_arc(32.0f, 32.0f, 24.0f, 10.0f, RING[i].color, RING[i].start, RING[i].end, false);
    }
    create_dot(32.0f, 32.0f, 16.0f, COLOR_FACE);
    create_dot(26.5f, 29.0f, 3.0f, 0xffffff);
    create_dot(37.5f, 29.0f, 3.0f, 0xffffff);
    create_arc(SMILE_CX, SMILE_CY, SMILE_R, SMILE_W, SMILE_COLOR, 44, 136, true);

    create_text("Speaker Buddy", &lv_font_montserrat_48, 0xffffff, TITLE_Y);
    create_text(version != NULL ? version : "", &lv_font_montserrat_28, COLOR_VERSION, VERSION_Y);
}

void startup_set_visible(bool visible)
{
    if (s_root != NULL) {
        lv_obj_set_hidden(s_root, !visible);
    }
}
