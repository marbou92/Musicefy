# QtHelpers.cmake
# Convenience helpers for Qt-based targets in Musicefy.

# Apply standard Musicefy warning settings to a target.
function(musicefy_set_warnings target)
    if(MSVC)
        target_compile_options(${target} PRIVATE ${MUSICEFY_WARNINGS})
    elseif(CMAKE_CXX_COMPILER_ID MATCHES "Clang|GNU")
        target_compile_options(${target} PRIVATE ${MUSICEFY_WARNINGS})
    endif()

    if(MUSICEFY_ENABLE_ASAN AND CMAKE_BUILD_TYPE STREQUAL "Debug")
        if(MSVC)
            target_compile_options(${target} PRIVATE /fsanitize=address)
            target_link_options(${target} PRIVATE /fsanitize=address)
        else()
            target_compile_options(${target} PRIVATE -fsanitize=address -fno-omit-frame-pointer)
            target_link_options(${target} PRIVATE -fsanitize=address)
        endif()
    endif()

    if(MUSICEFY_ENABLE_UBSAN AND CMAKE_BUILD_TYPE STREQUAL "Debug")
        if(MSVC)
            target_compile_options(${target} PRIVATE /fsanitize=undefined)
            target_link_options(${target} PRIVATE /fsanitize=undefined)
        else()
            target_compile_options(${target} PRIVATE -fsanitize=undefined)
            target_link_options(${target} PRIVATE -fsanitize=undefined)
        endif()
    endif()
endfunction()

# Link a target against a list of Qt modules.
function(musicefy_use_qt target)
    set(options "")
    set(oneValueArgs "")
    set(multiValueArgs COMPONENTS)
    cmake_parse_arguments(MUH "${options}" "${oneValueArgs}" "${multiValueArgs}" ${ARGN})

    foreach(component IN LISTS MUH_COMPONENTS)
        target_link_libraries(${target} PRIVATE Qt5::${component})
    endforeach()
endfunction()

# Mark a target as a Windows GUI app (no console window).
function(musicefy_set_gui_executable target)
    if(WIN32)
        set_target_properties(${target} PROPERTIES WIN32_EXECUTABLE TRUE)
    endif()
endfunction()
