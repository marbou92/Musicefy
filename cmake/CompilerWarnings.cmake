# CompilerWarnings.cmake
# MSVC + Clang/GCC compatible warning settings for Musicefy.

if(MSVC)
    set(MUSICEFY_WARNINGS
        /W4                 # Warning level 4
        /permissive-        # Strict standard conformance
        /Zc:__cplusplus     # Correct __cplusplus macro
        /Zc:inline          # Remove unreferenced COMDAT
        /utf-8              # Set source/execution charset to UTF-8
        /EHsc               # Standard exception handling
        /volatile:iso       # ISO-compliant volatile semantics
    )

    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        # /W4 is OK in debug, no need for /WX
    else()
        # Treat warnings as errors in release
        list(APPEND MUSICEFY_WARNINGS /WX)
    endif()

    # Disable some noisy warnings:
    # 4127: conditional expression is constant
    # 4244: conversion from 'X' to 'Y', possible loss of data
    # 4251: 'X' needs to have dll-interface to be used by clients
    # 4275: non dll-interface used as base for dll-interface
    # 4514: unreferenced inline function has been removed
    # 4710: function not inlined
    # 4711: selected for automatic inline expansion
    # 4820: padding added after data member
    # 5039: exception throws in extern "C" functions
    set(MUSICEFY_WARNINGS_DISABLE
        4127
        4244
        4251
        4275
        4514
        4710
        4711
        4820
        4996
        5039
    )

    # Each /wd must be a separate list element so target_compile_options
    # passes them as individual arguments to cl.exe.
    foreach(_warn_num IN LISTS MUSICEFY_WARNINGS_DISABLE)
        list(APPEND MUSICEFY_WARNINGS "/wd${_warn_num}")
    endforeach()
endif()

if(CMAKE_CXX_COMPILER_ID MATCHES "Clang|GNU")
    set(MUSICEFY_WARNINGS
        -Wall
        -Wextra
        -Wpedantic
        -Wshadow
        -Wnon-virtual-dtor
        -Wold-style-cast
        -Wcast-align
        -Wunused
        -Woverloaded-virtual
        -Wconversion
        -Wsign-conversion
        -Wnull-dereference
        -Wdouble-promotion
        -Wformat=2
    )

    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        # Don't fail on warnings in debug
    else()
        list(APPEND MUSICEFY_WARNINGS -Werror)
    endif()
endif()
