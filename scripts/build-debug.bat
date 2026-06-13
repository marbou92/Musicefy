@echo off
REM build-debug.bat
REM Builds Musicefy in Debug mode (for development + tests).

setlocal
set "SRC_DIR=%~dp0.."
pushd "%SRC_DIR%"

if not exist build\CMakeCache.txt (
    echo Build dir not configured. Run scripts\configure-msvc.bat first.
    popd
    exit /b 1
)

cd build
cmake --build . --config Debug --parallel

if errorlevel 1 (
    echo.
    echo Build FAILED.
    popd
    exit /b 1
)

echo.
echo Debug build succeeded. Output in build\bin\Debug\

popd
endlocal
