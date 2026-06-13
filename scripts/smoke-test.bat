@echo off
REM smoke-test.bat
REM Lightweight automated smoke check for a portable Musicefy build.
REM Runs in two modes:
REM   1. Unit tests (ctest) — verifies the build is healthy.
REM   2. App launch (--no-window --exit-after=3) — verifies the
REM      executable starts, the DI graph builds, and the process
REM      exits cleanly within a few seconds. Catches the "I
REM      shipped an exe that crashes on first launch" class of
REM      bug.
REM
REM Usage:
REM   scripts\smoke-test.bat [build-dir]
REM     build-dir defaults to build\bin\Release (the output of
REM     scripts\build-portable.bat).
REM
REM Exits 0 on success, non-zero on any failure.

setlocal EnableDelayedExpansion

set "SRC_DIR=%~dp0.."
pushd "%SRC_DIR%"

set "BUILD_DIR=%~1"
if "%BUILD_DIR%"=="" set "BUILD_DIR=build\bin\Release"
set "EXE=%BUILD_DIR%\musicefy.exe"

if not exist "%EXE%" (
    echo ERROR: %EXE% not found.
    echo Run scripts\build-portable.bat first, or pass the build dir
    echo as the first argument.
    popd
    exit /b 1
)

echo ════════════════════════════════════════════════════════
echo   Musicefy smoke test
echo   Build: %EXE%
echo ════════════════════════════════════════════════════════
echo.

set "FAILED=0"

REM ── 1. Unit tests ─────────────────────────────────────────────────────
echo [1/3] Running unit tests...
if not exist build\CMakeCache.txt (
    echo   Skipped (no build dir at ^"build\^").
    echo   To enable this step run scripts\configure-msvc.bat ^&^& cmake --build build.
) else (
    pushd build
    ctest --output-on-failure -C Release -E "smoke"
    if errorlevel 1 (
        echo.
        echo   Unit tests FAILED.
        set "FAILED=1"
    ) else (
        echo.
        echo   Unit tests passed.
    )
    popd
)
echo.

REM ── 2. Headless launch ───────────────────────────────────────────────
echo [2/3] Headless launch test (--no-window --exit-after=3)...
pushd "%BUILD_DIR%"
start /WAIT /B "" "%EXE%" --no-window --exit-after=3
set "LAUNCH_EXIT=!errorlevel!"
popd
if not "!LAUNCH_EXIT!"=="0" (
    echo   FAILED: exit code !LAUNCH_EXIT!
    set "FAILED=1"
) else (
    echo   OK: launched and exited cleanly.
)
echo.

REM ── 3. Quick file inventory ───────────────────────────────────────────
echo [3/3] Verifying required runtime files are present...
set "MISSING="
for %%F in (Qt5Core.dll Qt5Gui.dll Qt5Widgets.dll Qt5Multimedia.dll
           Qt5Network.dll Qt5Sql.dll Qt5Svg.dll
           platforms\qwindows.dll
           sqldrivers\qsqlite.dll) do (
    if not exist "%BUILD_DIR%\%%F" set "MISSING=!MISSING! %%F"
)
if defined MISSING (
    echo   FAILED: missing files:!MISSING!
    echo   Did scripts\build-portable.bat run windeployqt?
    set "FAILED=1"
) else (
    echo   OK: all required runtime files present.
)
echo.

REM ── Result ────────────────────────────────────────────────────────────
if "%FAILED%"=="0" (
    echo ════════════════════════════════════════════════════════
    echo   Smoke test PASSED.
    echo ════════════════════════════════════════════════════════
    popd
    endlocal
    exit /b 0
) else (
    echo ════════════════════════════════════════════════════════
    echo   Smoke test FAILED. See messages above.
    echo ════════════════════════════════════════════════════════
    popd
    endlocal
    exit /b 1
)
