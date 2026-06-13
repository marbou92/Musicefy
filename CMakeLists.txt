cmake_minimum_required(VERSION 3.16)

project(Musicefy
    VERSION 2.0.0
    DESCRIPTION "A modern music player for Windows 7+"
    LANGUAGES CXX
)

# ── C++ settings ──────────────────────────────────────────────────────────
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# ── Windows 7+ baseline ──────────────────────────────────────────────────
# Force the SDK + compiler to expose only APIs available on Win 7 SP1.
# Qt 5.15.2 msvc2019_64 is the last Qt 5 release that still supports
# Win 7, so this is the strictest setting we can use safely.
if(WIN32)
    add_compile_definitions(
        _WIN32_WINNT=0x0601
        WINVER=0x0601
        NTDDI_VERSION=0x06010000
        _CRT_SECURE_NO_WARNINGS
        WIN32_LEAN_AND_MEAN
        NOMINMAX
    )
endif()

set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)
set(CMAKE_INCLUDE_CURRENT_DIR ON)

# Output to a single bin/lib dir for easy windeployqt
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")

# ── Qt ────────────────────────────────────────────────────────────────────
find_package(Qt5 5.15.2 REQUIRED COMPONENTS
    Core
    Gui
    Widgets
    Multimedia
    Network
    Sql
    Qml
    Quick
    QuickControls2
    Svg
    Test
)

# ── Warnings + helpers ──────────────────────────────────────────────────
include(${CMAKE_SOURCE_DIR}/cmake/CompilerWarnings.cmake)
include(${CMAKE_SOURCE_DIR}/cmake/MusicefyHelpers.cmake)

# ── Options ───────────────────────────────────────────────────────────────
option(MUSICEFY_BUILD_TESTS "Build unit tests" ON)
option(MUSICEFY_BUILD_APP "Build the GUI application" ON)
option(MUSICEFY_ENABLE_ASAN "Enable AddressSanitizer (debug only)" OFF)
option(MUSICEFY_ENABLE_UBSAN "Enable UndefinedBehaviorSanitizer (debug only)" OFF)
option(MUSICEFY_ENABLE_WINRT_SRTC
       "Enable real Windows.SystemMediaTransportControls via C++/WinRT (Win 10+ only)"
       OFF)

# ── Windows 7+ baseline with optional Win 10+ SMTC ───────────────────────
# By default we build only with the Win 7-compatible Win32 surface. Setting
# MUSICEFY_ENABLE_WINRT_SRTC=ON switches the SMTC integration from a stub
# to the real Windows.Media.SystemMediaTransportControls WinRT class. The
# NuGet package + runtimeobject.lib are pulled in via FetchContent below.
# Win 7 can't host the WinRT class so the flag is effectively a no-op there.
if(MUSICEFY_ENABLE_WINRT_SRTC)
    if(WIN32)
        # C++/WinRT (NuGet). MSBuild's "Microsoft.Windows.CppWinRT" is the
        # canonical package — the headers land in ${nuget} so we can add
        # them to the include path below. We use FetchContent's ExternalProject
        # mechanism to drive nuget.exe restore so the dev doesn't need a
        # package manager.
        include(FetchContent)
        set(MUSICEFY_CPPWINRT_VERSION "2.0.240111.5")
        FetchContent_Declare(
            cppwinrt_nuget
            URL "https://www.nuget.org/api/v2/package/Microsoft.Windows.CppWinRT/${MUSICEFY_CPPWINRT_VERSION}"
        )
        FetchContent_MakeAvailable(cppwinrt_nuget)
        # Add the WinRT include path to the core target via compile
        # definitions + a private include dir. We do this in core's
        # CMakeLists.txt (so SmtcController.cpp can #include <winrt/...>).
        set(MUSICEFY_CPPWINRT_INCLUDE_DIR
            "${cppwinrt_nuget_SOURCE_DIR}/include"
            CACHE INTERNAL "C++/WinRT include path")
        message(STATUS "  C++/WinRT: enabled (${MUSICEFY_CPPWINRT_VERSION})")
    else()
        message(WARNING "MUSICEFY_ENABLE_WINRT_SRTC is Win-only; ignoring on this platform")
    endif()
endif()

# ── Sub-projects ──────────────────────────────────────────────────────────
add_subdirectory(src/core)
if(MUSICEFY_BUILD_APP)
    if(EXISTS "${CMAKE_SOURCE_DIR}/src/app/CMakeLists.txt")
        add_subdirectory(src/app)
    endif()
endif()

if(MUSICEFY_BUILD_TESTS)
    enable_testing()
    add_subdirectory(tests)
endif()

# ── Summary ───────────────────────────────────────────────────────────────
message(STATUS "")
message(STATUS "═══════════════════════════════════════════════════")
message(STATUS "  Musicefy ${PROJECT_VERSION}")
message(STATUS "═══════════════════════════════════════════════════")
message(STATUS "  C++ standard:    ${CMAKE_CXX_STANDARD}")
message(STATUS "  Qt:              ${Qt5_VERSION}")
message(STATUS "  Compiler:        ${CMAKE_CXX_COMPILER_ID} ${CMAKE_CXX_COMPILER_VERSION}")
message(STATUS "  Build type:      ${CMAKE_BUILD_TYPE}")
message(STATUS "  Tests:           ${MUSICEFY_BUILD_TESTS}")
message(STATUS "  App:             ${MUSICEFY_BUILD_APP}")
message(STATUS "  ASan:            ${MUSICEFY_ENABLE_ASAN}")
message(STATUS "  UBSan:           ${MUSICEFY_ENABLE_UBSAN}")
message(STATUS "═══════════════════════════════════════════════════")
message(STATUS "")
