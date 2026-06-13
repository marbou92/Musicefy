@echo off
REM configure-msvc.bat
REM Configures Musicefy for Visual Studio 2019 16.11 (x64) using Qt 5.15.2.
REM We use the v142 toolset (VS 2019) because:
REM   1) Qt 5.15.2 msvc2019_64 binaries are built with the v142 toolset
REM      (binary-compatible with VS 2019; also loads under v143 on Win 10+).
REM   2) VS 2019 is the most reliable toolset for a Windows 7+ target.
REM      VS 2022's v143 does not officially support Win 7.
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

cmake -G "Visual Studio 16 2019" -A x64 ^
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
echo       - VS 2019 v142 toolset selected
echo       - _WIN32_WINNT=0x0601 enforced project-wide
echo       - UCRT (ucrtbase.dll) must be present on the target machine
echo         (Win 7 SP1: install KB2999226, or use scripts\bundle-ucrt.bat
echo          to ship the UCRT DLLs next to musicefy.exe for portable use).

popd
endlocal
