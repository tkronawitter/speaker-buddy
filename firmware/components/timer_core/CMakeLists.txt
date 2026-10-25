# Plain C without ESP-IDF headers; the same sources build for the host tests in firmware/test/host
idf_component_register(
    SRCS "clock.c" "format.c" "framer.c" "line.c" "session.c" "snapshot.c" "splash.c" "view.c"
    INCLUDE_DIRS "include"
)
