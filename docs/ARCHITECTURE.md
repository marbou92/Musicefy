# ARCHITECTURE.md

High-level shape of the Musicefy v2 codebase.

## Goals

- **Win 7 SP1+** at runtime, Win 10 SDK at compile time.
- **Material 3** look and feel.
- **Single C++17 codebase** with all platform-specific code
  concentrated in `src/platform/windows/`.
- **Offline-first**: local library, optional remote sources
  (Subsonic, YouTube), optional extensions.

## Layered structure

```
┌────────────────────────────────────────────────────────────┐
│ src/app/                                                   │
│   AppContainer, AppLifecycle, MainWindow, ViewModels,      │
│   Widgets (HomeView, SearchView, LibraryView, SettingsPage,│
│   ArtistView, AlbumView, PlaylistView, NowPlayingView,     │
│   Sidebar, ToastOverlay, NowPlayingBar)                   │
│   ─ Depends on:  core, theme, Qt5::Widgets                 │
├────────────────────────────────────────────────────────────┤
│ src/core/                                                  │
│   models/, theme/, di/, database/, playback/, sources/,    │
│   services/, interfaces/                                   │
│   ─ Depends on:  Qt5::Core, Qt5::Gui, Qt5::Sql,           │
│                  Qt5::Multimedia, Qt5::Network             │
│   ─ Pure C++ + Qt; no GUI dependency.                     │
├────────────────────────────────────────────────────────────┤
│ third_party/material_color_utilities/                      │
│   (vendored: HCT color space → Material You tonal palettes│
└────────────────────────────────────────────────────────────┘
```

## Composition root

`src/app/AppContainer.cpp` builds a `mf::core::di::ServiceCollection`
that owns the singletons. Every service is registered in one place;
the rest of the app resolves through `AppContainer::xxx()` accessors
or the raw `services()` collection (used by tests).

## Major subsystems

### Library + database

- `LibraryRepository` (in `core/database/`) wraps a single
  `QSqlDatabase` connection. All read/write access goes through
  this class.
- `LibraryScanner` walks the filesystem, calls `TagReader` to read
  metadata, and upserts into the repo.
- `LibraryService` (in `core/services/`) composes the two and
  exposes a high-level "folders" API with progress signals.
- The schema lives in `src/core/database/migrations/` and is
  applied at startup by the `Database` class.

### Playback

- `PlaybackService` is a thin wrapper around `QMediaPlayer`.
- `QueueManager` owns the queue + current-index state.
- `PlayerViewModel` exposes them as Q_PROPERTYs for the UI.
- `MediaKeyFilter` intercepts `WM_APPCOMMAND` messages for the
  hardware media keys.
- `SmtcController` is the Win 8+ System Media Transport Controls
  integration. Stub on Win 7 + this build, ready to be enabled
  by defining `MUSICEFY_ENABLE_WINRT_SRTC` and adding the
  C++/WinRT NuGet package.

### Sources

- `HttpClient` is a `QNetworkAccessManager` wrapper with cookie
  persistence, redirect following, and JSON/text convenience.
- `StreamingSourceManager` is a registry of `IMusicSourceProvider`
  instances and their `IMusicSourceSession`s.
- `LocalFolderProvider`, `SubsonicProvider`, `YouTubeProvider`
  are the built-in providers. YouTube ships as a structurally
  complete stub — the InnerTube cipher is the next big chunk to
  port.

### Theming

- `ThemeManager` is a singleton that holds the current
  `AppTheme` (one of 19 named themes) + `ThemeMode`
  (System/Light/Dark/Amoled) + optional dynamic seed color.
- On any change, it builds a `MusicefyColorScheme` (the
  `Q_GADGET` Material-3 palette) and emits `schemeChanged`.
- Widgets re-style themselves in response to `schemeChanged` by
  re-applying their QSS templates.
- HCT extraction and palette generation go through
  `HctFacade` (vendored Material Color Utilities).

### Image pipeline

- `ImageCache` caches downloaded cover art on disk and in a small
  LRU. Keyed by SHA-1 of URL.
- `ColorExtractor` runs k-means++ on a downscaled QImage to find
  the dominant clusters, then picks the most-chromatic one and
  re-renders it at HCT chroma=40 tone=50 to produce a Material-3
  seed color.
- `ArtworkEnrichment` composes the two, adding a per-URL seed
  cache to avoid re-extracting the same cover.
- `PathToImage` is the synchronous fast path: local file → QImage
  directly, URL → ImageCache lookup (no network).

### Settings

- `SettingsControl` is a thin wrapper over `QSettings` with
  templated `get<T>` / `set<T>` accessors.
- `SettingsPage` is a `QStackedWidget` of seven sub-panels
  (Library, Appearance, Sources, Downloads, Discover, Extensions,
  Repositories), one for each logical group.

## Conventions

- **No raw pointers in service APIs** — `std::shared_ptr` for
  DI-resolved services, value types for models.
- **`Pimpl` for QObject subclasses that need private state** —
  `SmtcController`, `MediaKeyFilter`, etc.
- **Async via callbacks** (`std::function`) for one-shot operations
  (download, fetch, enrich). Sync variants provided for the
  fast paths.
- **Q_PROPERTY + signal** for state that needs to drive QML or
  QDataWidgetMapper. Plain signals for everything else.
- **File-scoped namespaces** in `core/services/` so each header is
  a self-contained unit. The `using` aliases are at the top of
  every `*.cpp`.
- **No `git init` of the Qt port** — left to the user.

## Testing

- `tests/` uses Qt Test + `QTEST_GUILESS_MAIN` for everything.
- `QT_QPA_PLATFORM=offscreen` is set in the test properties.
- Per-test isolation via `QTemporaryDir` + a unique DB tag.
- Tests are registered in `tests/CMakeLists.txt` and run via
  `ctest`.

## Build

See `BUILD.md`. CI is in `.github/workflows/build.yml`.
