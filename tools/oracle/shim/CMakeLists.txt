# One shared library per shim_<pkg>.c: aomshim_<pkg>.dll exporting wrappers around libaom's
# internal C functions, so C# ports can be tested against them (see docs/TESTING.md).
cmake_minimum_required(VERSION 3.20)
project(aomshim C)

set(AOM_ROOT "" CACHE PATH "libaom source directory")
set(AOM_BUILD_DIR "" CACHE PATH "pure-C libaom build directory")
set(SHIM "" CACHE STRING "shim name: builds shim_<SHIM>.c into aomshim_<SHIM>.dll")

add_library(aomshim_${SHIM} SHARED "${CMAKE_CURRENT_SOURCE_DIR}/shim_${SHIM}.c")
target_include_directories(aomshim_${SHIM} PRIVATE "${AOM_ROOT}" "${AOM_BUILD_DIR}")
target_link_libraries(aomshim_${SHIM} PRIVATE "${AOM_BUILD_DIR}/$<CONFIG>/aom.lib")
target_compile_definitions(aomshim_${SHIM} PRIVATE _CRT_SECURE_NO_WARNINGS)
