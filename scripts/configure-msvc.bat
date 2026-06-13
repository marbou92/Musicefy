@echo off
REM configure-msvc.bat
REM Configures Musicefy using an available Visual Studio installation.
REM Auto-detects VS 2019 (v142) or VS 2022 (v143) — whichever is installed.
REM Qt 5.15.2 msvc2019_64 binaries are binary-compatible with both toolsets.
REM Usage: scripts\configure-msvc.bat [Debug|Release] [path\to\Qt]

setlocal

if "%~1"=="" set "BUILD_TYPE=Release"
if not "%~1"=="" set "BUILD_TYPE=%~1"

if "%~2"=="" set "QT_DIR=C:\Qt\Qt5.15.2\msvc2019_64"
if not "%~2"=="" set "QT_DIR=%~2"

if not exist "%QT_DIR%\lib\cmake\Qt5\Qt5Config.cmake" (
    echo ERROR: Qt5Config.cmake not found at "%QT_DIR%\lib\cmake\Qt5\".
    echo Please install Qt 5.15.2 MSVC 2019 64-bit and/or pass the Qt path as the second argument.
    exit /b 1
)

set "SRC_DIR=%~dp0.."
pushd "%SRC_DIR%"

if not exist build mkdir build
cd build

REM --- Auto-detect Visual Studio version ---
set "GEN="
where /R "%ProgramFiles(x86)%\Microsoft Visual Studio\2019" cmake.exe >nul 2>&1
if not errorlevel 1 (
    set "GEN=Visual Studio 16 2019"
    echo Detected VS 2019
)
if "%GEN%"=="" (
    where /R "%ProgramFiles%\Microsoft Visual Studio\2022" cmake.exe >nul 2>&1
    if not errorlevel 1 (
        set "GEN=Visual Studio 17 2022"
        echo Detected VS 2022
    )
)
if "%GEN%"=="" (
    echo ERROR: No Visual Studio installation found (2019 or 2022).
    echo Install VS 2019 Build Tools or VS 2022 Build Tools.
    popd
    exit /b 1
)

echo Using generator: %GEN%

cmake -G "%GEN%" -A x64 ^
    -DCMAKE_BUILD_TYPE=%BUILD_TYPE% ^
    -DCMAKE_PREFIX_PATH="%QT_DIR%" ^
    -S .. -B .

if errorlevel 1 (
    echo.
    echo CMake configuration FAILED.
    popd
    exit /b 1
)

echo.
echo Configuration succeeded. Build with:
echo     cmake --build . --config %BUILD_TYPE% --parallel
echo Or run: scripts\build-release.bat
echo.
echo NOTE: This build targets Windows 7 SP1 and later.
echo       - _WIN32_WINNT=0x0601 enforced project-wide
echo       - UCRT (ucrtbase.dll) must be present on the target machine
echo         (Win 7 SP1: install KB2999226, or use scripts\bundle-ucrt.bat
echo          to ship the UCRT DLLs next to musicefy.exe for portable use).

popd
endlocal
