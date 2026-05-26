# Musicefy

A WPF music player for Windows with local library management, Subsonic API support, and YouTube Music streaming.

## Features

- **Library Management** — Scans local files (MP3, FLAC, WAV, OGG, M4A, AAC, WMA, APE, MPC, WV, AIFF, DSF), extracts ID3 tags and embedded artwork, and indexes everything in SQLite for instant search
- **Smart Caching** — Tracks file modification timestamps to skip redundant reads; keeps up to 10,000 metadata entries in memory with LRU eviction
- **Playback** — NAudio/WASAPI output with resampling, shuffle/repeat queue, seek, volume control
- **Streaming Sources** — YouTube Music (Google API key or anonymous InnerTube fallback), Subsonic-compatible servers (Navidrome, Airsonic, etc.)
- **Extensions** — Load third-party `IMusicSourceProvider` DLLs at runtime with SHA256 hash verification and remote repo support
- **Themes** — Light, Dark, Dark Pure (OLED) modes with Catppuccin, GreenApple, and Lavender accent palettes; auto-switches on Windows light/dark mode
- **MVVM Architecture** — Dependency injection via `Microsoft.Extensions.DependencyInjection`, clean ViewModel/View separation
- **Favourites & History** — Toggle favourites, play-count tracking, recently played view
- **Download Manager** — Streaming source downloads with resume support, 500 MB per-file cap, 10 GB cache limit

## Quick Start

```
git clone https://github.com/marbou92/Musicefy.git
cd Musicefy
dotnet build --configuration Release
.\Musicefy\bin\Release\net472\Musicefy.exe
```

Requires .NET Framework 4.7.2+ SDK and Visual Studio 2019+ (or `dotnet build`).

## Project Structure

```
Musicefy.sln
├── Musicefy/               # WPF application
│   ├── App.xaml.cs         # DI composition root
│   ├── ViewModels/         # MVVM view models
│   ├── Views/              # XAML views (MainWindow, Home, Library, Search, Settings)
│   ├── Services/           # PlaybackService, ThemeManager, NavigationService, ToastService
│   └── Themes/             # XAML resource dictionaries (modes, palettes)
├── Musicefy.Core/          # Business logic & interfaces
│   ├── Interfaces/         # IAudioPlayer, ILibraryService, IQueueManager, IMusicSourceProvider, etc.
│   ├── Models/             # MusicFile, StreamingSource, ExtensionManifest
│   ├── Services/           # QueueManager, LibraryScanner, SubsonicClient, YouTubeSourceProvider
│   └── Configuration/      # DatabaseConfig (Dapper type handlers for TimeSpan/DateTime)
└── Musicefy.Tests/         # MSTest unit tests
```

## Key Dependencies

| Package | Purpose |
|---|---|
| NAudio 2.2.1 | WASAPI audio playback, resampling |
| TagLibSharp 2.2.0 | ID3 tag & artwork extraction |
| Microsoft.Data.Sqlite 8.0.0 | Local database |
| Dapper 2.1.35 | ORM for SQLite access |
| Newtonsoft.Json 13.0.1 | API serialization |
| Microsoft.Extensions.DependencyInjection 8.0.0 | DI container |

## License

MIT — see [LICENSE](LICENSE).
