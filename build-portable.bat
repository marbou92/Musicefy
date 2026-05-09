@echo off
REM Portable Build Script for Musicefy (Batch)
REM Usage: build-portable.bat [Configuration] [Platform]

setlocal enabledelayedexpansion

set Configuration=%1
if "%Configuration%"=="" set Configuration=Release

set Platform=%2
if "%Platform%"=="" set Platform=x86

echo Building Musicefy Portable EXE...
echo Configuration: %Configuration%
echo Platform: %Platform%

REM Find Visual Studio
for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath`) do set vsPath=%%i

if "%vsPath%"=="" (
    echo Error: Visual Studio not found. Please install Visual Studio 2019 or later.
    exit /b 1
)

echo Found Visual Studio at: %vsPath%

REM Build the solution
echo Building solution...
set msbuild="%vsPath%\MSBuild\Current\Bin\MSBuild.exe"

if not exist %msbuild% (
    echo Error: MSBuild not found
    exit /b 1
)

%msbuild% Musicefy.sln /p:Configuration=%Configuration% /p:Platform=%Platform%

if %ERRORLEVEL% neq 0 (
    echo Build failed!
    exit /b 1
)

echo Build completed successfully!

REM Copy output files
set OutputPath=Musicefy\bin\%Configuration%
set PortableDir=Musicefy-Portable

if not exist "%PortableDir%" mkdir "%PortableDir%"

echo Copying files to portable directory: %PortableDir%

copy "%OutputPath%\Musicefy.exe" "%PortableDir%\" /Y
copy "%OutputPath%\Musicefy.Core.dll" "%PortableDir%\" /Y
copy "%OutputPath%\NAudio.dll" "%PortableDir%\" /Y
copy "%OutputPath%\TagLibSharp.dll" "%PortableDir%\" /Y

REM Create README
(
    echo Musicefy Portable
    echo =================
    echo.
    echo This is a standalone version of Musicefy that requires no installation.
    echo.
    echo To run:
    echo 1. Extract all files to a folder
    echo 2. Double-click Musicefy.exe
    echo 3. Enjoy streaming music!
    echo.
    echo Requirements:
    echo - Windows 7 SP1 or later
    echo - Internet connection for streaming
    echo - No additional software required
    echo.
    echo Supported Streaming Services:
    echo - Squidify (Subsonic API)
    echo - Monochrome (Hi-Fi / TIDAL)
    echo - Local Music Files
) > "%PortableDir%\README.txt"

echo Portable EXE created successfully!
echo Location: %PortableDir%

echo.
echo Build Complete!
echo.
pause
