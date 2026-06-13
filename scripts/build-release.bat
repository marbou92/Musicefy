@echo off
REM build-release.bat
REM Builds Musicefy in Release mode.

setlocal
set "SRC_DIR=%~dp0.."
pushd "%SRC_DIR%"

if not exist build\CMakeCache.txt (
    echo Build dir not configured. Run scripts\configure-msvc.bat first.
    popd
    exit /b 1
)

cd build
cmake --build . --config Release --parallel

if errorlevel 1 (
    echo.
    echo Build FAILED.
    popd
    exit /b 1
)

echo.
echo Build succeeded. Output in build\bin\Release\

popd
endlocal
