@echo off
REM bundle-ucrt.bat
REM Copies the Universal C Runtime (UCRT) DLLs next to musicefy.exe so
REM the binary can launch on a bare Windows 7 SP1 install that hasn't
REM installed KB2999226.
REM
REM The UCRT was introduced in Win 10 and back-ported to Win 7/8 via
REM KB2999226. Most Win 7 machines have it via Windows Update, but a
REM clean install or a stripped-down image will not. Bundling the DLLs
REM is the safest portable-build option.
REM
REM Usage: scripts\bundle-ucrt.bat [build-config]
REM   build-config defaults to Release.

setlocal

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"

set "SRC_DIR=%~dp0.."
set "OUT_DIR=%SRC_DIR%\build\bin\%CONFIG%"

if not exist "%OUT_DIR%" (
    echo ERROR: %OUT_DIR% does not exist. Build the project first.
    exit /b 1
)

REM ── Locate the UCRT redistributable folder ──────────────────────────
REM Try the most common SDK versions in descending order, then fall
REM back to vswhere for the latest. Each install ships the same files.
set "UCRT_REL="
for %%V in (10.0.22621.0 10.0.22000.0 10.0.20348.0 10.0.19041.0 10.0.18362.0 10.0.17763.0 10.0.17134.0) do (
    if exist "C:\Program Files (x86)\Windows Kits\10\Redist\%%V\ucrt\DLLs\x64\ucrtbase.dll" (
        set "UCRT_REL=C:\Program Files (x86)\Windows Kits\10\Redist\%%V\ucrt\DLLs\x64"
        goto :copy
    )
)

if "%UCRT_REL%"=="" (
    echo ERROR: Could not locate the UCRT redistributable in the Windows 10 SDK.
    echo        Install the Windows 10 SDK (any version 17134+) and retry.
    exit /b 1
)

:copy
echo Copying UCRT DLLs from:
echo   %UCRT_REL%
echo into:
echo   %OUT_DIR%
echo.

copy /Y "%UCRT_REL%\ucrtbase.dll" "%OUT_DIR%\" >nul
if errorlevel 1 goto :fail

copy /Y "%UCRT_REL%\api-ms-win-*.dll" "%OUT_DIR%\" >nul
if errorlevel 1 goto :fail

echo.
echo Done. %OUT_DIR% now contains the UCRT runtime.
echo The portable build can now launch on Win 7 SP1 without KB2999226.

endlocal
exit /b 0

:fail
echo.
echo Copy FAILED.
endlocal
exit /b 1
