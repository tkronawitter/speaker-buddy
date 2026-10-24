/* Landscape timer screen: big elapsed time, deviation line, disconnected symbol (D-01, D-02, D-04). */
#pragma once

#include "timer_core/view.h"

/* Creates the widgets on the active screen. Call once, with the display lock held. */
void ui_create(void);

/* Draws a view; call from LVGL context (timer callback). Unchanged views redraw nothing. */
void ui_render(const tc_view_t *view);
