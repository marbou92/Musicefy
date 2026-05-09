# Musicefy 🎵

A lightweight music streaming desktop application for Windows 7+

## Features

- 🎵 Play local music files (MP3, WAV, FLAC, OGG, M4A)
- 🎚️ Volume control and playback controls
- 📋 Playlist management
- 🔍 Search and filter songs
- 📂 Folder scanning for music library
- 🎨 Clean and intuitive dark-themed UI
- ⚡ Lightweight and fast
- 💾 Persistent playlist storage

## System Requirements

- **OS:** Windows 7 or later
- **.NET Framework:** 4.7.2 or higher
- **RAM:** 256 MB minimum
- **Disk Space:** 50 MB

## Installation

1. Download the latest release from [Releases](https://github.com/marbou92/Musicefy/releases)
2. Extract the files
3. Run `Musicefy.exe`

## Building from Source

### Prerequisites
- Visual Studio 2019 or later
- .NET Framework 4.5+ Developer Pack

### Steps

1. Clone the repository:
```bash
git clone https://github.com/marbou92/Musicefy.git
cd Musicefy
```

2. Open `Musicefy.sln` in Visual Studio

3. Restore NuGet packages:
```
Install-Package NAudio
Install-Package TagLibSharp
```

4. Build the solution (Ctrl+Shift+B)

5. Run the application (F5)

## Project Structure

```
Musicefy/
├── Musicefy/                          # WPF UI Project
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── Properties/
│   ├── Resources/
│   └── Views/
├── Musicefy.Core/                     # Core Library Project
│   ├── Models/
│   │   └── MusicFile.cs
│   ├── Services/
│   │   ├── AudioPlayer.cs
│   │   └── PlaylistManager.cs
│   └── Properties/
├── Musicefy.sln
└── packages.config
```

## Dependencies

- **NAudio** v2.2.1 - Audio playback and processing
- **TagLibSharp** v2.2.0 - Reading music metadata

## Usage

1. **Add Music:** Click "Add Folder" to add a music directory
2. **Play:** Double-click a song or click the play button
3. **Create Playlist:** Right-click and select "New Playlist"
4. **Search:** Use the search bar to find songs
5. **Controls:** Use play, pause, next, previous buttons
6. **Volume:** Adjust volume with the slider

## Contributing

Contributions are welcome! Feel free to open issues and pull requests.

## License

MIT License - See LICENSE file for details

## Author

Created by [marbou92](https://github.com/marbou92)

---

**Note:** This project is built to be compatible with Windows 7 SP1 and later versions.
