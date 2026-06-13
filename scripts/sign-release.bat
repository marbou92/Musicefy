@echo off
REM sign-release.bat
REM Authenticode-signs musicefy.exe using a code signing certificate.
REM
REM Usage:
REM   scripts\sign-release.bat                          (uses default cert file)
REM   scripts\sign-release.bat path\to\certificate.pfx  (explicit cert file)
REM   scripts\sign-release.bat /store                   (sign from Windows cert store)
REM
REM Environment variables (optional):
REM   SIGN_CERT_FILE    - Path to .pfx certificate file
REM   SIGN_CERT_PASS    - Password for the .pfx file (or leave empty for prompt)
REM   SIGN_TIMESTAMP_URL - Timestamp server URL (default: http://timestamp.digicert.com)
REM   SIGN_DESCRIPTION  - Description embedded in signature (default: "Musicefy Music Player")
REM   SIGN_URL          - URL embedded in signature (default: "https://github.com/MarBou/MusicefyQt")
REM
REM The script signs: musicefy.exe in build\bin\Release\
REM If the file doesn't exist, it tries build\bin\musicefy.exe.

setlocal EnableDelayedExpansion

set "SIGNTOOL=%ProgramFiles(x86)%\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe"
if not exist "%SIGNTOOL%" (
    set "SIGNTOOL=%ProgramFiles(x86)%\Windows Kits\10\bin\10.0.22000.0\x64\signtool.exe"
)
if not exist "%SIGNTOOL%" (
    set "SIGNTOOL=%ProgramFiles(x86)%\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"
)

if not exist "%SIGNTOOL%" (
    echo ERROR: signtool.exe not found. Install Windows SDK or set SIGNTOOL env var.
    exit /b 1
)

echo Using signtool: %SIGNTOOL%

REM ── Locate the executable ──────────────────────────────────────────────
set "EXE_PATH=%~dp0..\build\bin\Release\musicefy.exe"
if not exist "%EXE_PATH%" (
    set "EXE_PATH=%~dp0..\build\bin\musicefy.exe"
)
if not exist "%EXE_PATH%" (
    echo ERROR: musicefy.exe not found in build output directory.
    echo Run a release build first: scripts\build-release.bat
    exit /b 1
)
echo Signing: %EXE_PATH%

REM ── Timestamp server ───────────────────────────────────────────────────
if "%SIGN_TIMESTAMP_URL%"=="" set "SIGN_TIMESTAMP_URL=http://timestamp.digicert.com"

REM ── Description ────────────────────────────────────────────────────────
if "%SIGN_DESCRIPTION%"=="" set "SIGN_DESCRIPTION=Musicefy Music Player"
if "%SIGN_URL%"=="" set "SIGN_URL=https://github.com/MarBou/MusicefyQt"

REM ── Determine signing mode ─────────────────────────────────────────────
if /i "%~1"=="/store" goto :sign_store
if /i "%~1"=="/silent" set "SIGN_SILENT=1" & shift & goto :sign_file

REM ── File-based signing (.pfx) ─────────────────────────────────────────
set "CERT_FILE=%SIGN_CERT_FILE%"
if not "%~1"=="" set "CERT_FILE=%~1"

if "%CERT_FILE%"=="" (
    echo.
    echo No certificate specified.
    echo.
    echo Usage:
    echo   scripts\sign-release.bat path\to\certificate.pfx
    echo   scripts\sign-release.bat /store
    echo.
    echo Or set environment variables:
    echo   SIGN_CERT_FILE=path\to\certificate.pfx
    echo   SIGN_CERT_PASS=password
    echo.
    echo To sign from the Windows certificate store (e.g., after importing
    echo a certificate), use /store mode.
    exit /b 1
)

if not exist "%CERT_FILE%" (
    echo ERROR: Certificate file not found: %CERT_FILE%
    exit /b 1
)

echo Certificate: %CERT_FILE%
echo Timestamp:   %SIGN_TIMESTAMP_URL%
echo.

REM Build the sign command
set "SIGN_CMD="%SIGNTOOL%" sign /fd SHA256 /tr "%SIGN_TIMESTAMP_URL%" /td SHA256"
set "SIGN_CMD=!SIGN_CMD! /d "%SIGN_DESCRIPTION%" /du "%SIGN_URL%""
set "SIGN_CMD=!SIGN_CMD! /f "%CERT_FILE%""

if not "%SIGN_CERT_PASS%"=="" (
    set "SIGN_CMD=!SIGN_CMD! /p "%SIGN_CERT_PASS%""
)

set "SIGN_CMD=!SIGN_CMD! "%EXE_PATH%""

echo Executing: !SIGN_CMD!
!SIGN_CMD!
if errorlevel 1 (
    echo.
    echo ERROR: Signing failed. Check your certificate and password.
    exit /b 1
)
goto :verify

:sign_store
REM ── Store-based signing (Windows certificate store) ────────────────────
echo Signing from Windows certificate store...
echo.

set "SIGN_CMD="%SIGNTOOL%" sign /fd SHA256 /tr "%SIGN_TIMESTAMP_URL%" /td SHA256"
set "SIGN_CMD=!SIGN_CMD! /d "%SIGN_DESCRIPTION%" /du "%SIGN_URL%""
set "SIGN_CMD=!SIGN_CMD! /a /s My /n "Musicefy""
set "SIGN_CMD=!SIGN_CMD! "%EXE_PATH%""

echo Executing: !SIGN_CMD!
!SIGN_CMD!
if errorlevel 1 (
    echo.
    echo ERROR: Signing failed. Make sure a valid certificate is installed
    echo in the current user's Personal certificate store with the name "Musicefy".
    exit /b 1
)

:verify
echo.
echo Verifying signature...
"%SIGNTOOL%" verify /pa /v "%EXE_PATH%"
if errorlevel 1 (
    echo WARNING: Signature verification failed. The file may still be signed
    echo but verification requires the signing certificate to be trusted.
)

echo.
echo ══════════════════════════════════════════════════════════
echo   Signing complete: %EXE_PATH%
echo ══════════════════════════════════════════════════════════

popd
endlocal
exit /b 0
