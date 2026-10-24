idf_component_register(
    SRCS "main.c" "ui.c" "input.c" "link.c" "fonts/digits_150.c"
    INCLUDE_DIRS "."
)

# The generated font includes "lvgl.h" only with this set, else "lvgl/lvgl.h"
set_source_files_properties("fonts/digits_150.c" PROPERTIES COMPILE_DEFINITIONS LV_LVGL_H_INCLUDE_SIMPLE)
