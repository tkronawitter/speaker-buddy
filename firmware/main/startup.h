/* Startup screen: logo D ("face in the colour ring"), the name and the firmware version on black. */
#pragma once

#include <stdbool.h>

/*
 * Creates the startup screen on LVGL's top layer, over the timer screen, and shows it. Nothing on it
 * takes touches, so a tap reaches the timer screen below. Call once, with the display lock held.
 */
void startup_create(const char *version);

/* Shows or hides the startup screen; call from LVGL context. */
void startup_set_visible(bool visible);
