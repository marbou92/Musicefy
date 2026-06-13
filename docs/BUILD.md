# BUILD.md

How to build Musicefy on Windows.

## Prerequisites

| Tool | Version | Why |
|------|---------|-----|
| **Visual Studio 2019** Community 16.11+ | C++ workload, MSVC v142 | The last MSVC that still produces Win 7-compatible binaries. |
| **Windows 10 SDK** | 10.0.17134 or newer | Provides the UCRT runtime + `ucrtbase.dll` for Win 7. |
| **Qt** | 5.15.2 (msvc2019_64) | The last Qt 5 release with Win 7 support. Install via the official Qt online installer. |
| **CMake** | 3.21+ | Project generator. |
| **Git** | any | For submodules + the version-line in CMakeLists. |

The build also works with **MSVC v143** (VS 2022) if you only target
Windows 10+; just update the `_WIN32_WINNT=0x0601` line in the root
`CMakeLists.txt` to `0x0A00`.

## Quick start (portable build)

```cmd
git clone <repo>
cd MusicefyQt
scripts\configure-msvc.bat
scripts\build-portable.bat C:\Qt\5.15.2\msvc2019_64\bin
```

The output tree at `build\bin\Release\` is fully self-contained:
`musicefy.exe`, all required Qt DLLs, the UCRT runtime, and the SQL
migrations. It launches on a clean Windows 7 SP1 install with no
additional setup.

## Step-by-step

### 1. Configure

```cmd
scripts\configure-msvc.bat
```

This wraps `cmake -G "Visual Studio 16 2019" -A x64` into a `build\`
directory. Pass `-DCMAKE_BUILD_TYPE=Debug` etc. to the script's tail
if you want non-Release.

### 2. Build (Release)

```cmd
scripts\build-release.bat
```

Or directly:

```cmd
cmake --build build --config Release --parallel
```

The exe lands at `build\bin\Release\musicefy.exe`.

### 3. Run tests

```cmd
scripts\run-tests.bat
```

Or:

```cmd
cd build
ctest --output-on-failure -C Debug
```

Tests use `QT_QPA_PLATFORM=offscreen` so they run on a headless CI
runner. New tests go in `tests/` and are registered in
`tests\CMakeLists.txt`.

### 4. Ship a portable build

```cmd
scripts\build-portable.bat C:\Qt\5.15.2\msvc2019_64\bin
```

This wraps the build, runs `windeployqt` to copy every Qt DLL + plugin
next to the exe, then runs `scripts\bundle-ucrt.bat` to drop the
UCRT runtime. The directory is then zipped and shipped.

## CI

The GitHub Actions workflow at `.github\workflows\build.yml` runs on
every push / PR:

- **build-windows** — Release + tests + portable bundle, uploads the
  portable tree as an artifact.
- **build-debug** — Debug + AddressSanitizer + tests.

The `jurplel/install-qt-action@v3` step caches Qt between runs, so
the build typically takes 4–6 min after the first run.

## Optional: real WinRT SMTC integration (Win 10+)

By default `SmtcController` ships as a stub that logs metadata and
ignores commands. The real Windows
`SystemMediaTransportControls` WinRT class (lock-screen / volume
flyout / Cortana / global media keys) is opt-in:

```cmd
cmake -S . -B build -G "Visual Studio 16 2019" -A x64 ^
    -DMUSICEFY_ENABLE_WINRT_SRTC=ON
cmake --build build --config Release --parallel
```

The flag pulls in the
[`Microsoft.Windows.CppWinRT`](https://www.nuget.org/packages/Microsoft.Windows.CppWinRT)
NuGet package via `FetchContent` (the headers land under
`build\_deps\cppwinrt_nuget-src\include`) and links
`runtimeobject.lib`. The stub `SmtcController` source
(`src\core\playback\SmtcController.cpp`) is automatically
replaced by the real implementation.

There is **no benefit** to enabling this on a Win 7 build host
(other than compile-testing). The CI workflow does not enable
it by default — opt in locally if you want to exercise the
lock-screen path.

## Smoke test

After a portable build:

```cmd
scripts\smoke-test.bat
```

This runs `ctest`, then launches `musicefy.exe --no-window
--exit-after=3` to confirm the DI graph builds and the process
exits cleanly, then verifies all the required Qt / plugin /
UCRT DLLs are present. It's wired into the pre-release checklist
in `docs\RELEASE.md` §3.3.

For the full manual pass (14 sections, cross-referenced to
the unit tests), see `docs\SMOKE_TEST.md`.

## Targeting Windows 7 from Windows 10/11

The build works fine on Win 10/11 dev boxes; the resulting binary
just refuses to use APIs that don't exist on Win 7. The two pieces
that need attention are:

1. **`scripts\bundle-ucrt.bat`** — copies `ucrtbase.dll` + the
   `api-ms-win-*.dll` set from a Windows 10 SDK install.
2. **`_WIN32_WINNT=0x0601`** in the root `CMakeLists.txt` — pins the
   SDK to expose only Win 7 APIs at compile time.

Both are set in this repo by default.

## Common build issues

### `cl.exe not found`

Run from a **Developer Command Prompt for VS 2019**, or invoke
`vcvarsall.bat x64` first. `scripts\configure-msvc.bat` does this
automatically.

### `windeployqt not found`

Pass the Qt bin dir to `build-portable.bat`:

```cmd
scripts\build-portable.bat C:\Qt\5.15.2\msvc2019_64\bin
```

### `moc_*.cpp: No such file`

`CMAKE_AUTOMOC=ON` is set in the root `CMakeLists.txt`; if you've
disabled it, re-enable it. The `musicefy_core` target also sets
`AUTOMOC ON` explicitly.

### Tests fail with "no platform plugin"

`QT_QPA_PLATFORM=offscreen` is set by `tests\CMakeLists.txt`. If you
run a test exe directly, set the env var yourself.
