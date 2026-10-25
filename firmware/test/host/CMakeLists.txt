# Host tests for components/timer_core, built with the system C compiler (no ESP-IDF).
# cmake -S firmware/test/host -B build-host && cmake --build build-host && ctest --test-dir build-host
# With -DTIMER_CORE_COVERAGE=ON (GCC) the run leaves gcov data for gcovr; CI uploads it to Codecov.
cmake_minimum_required(VERSION 3.16)
project(timer_core_host_tests C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_C_EXTENSIONS OFF)

option(TIMER_CORE_COVERAGE "Instrument timer_core and the tests for gcov" OFF)
if(TIMER_CORE_COVERAGE)
    if(NOT CMAKE_C_COMPILER_ID STREQUAL "GNU")
        message(FATAL_ERROR "TIMER_CORE_COVERAGE needs GCC (gcov), not ${CMAKE_C_COMPILER_ID}")
    endif()
    add_compile_options(--coverage -O0)
    add_link_options(--coverage)
endif()

set(TIMER_CORE_DIR ${CMAKE_CURRENT_SOURCE_DIR}/../../components/timer_core)

add_library(timer_core STATIC
    ${TIMER_CORE_DIR}/clock.c
    ${TIMER_CORE_DIR}/format.c
    ${TIMER_CORE_DIR}/framer.c
    ${TIMER_CORE_DIR}/line.c
    ${TIMER_CORE_DIR}/session.c
    ${TIMER_CORE_DIR}/snapshot.c
    ${TIMER_CORE_DIR}/splash.c
    ${TIMER_CORE_DIR}/view.c
)
target_include_directories(timer_core PUBLIC ${TIMER_CORE_DIR}/include)

if(CMAKE_C_COMPILER_ID MATCHES "GNU|Clang")
    set(WARNINGS -Wall -Wextra -Wpedantic -Wconversion -Wshadow -Werror)
    target_compile_options(timer_core PRIVATE ${WARNINGS})
endif()

enable_testing()
foreach(name clock format framer line session snapshot splash view)
    add_executable(test_${name} test_${name}.c)
    target_link_libraries(test_${name} PRIVATE timer_core)
    if(CMAKE_C_COMPILER_ID MATCHES "GNU|Clang")
        target_compile_options(test_${name} PRIVATE -Wall -Wextra -Werror)
    endif()
    add_test(NAME ${name} COMMAND test_${name})
endforeach()
