@echo off
REM run-tests.bat
REM Runs the unit test suite via ctest.

setlocal
set "SRC_DIR=%~dp0.."
pushd "%SRC_DIR%"

if not exist build\CMakeCache.txt (
    echo Build dir not configured. Run scripts\configure-msvc.bat first.
    popd
    exit /b 1
)

cd build
ctest --output-on-failure -C Debug

if errorlevel 1 (
    echo.
    echo Tests FAILED.
    popd
    exit /b 1
)

echo.
echo All tests passed.

popd
endlocal
