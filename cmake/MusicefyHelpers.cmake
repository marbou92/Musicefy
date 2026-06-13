# MusicefyHelpers.cmake
# Utility functions used across the Musicefy CMake build tree.
# Include this file from the root CMakeLists.txt after CompilerWarnings.

# ── musicefy_set_warnings(target) ───────────────────────────────────────
# Applies the warning flags assembled in CompilerWarnings.cmake to the
# given target.  Call after the target is created and linked.
function(musicefy_set_warnings target)
    if(NOT TARGET ${target})
        message(FATAL_ERROR "musicefy_set_warnings: ${target} is not a target")
    endif()
    target_compile_options(${target} PRIVATE ${MUSICEFY_WARNINGS})
endfunction()

# ── musicefy_use_qt(target) ────────────────────────────────────────────
# Applies standard Qt properties to an executable or library target:
#   • WIN32_EXECUTABLE on Windows (no console window)
#   • MACOSX_BUNDLE on macOS
#   • AUTOMOC, AUTORCC, AUTOUIC already set globally, but this
#     function is a convenient place to add per-target Qt tweaks
#     in the future (e.g. QML import paths, resource compilation).
function(musicefy_use_qt target)
    if(NOT TARGET ${target})
        message(FATAL_ERROR "musicefy_use_qt: ${target} is not a target")
    endif()

    if(WIN32)
        set_target_properties(${target} PROPERTIES WIN32_EXECUTABLE TRUE)
    endif()

    if(APPLE)
        set_target_properties(${target} PROPERTIES MACOSX_BUNDLE TRUE)
    endif()

    # Ensure Qt's automoc/autorcc/autouic are on (redundant with the
    # global settings but makes the intent explicit per-target).
    set_target_properties(${target} PROPERTIES
        AUTOMOC ON
        AUTORCC ON
        AUTOUIC ON
    )
endfunction()
