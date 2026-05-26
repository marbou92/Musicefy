# Building Musicefy as a Portable EXE

This guide explains how to build Musicefy into a standalone portable `.exe` file that can run on Windows 7 without requiring .NET Framework pre-installation.

## Prerequisites

- Visual Studio 2019 or later
- .NET Framework 4.5+ SDK
- ILMerge (for combining assemblies) - Optional but recommended
- Windows 7 SP1 or later

## Method 1: Quick Start with Batch Script (Easiest)

Simply run:

```cmd
build-portable.bat Release x86
```

This automatically:
- ✅ Auto-detects Visual Studio
- ✅ Builds in Release x86 mode
- ✅ Copies all dependencies
- ✅ Creates `Musicefy-Portable` folder with all files
- ✅ Generates README.txt

---

## Method 2: Manual Build Steps

### Step 1: Build Release Version

1. Open `Musicefy.sln` in Visual Studio
2. Set Configuration to **Release**
3. Set Platform to **x86** (for Windows 7 compatibility)
4. Build → Build Solution

### Step 2: Create Portable Folder

Create a new folder: `Musicefy-Portable`

### Step 3: Copy Files

From `Musicefy/bin/Release/`, copy these files to `Musicefy-Portable/`:

```
Musicefy.exe
Musicefy.Core.dll
NAudio.dll
TagLibSharp.dll
```

### Step 4: Create README

Create `Musicefy-Portable/README.txt`:

```
Musicefy Portable
=================

This is a standalone version of Musicefy that requires no installation.

To run:
1. Extract all files to a folder
2. Double-click Musicefy.exe
3. Enjoy streaming music!

Requirements:
- Windows 7 SP1 or later
- Internet connection for streaming
- No additional software required

Supported Streaming Services:
- Squidify (Subsonic API)
- Monochrome (Hi-Fi / TIDAL)
- Local Music Files
```

### Step 5: ZIP for Distribution

Compress `Musicefy-Portable` folder as `Musicefy-Portable.zip`

---

## Method 3: Advanced - ILMerge (Single EXE)

Create a single combined executable with no external DLLs:

### Step 1: Install ILMerge

```bash
nuget install ILMerge -OutputDirectory packages
```

### Step 2: Merge Assemblies

```bash
packages\ILMerge.3.x.x\tools\ILMerge.exe /out:Musicefy-Portable.exe ^
  Musicefy\bin\Release\Musicefy.exe ^
  Musicefy\bin\Release\Musicefy.Core.dll ^
  Musicefy\bin\Release\NAudio.dll ^
  Musicefy\bin\Release\TagLibSharp.dll
```

Result: Single `Musicefy-Portable.exe` file (~8-12 MB)

---

## Distribution Methods

### Option 1: GitHub Releases (Recommended)

```bash
# Tag your release
git tag v1.0.0
git push origin v1.0.0

# Create GitHub Release and upload Musicefy-Portable.zip
```

### Option 2: Direct Website Download

Host the ZIP on your server.

### Option 3: Portable USB

Copy `Musicefy-Portable` folder to USB stick - runs on any Windows 7+ computer!

---

## Troubleshooting

### "Visual Studio not found"
- Install Visual Studio 2019 or later
- Install Visual Studio Build Tools if you don't need the full IDE

### "Assembly not found" error
- Ensure ALL 4 DLLs are copied to the portable folder
- Check file names exactly match
- Use ILMerge to combine into single EXE

### "NAudio initialization error" on Windows 7
- Install Visual C++ Redistributable: https://support.microsoft.com/en-us/help/2977003
- Windows 7 may need DirectX 9.0c compatibility

### "TIDAL API connection error"
- Ensure internet connection is working
- Check if Monochrome instances are up: https://tidal-uptime.geeked.wtf

---

## Portable Package Contents

After building, your `Musicefy-Portable` folder contains:

| File | Size | Purpose |
|------|------|---------|
| Musicefy.exe | ~400 KB | Main application |
| Musicefy.Core.dll | ~50 KB | Core library |
| NAudio.dll | ~2-3 MB | Audio playback |
| TagLibSharp.dll | ~100 KB | Metadata reading |
| README.txt | ~1 KB | Instructions |

**Total: ~5-10 MB**

---

## System Requirements for End Users

- **OS**: Windows 7 SP1 or later (x86/x64)
- **RAM**: 256 MB minimum
- **Storage**: 50 MB available
- **Internet**: Required for streaming
- **Visual C++ Redistributable**: Recommended (free)

---

## Version Control

After building, add to `.gitignore`:

```
Musicefy-Portable/
Musicefy-Portable.zip
*.obj
*.exe
*.dll (for release builds)
```

---

## Automated GitHub Actions Build

Create `.github/workflows/build-portable.yml`:

```yaml
name: Build Portable EXE

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - uses: microsoft/setup-msbuild@v1
      - run: msbuild Musicefy.sln /p:Configuration=Release /p:Platform=x86
      - run: |
          mkdir Musicefy-Portable
          copy "Musicefy\bin\Release\*.exe" Musicefy-Portable\
          copy "Musicefy\bin\Release\*.dll" Musicefy-Portable\
      - uses: actions/upload-artifact@v3
        with:
          name: Musicefy-Portable
          path: Musicefy-Portable/
```

This automatically builds on every tag push!

---

## Success!

Your Musicefy portable EXE is now ready for distribution! 🎵

Users can:
1. Download the ZIP
2. Extract anywhere
3. Run Musicefy.exe
4. Start streaming immediately!

No installation. No dependencies. Pure portable streaming! ✨
