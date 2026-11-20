# Builds av1rt_oracle against a prebuilt libaom (see tools/oracle/build.ps1).
cmake_minimum_required(VERSION 3.20)
project(av1rt_oracle C)

set(AOM_ROOT "" CACHE PATH "libaom source directory")
set(AOM_BUILD_DIR "" CACHE PATH "libaom build directory (contains config headers and <Config>/aom.lib)")
set(EXTRA_LINK_LIBS "" CACHE STRING "Extra libraries, e.g. the clang profile runtime for coverage builds")

add_executable(av1rt_oracle av1rt_oracle.c)
target_include_directories(av1rt_oracle PRIVATE "${AOM_ROOT}" "${AOM_BUILD_DIR}")
target_link_libraries(av1rt_oracle PRIVATE "${AOM_BUILD_DIR}/$<CONFIG>/aom.lib" ${EXTRA_LINK_LIBS})
target_compile_definitions(av1rt_oracle PRIVATE _CRT_SECURE_NO_WARNINGS)
