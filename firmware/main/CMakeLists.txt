idf_component_register(
    SRCS "main.c" "ui.c" "input.c" "link.c" "fonts/digits_150.c" "fonts/digits_76.c"
    INCLUDE_DIRS "."
)

# The generated fonts include "lvgl.h" only with this set, else "lvgl/lvgl.h"
set_source_files_properties("fonts/digits_150.c" "fonts/digits_76.c" PROPERTIES COMPILE_DEFINITIONS LV_LVGL_H_INCLUDE_SIMPLE)
