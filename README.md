# Musicefy

A cross-platform music player built with Qt/C++, featuring YouTube streaming, local library management, and Material You theming.

![Build](https://img.shields.io/badge/build-passing-brightgreen)
![License](https://img.shields.io/badge/license-MIT-blue)
![Qt](https://img.shields.io/badge/Qt-5.15-green)

## Features

- **YouTube Streaming** — Search and play music from YouTube with bot-detection mitigation and cipher extraction
- **Local Library** — Scan, organize, and browse your local music collection with smart playlists (Most Played, Recently Added, Favourites, etc.)
- **Material You Theming** — 18+ built-in color schemes with dynamic accent color extraction from album art
- **Equalizer** — 14 presets + custom 10-band equalizer
- **ReplayGain** — Automatic volume normalization from track tags
- **Lyrics** — Free lyrics fetching via LRCLIB
- **Scrobbling** — Last.fm integration with MD5-signed API
- **SMTC Integration** — Windows media transport controls (lock screen, volume flyout, media keys)
- **Subsonic Support** — Connect to any Subsonic-compatible server
- **Extension System** — Load and manage third-party extensions

## Screenshots

*Screenshots coming soon*

## Building

### Prerequisites

- **Windows**: Visual Studio 2019+ (MSVC v142), Qt 5.15.2, CMake 3.16+
- **Qt modules**: QtMultimedia, QtWidgets, QtNetwork, QtSql, QtSvg

### Build Steps

```bash
# Clone the repository
git clone https://github.com/yourusername/MusicefyQt.git
cd MusicefyQt

# Configure (Windows/MSVC)
mkdir build && cd build
cmake .. -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_PREFIX_PATH="C:/Qt/5.15.2/msvc2019_64"

# Build
cmake --build . --parallel
```

### Running Tests

```bash
cd build
ctest --output-on-failure
```

## Project Structure

```
MusicefyQt/
├── src/
│   ├── app/              # Application layer (widgets, viewmodels, lifecycle)
│   ├── core/             # Core business logic
│   │   ├── database/     # SQLite persistence
│   │   ├── models/       # Data models
│   │   ├── interfaces/   # Abstract interfaces
│   │   ├── playback/     # Audio playback + queue management
│   │   ├── services/     # Business services
│   │   ├── sources/      # Music source providers (YouTube, Subsonic, Local)
│   │   └── theme/        # Material You theming engine
│   └── ...
├── tests/                # Unit tests (Qt Test)
├── third_party/          # material_color_utilities
├── cmake/                # Build helpers
└── scripts/              # Build, sign, and release scripts
```

## Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for detailed architecture documentation.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [material-color-utilities](https://github.com/material-foundation/material-color-utilities) by Google — HCT color space and DynamicScheme
- [Qt](https://www.qt.io/) — Cross-platform UI framework
